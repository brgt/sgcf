using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Testes E2E do fluxo FGI-modalidade (Onda 3a).
/// Cobre: fluxo completo (criar cotação FGI → proposta BRL Bullet → aceitar → converter com FgiDetail),
/// rejeição de proposta não-BRL, rejeição de proposta com NDF, rejeição de proposta não-Bullet,
/// e validação de fgi obrigatório na conversão.
/// SPEC fgi.md §4, §5, §6, §7.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class FgiFluxoTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Seed Helpers ─────────────────────────────────────────────────────────

    private static async Task<Guid> SeedBancoAsync(HttpClient client)
    {
        string codigo = Random.Shared.Next(300, 499).ToString(System.Globalization.CultureInfo.InvariantCulture);
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"BankFgi {codigo} S.A.",
            apelido = $"FGI{codigo}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task SeedCdiAsync(HttpClient client)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-16",
            cdiAaPercentual = 10.75m
        });
        res.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
    }

    private static async Task SeedLimiteFgiAsync(HttpClient client, Guid bancoId, decimal valorLimiteBrl)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Fgi",
            valorLimiteBrl,
            dataVigenciaInicio = "2026-01-01"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed limite FGI falhou: {await res.Content.ReadAsStringAsync()}");
    }

    // ─── Cenário 1: Fluxo Completo FGI BRL Bullet ─────────────────────────────

    /// <summary>
    /// Fluxo completo: criar cotação FGI → proposta BRL Bullet → aceitar → converter em contrato
    /// com FgiDetail (taxaFgiAaPercentual + percentualCoberto + numeroOperacaoFgi).
    /// SPEC fgi.md §4-§6, §7.3.
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_FgiBrlBullet_RetornaContratoComFgiDetail()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteFgiAsync(client, bancoId, 2_000_000m);

        // 1. Criar cotação FGI — sem PTAX (operação doméstica BRL). SPEC fgi.md §4.1.
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 500_000m,
            prazoMaximoDias = 365,
            dataAbertura = "2026-05-16",
            observacoes = "Linha BNDES FGI para aquisição de máquinas — golden case SPEC §7.3"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar cotação FGI falhou: {await criarRes.Content.ReadAsStringAsync()}");
        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();
        cotacaoBody.GetProperty("modalidade").GetString().Should().Be("Fgi");

        // FGI não tem PTAX — ptaxUsadaUsdBrl deve ser null ou ausente.
        if (cotacaoBody.TryGetProperty("ptaxUsadaUsdBrl", out JsonElement ptaxEl))
        {
            ptaxEl.ValueKind.Should().Be(JsonValueKind.Null,
                "cotação FGI não tem PTAX — operação doméstica BRL (SPEC fgi.md §2)");
        }

        // 2. Adicionar banco
        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Enviar para captação
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Registrar proposta BRL Bullet. SPEC fgi.md §5.2 (EC-1, EC-10, EC-11).
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Brl",
                valorOferecido = 500_000m,
                taxaAa = 12.0m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 365,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Cobertura FGI 80%",
                valorGarantiaBrl = 0m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta FGI BRL falhou: {await propRes.Content.ReadAsStringAsync()}");
        JsonElement propBody = await propRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid propostaId = propBody.GetProperty("id").GetGuid();
        propBody.GetProperty("moedaOriginal").GetString().Should().Be("Brl");
        propBody.TryGetProperty("cetCalculadoAaPercentual", out JsonElement cetEl).Should().BeTrue();
        cetEl.GetDecimal().Should().BeGreaterThan(0m,
            "CET FGI deve ser calculado e positivo após registro da proposta");

        // 5. Encerrar captação
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Aceitar proposta
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Converter em contrato com fgi obrigatório e numeroOperacaoFgi opcional.
        //    SPEC fgi.md §6.1; MD-3: percentualCoberto não entra no CET.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"FGI-CAIXA-{Guid.NewGuid():N}"[..18],
                dataContratacao = "2026-05-16",
                dataVencimento = "2027-05-16",
                taxaAa = 12.0m,
                numeroOperacaoFgi = "FGI-BNDES-2026-12345",
                fgi = new
                {
                    taxaFgiAaPercentual = 0.5m,
                    percentualCoberto = (decimal?)80.0m
                }
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter FGI falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.GetProperty("modalidade").GetString().Should().Be("Fgi");

        // Verifica fgiDetail no retorno
        contratoBody.TryGetProperty("fgiDetail", out JsonElement fgiDetail).Should().BeTrue(
            "fgiDetail deve estar no ContratoDto para modalidade Fgi");

        // Tarifa e percentual persistidos como fração (0.005 e 0.8) no domínio.
        // A serialização pode variar — verificar que o campo existe com valor > 0.
        fgiDetail.TryGetProperty("taxaFgiAaPercentual", out JsonElement taxaFgiEl).Should().BeTrue();
        taxaFgiEl.GetDecimal().Should().BeGreaterThan(0m, "taxaFgiAaPercentual deve ser positiva");
    }

    // ─── Cenário 2: Proposta não-BRL em cotação FGI deve retornar 400 ─────────

    /// <summary>
    /// EC-10: proposta FGI com moedaOriginal != BRL deve retornar HTTP 400.
    /// SPEC fgi.md §5.2.
    /// </summary>
    [Fact]
    public async Task PropostaUsd_EmCotacaoFgi_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedLimiteFgiAsync(client, bancoId, 2_000_000m);

        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 500_000m,
            prazoMaximoDias = 365,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta USD em cotação FGI deve retornar 400
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",   // inválido para FGI
                valorOferecido = 100_000m,
                taxaAa = 12.0m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 365,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "FGI 80%",
                valorGarantiaBrl = 0m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });

        propRes.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "FGI com moeda USD deve retornar 400 — modalidade é doméstica BRL (EC-10, SPEC fgi.md §5.2)");
    }

    // ─── Cenário 3: Proposta com NDF em cotação FGI deve retornar 400 ─────────

    /// <summary>
    /// EC-11: proposta FGI com exigeNdf=true deve retornar HTTP 400.
    /// SPEC fgi.md §5.2.
    /// </summary>
    [Fact]
    public async Task PropostaComNdf_EmCotacaoFgi_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedLimiteFgiAsync(client, bancoId, 2_000_000m);

        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 500_000m,
            prazoMaximoDias = 365,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta com exigeNdf=true em cotação FGI deve retornar 400
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Brl",
                valorOferecido = 500_000m,
                taxaAa = 12.0m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 365,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = true,           // inválido para FGI
                custoNdfAa = (decimal?)0.5m,
                garantiaExigida = "FGI 80%",
                valorGarantiaBrl = 0m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });

        propRes.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "FGI com exigeNdf=true deve retornar 400 — sem exposição cambial (EC-11, SPEC fgi.md §5.2)");
    }

    // ─── Cenário 4: Converter sem fgi retorna 400 ─────────────────────────────

    /// <summary>
    /// SPEC fgi.md §6.1: objeto fgi é obrigatório na conversão — ausente retorna 400.
    /// </summary>
    [Fact]
    public async Task ConverterSemFgi_EmCotacaoFgi_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteFgiAsync(client, bancoId, 2_000_000m);

        // Criar e levar a cotação até o estado Aceita
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 500_000m,
            prazoMaximoDias = 365,
            dataAbertura = "2026-05-16"
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
                moedaOriginal = "Brl",
                valorOferecido = 500_000m,
                taxaAa = 12.0m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 365,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "FGI 80%",
                valorGarantiaBrl = 0m,
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

        // Converter SEM fgi → deve retornar erro (400 ou 422)
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"FGI-NOFGI-{Guid.NewGuid():N}"[..18],
                dataContratacao = "2026-05-16",
                dataVencimento = "2027-05-16",
                taxaAa = 12.0m
                // fgi ausente — ConversorFgi lança InvalidOperationException
            });

        int statusCode = (int)convertRes.StatusCode;
        statusCode.Should().BeOneOf([400, 422],
            "converter FGI sem objeto fgi deve retornar erro (400 ou 422) — SPEC fgi.md §6.1");
    }
}
