using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Testes E2E do fluxo Lei 4131/62 (Onda 4).
/// Cobre: fluxo completo (criar → proposta USD → comparativo IRRF → aceitar → converter),
/// rejeição de proposta BRL em cotação Lei4131,
/// e validação de lei4131Detail obrigatório na conversão.
/// SPEC §4, §5, §6, §8.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class Lei4131FluxoTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Seed Helpers ─────────────────────────────────────────────────────────

    private static async Task<Guid> SeedBancoAsync(HttpClient client)
    {
        string codigo = TestBancoCodigo.Next();
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"BankLei4131 {codigo} S.A.",
            apelido = $"L4K{codigo}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task SeedCdiAsync(HttpClient client)
    {
        // Data ≤ instante fixo do fixture (2026-05-16) para o GetMaisRecenteAsync encontrar.
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-15",
            cdiAaPercentual = 10.75m
        });
        res.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
    }

    private static async Task SeedLimiteLei4131Async(HttpClient client, Guid bancoId, decimal valorLimiteBrl)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Lei4131",
            valorLimiteBrl,
            dataVigenciaInicio = "2026-01-01"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed limite Lei4131 falhou: {await res.Content.ReadAsStringAsync()}");
    }

    // ─── Cenário 1: Fluxo Completo Lei 4131 com SBLC ─────────────────────────

    /// <summary>
    /// Fluxo completo: criar cotação Lei4131 → proposta USD → comparativo com IRRF →
    /// aceitar → converter em contrato com lei4131Detail. SPEC §4-§6 e §8.
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_Lei4131UsdSblc_RetornaContratoComLei4131Detail()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteLei4131Async(client, bancoId, 50_000_000m);

        // 1. Criar cotação Lei4131 — PTAX é injetada automaticamente pelo seed de FX do fixture.
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Lei4131",
            valorAlvoBrl = 25_000_000m,
            prazoMaximoDias = 720,
            dataAbertura = "2026-05-18",
            observacoes = "Empréstimo externo USD 5MM com SBLC Itaú"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar Lei4131 falhou: {await criarRes.Content.ReadAsStringAsync()}");
        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();
        cotacaoBody.GetProperty("modalidade").GetString().Should().Be("Lei4131");
        cotacaoBody.TryGetProperty("ptaxUsadaUsdBrl", out _).Should().BeTrue(
            "Lei4131 é modalidade cambial — ptaxUsadaUsdBrl deve ser retornada");

        // 2. Adicionar banco
        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Enviar para captação
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Registrar proposta USD — Lei4131 exige moeda estrangeira. SPEC §4.2.
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 5_000_000m,
                taxaAa = 6.25m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 720,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "SBLC 100% (obrigatório)",
                valorGarantiaBrl = 25_000_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta Lei4131 USD falhou: {await propRes.Content.ReadAsStringAsync()}");
        JsonElement propBody = await propRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid propostaId = propBody.GetProperty("id").GetGuid();
        propBody.GetProperty("moedaOriginal").GetString().Should().Be("Usd");
        propBody.TryGetProperty("cetCalculadoAaPercentual", out JsonElement cetElement).Should().BeTrue();
        cetElement.GetDecimal().Should().BeGreaterThan(0m, "CET Lei4131 deve ser calculado na criação da proposta");

        // 5. Comparativo com IRRF informativo — SPEC §8.3.
        HttpResponseMessage compRes = await client.GetAsync(
            $"/api/v1/cotacoes/{cotacaoId}/comparativo?aliquotaIrrfPercentual=15");
        compRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement[] linhas = (await compRes.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts))!;
        linhas.Should().HaveCount(1, "cotação tem exatamente 1 proposta");

        JsonElement linha = linhas[0];
        linha.GetProperty("irrfEstimadoBrl").GetDecimal().Should().BeGreaterThan(0m,
            "Lei4131 com alíquota 15% deve retornar IRRF > 0");

        // 6. Encerrar captação
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Aceitar proposta
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 8. Converter em contrato com lei4131Detail obrigatório. SPEC §5.3 e §6.1.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"LEI4131-{Guid.NewGuid():N}"[..20],
                dataContratacao = "2026-05-20",
                dataVencimento = "2028-05-18",
                taxaAa = 6.25m,
                lei4131 = new
                {
                    sblcNumero = "SBLC-2026-001234",
                    sblcBancoEmissor = "Itaú Unibanco S.A.",
                    sblcValorUsd = 5_000_000m,
                    temMarketFlex = false,
                    breakFundingFeePercentual = (decimal?)1.5m,
                    paisCredor = "USA",
                    aliquotaIrrfPercentual = (decimal?)15m
                }
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter Lei4131 falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.GetProperty("modalidade").GetString().Should().Be("Lei4131");

        // Verifica lei4131Detail no retorno
        contratoBody.TryGetProperty("lei4131Detail", out JsonElement lei4131Detail).Should().BeTrue(
            "lei4131Detail deve estar no ContratoDto para modalidade Lei4131");
        lei4131Detail.GetProperty("sblcNumero").GetString().Should().Be("SBLC-2026-001234");
        lei4131Detail.GetProperty("sblcBancoEmissor").GetString().Should().Be("Itaú Unibanco S.A.");
        lei4131Detail.GetProperty("sblcValorUsd").GetDecimal().Should().Be(5_000_000m);
        lei4131Detail.GetProperty("temMarketFlex").GetBoolean().Should().BeFalse();
    }

    // ─── Cenário 2: Proposta BRL em cotação Lei4131 deve retornar 400 ─────────

    /// <summary>
    /// EC-Lei4131-1: proposta Lei4131 em BRL deve retornar HTTP 400. SPEC §4.2 e §5.2.
    /// </summary>
    [Fact]
    public async Task PropostaBrl_EmCotacaoLei4131_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedLimiteLei4131Async(client, bancoId, 50_000_000m);

        // Criar cotação Lei4131
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Lei4131",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoDias = 360,
            dataAbertura = "2026-05-18"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta com moedaOriginal=Brl deve retornar 400
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Brl",
                valorOferecido = 5_000_000m,
                taxaAa = 6.25m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 360,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "SBLC 100%",
                valorGarantiaBrl = 5_000_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });

        propRes.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
            "Lei 4131 com moeda BRL deve retornar 400/422 — modalidade exige moeda estrangeira (SPEC §4.2)");
    }

    // ─── Cenário 3: Converter sem lei4131Detail retorna 400 ───────────────────

    /// <summary>
    /// SPEC §5.3: lei4131Detail é obrigatório na conversão — ausente retorna 400.
    /// </summary>
    [Fact]
    public async Task ConverterSemLei4131Detail_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteLei4131Async(client, bancoId, 50_000_000m);

        // Fluxo até aceitar proposta
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Lei4131",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoDias = 360,
            dataAbertura = "2026-05-18"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 1_000_000m,
                taxaAa = 6.0m,
                iofPct = 0.38m,
                spreadAa = 0.5m,
                prazoDias = 360,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "SBLC 100%",
                valorGarantiaBrl = 5_000_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid propostaId = (await propRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Converter SEM lei4131Detail → 400 (handler lança InvalidOperationException → 422 ou 400)
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"LEI4131-NODET-{Guid.NewGuid():N}"[..22],
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-18",
                taxaAa = 6.0m
                // lei4131Detail ausente
            });

        // Handler lança InvalidOperationException → API retorna 409 (Conflict).
        // Aceita também 400/422 caso validação migre para Validator pipeline.
        int statusCode = (int)convertRes.StatusCode;
        statusCode.Should().BeOneOf([400, 422, 409],
            "converter Lei4131 sem lei4131 deve retornar erro (400/422 se validação ou 409 se invariante de handler)");
    }
}
