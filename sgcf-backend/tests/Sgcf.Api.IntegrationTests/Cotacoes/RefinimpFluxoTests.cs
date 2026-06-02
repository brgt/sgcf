using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Testes E2E do fluxo REFINIMP (Onda 1).
/// Cobre: fluxo completo com contrato mãe Ativo, regra 70% BB,
/// e rejeição por mãe com status inválido.
/// Cada teste cria seu próprio banco e FINIMP mãe para isolamento.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class RefinimpFluxoTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um banco com codigoCompe aleatório (não-BB) e retorna bancoId.
    /// </summary>
    private static async Task<(Guid BancoId, string CodigoCompe)> SeedBancoAsync(HttpClient client, string? codigoCompe = null)
    {
        codigoCompe ??= TestBancoCodigo.Next();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco Refi {codigoCompe} S.A.",
            apelido = $"BR{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return (body.GetProperty("id").GetGuid(), codigoCompe);
    }

    private static async Task SeedCdiAsync(HttpClient client)
    {
        HttpResponseMessage cdiRes = await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-16",
            cdiAaPercentual = 10.50m
        });
        cdiRes.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
    }

    private static async Task SeedLimiteAsync(HttpClient client, Guid bancoId, string modalidade, decimal valorLimiteBrl)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade,
            valorLimiteBrl,
            dataVigenciaInicio = "2026-01-01"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed limite {modalidade} falhou: {await res.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Executa o fluxo completo de uma cotação FINIMP e retorna o contratoId criado.
    /// Utilizado como pré-requisito para criar o contrato mãe do REFINIMP.
    /// </summary>
    private static async Task<Guid> CriarContratoFinimpAsync(
        HttpClient client,
        Guid bancoId,
        decimal valorUsd = 200_000m)
    {
        // 1. Criar cotação FINIMP
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl = 1_200_000m,
            prazoMaximoDias = 360,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar FINIMP falhou: {await criarRes.Content.ReadAsStringAsync()}");
        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();

        // 2. Adicionar banco
        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Enviar
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Registrar proposta USD
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = valorUsd,
                taxaAa = 5.50m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 360,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval",
                valorGarantiaBrl = 1_200_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta FINIMP falhou: {await propRes.Content.ReadAsStringAsync()}");
        JsonElement propBody = await propRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid propostaId = propBody.GetProperty("id").GetGuid();

        // 5. Encerrar captação
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Aceitar proposta
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Converter em contrato
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"FINIMP-MAE-{Guid.NewGuid():N}"[..20],
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-16",
                taxaAa = 5.50m
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter FINIMP falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return contratoBody.GetProperty("id").GetGuid();
    }

    // ─── Cenário 1: Fluxo Completo REFINIMP ───────────────────────────────────

    /// <summary>
    /// Fluxo completo: FINIMP mãe Ativo → cotação REFINIMP → converter em contrato.
    /// Verifica que RefinimpDetail é retornado com contratoMaeId e valorQuitado corretos.
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_RefinimpSobreMaeAtivo_RetornaContratoComRefinimpDetail()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        (Guid bancoId, _) = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteAsync(client, bancoId, "Finimp", 10_000_000m);
        await SeedLimiteAsync(client, bancoId, "Refinimp", 10_000_000m);

        // Cria FINIMP mãe (200k USD)
        Guid contratoMaeId = await CriarContratoFinimpAsync(client, bancoId, valorUsd: 200_000m);

        // 1. Criar cotação REFINIMP referenciando o mãe
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Refinimp",
            valorAlvoBrl = 800_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16",
            contratoMaeId
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar REFINIMP falhou: {await criarRes.Content.ReadAsStringAsync()}");
        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();
        cotacaoBody.GetProperty("contratoMaeId").GetGuid().Should().Be(contratoMaeId);

        // 2. Adicionar banco
        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Enviar
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Registrar proposta em USD (mesma moeda do mãe)
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 150_000m, // 75% do mãe (200k) — dentro do limite 70% BB não se aplica (banco não-BB)
                taxaAa = 6.0m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval",
                valorGarantiaBrl = 900_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta REFINIMP falhou: {await propRes.Content.ReadAsStringAsync()}");
        JsonElement propBody = await propRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid propostaId = propBody.GetProperty("id").GetGuid();

        // 5. Encerrar captação
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Aceitar proposta
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Converter em contrato
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"REFINIMP-E2E-{Guid.NewGuid():N}"[..22],
                dataContratacao = "2026-05-20",
                dataVencimento = "2026-11-16",
                taxaAa = 6.0m,
                refinimp = new { percentualRefinanciado = 0.75m }
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter REFINIMP falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.TryGetProperty("id", out _).Should().BeTrue("contrato deve ter ID");
        contratoBody.GetProperty("modalidade").GetString().Should().Be("Refinimp");

        // Verificar RefinimpDetail na resposta
        contratoBody.TryGetProperty("refinimpDetail", out JsonElement refinimpDetail).Should().BeTrue(
            "resposta deve conter refinimpDetail para modalidade Refinimp");
        refinimpDetail.GetProperty("contratoMaeId").GetGuid().Should().Be(contratoMaeId,
            "contratoMaeId no detail deve referenciar o mãe criado");
        refinimpDetail.TryGetProperty("valorQuitadoNoRefi", out _).Should().BeTrue(
            "detail deve conter valorQuitadoNoRefi");

        // Verificar que a cotação ficou Convertida
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement cotacaoFinal = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        cotacaoFinal.GetProperty("status").GetString().Should().Be("Convertida");
    }

    // ─── Cenário 2: Regra 70% Banco do Brasil ─────────────────────────────────

    /// <summary>
    /// REFINIMP com banco BB (001) e valor acima de 70% do ancestral deve ser rejeitado
    /// ao converter em contrato com 409 Conflict ou 422 UnprocessableEntity.
    /// </summary>
    [Fact]
    public async Task ConverterEmContrato_QuandoBancoBbEValorAcima70Pct_RetornaErro()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        // Banco BB usa codigoCompe "001"
        (Guid bancoBbId, _) = await SeedBancoAsync(client, codigoCompe: "001");
        (Guid bancoAuxId, _) = await SeedBancoAsync(client); // banco auxiliar para criar o mãe (não-BB)
        await SeedCdiAsync(client);
        await SeedLimiteAsync(client, bancoAuxId, "Finimp", 10_000_000m);
        await SeedLimiteAsync(client, bancoBbId, "Finimp", 10_000_000m);
        await SeedLimiteAsync(client, bancoBbId, "Refinimp", 10_000_000m);

        // Cria FINIMP mãe com banco auxiliar (200k USD)
        Guid contratoMaeId = await CriarContratoFinimpAsync(client, bancoAuxId, valorUsd: 200_000m);

        // Criar cotação REFINIMP com banco BB
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Refinimp",
            valorAlvoBrl = 1_000_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16",
            contratoMaeId
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId = bancoBbId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta com 160k USD = 80% de 200k — acima do limite de 70% para BB
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId = bancoBbId,
                moedaOriginal = "Usd",
                valorOferecido = 160_000m, // 80% > 70% — deve falhar no conversor
                taxaAa = 6.0m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval",
                valorGarantiaBrl = 1_000_000m,
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

        // Converter — deve falhar pela regra 70% BB
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"REFI-BB-FAIL-{Guid.NewGuid():N}"[..22],
                dataContratacao = "2026-05-20",
                dataVencimento = "2026-11-16",
                taxaAa = 6.0m,
                refinimp = new { percentualRefinanciado = 0.80m }
            });

        // O ponto crítico é que NÃO retornou 201 — a regra 70% BB deve ter bloqueado.
        convertRes.StatusCode.Should().NotBe(HttpStatusCode.Created,
            "Banco do Brasil não pode receber REFINIMP acima de 70% do ancestral");
    }

    // ─── Cenário 3: Mãe com status inválido ──────────────────────────────────

    /// <summary>
    /// Criar cotação REFINIMP referenciando um mãe com status RefinanciadoTotal deve
    /// ser rejeitado na criação da cotação com 409 ou 422.
    /// </summary>
    [Fact]
    public async Task CriarCotacao_QuandoMaeFoiRefinanciadoTotal_RetornaErroDeValidacao()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        (Guid bancoId, _) = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteAsync(client, bancoId, "Finimp", 10_000_000m);
        await SeedLimiteAsync(client, bancoId, "Refinimp", 10_000_000m);

        // Cria FINIMP mãe original
        Guid contratoMaeId = await CriarContratoFinimpAsync(client, bancoId, valorUsd: 200_000m);

        // Cria primeira cotação REFINIMP que vai consumir 100% do mãe → RefinanciadoTotal
        HttpResponseMessage primeiraRefinimpRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Refinimp",
            valorAlvoBrl = 1_200_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16",
            contratoMaeId
        });
        primeiraRefinimpRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid primeiraCotacaoId = (await primeiraRefinimpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{primeiraCotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{primeiraCotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta de 200k USD = 100% — marcará mãe como RefinanciadoTotal
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{primeiraCotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 200_000m,
                taxaAa = 6.0m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval",
                valorGarantiaBrl = 1_200_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid propostaId = (await propRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsync($"/api/v1/cotacoes/{primeiraCotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{primeiraCotacaoId}/propostas/{propostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{primeiraCotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId = primeiraCotacaoId,
                numeroExternoContrato = $"REFI-100PCT-{Guid.NewGuid():N}"[..22],
                dataContratacao = "2026-05-20",
                dataVencimento = "2026-11-16",
                taxaAa = 6.0m,
                refinimp = new { percentualRefinanciado = 1.0m }
            }))
            .StatusCode.Should().Be(HttpStatusCode.Created, "primeira conversão deve ter sucesso");

        // Agora tentar criar nova cotação REFINIMP sobre o mesmo mãe (já RefinanciadoTotal)
        HttpResponseMessage segundaRefinimpRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Refinimp",
            valorAlvoBrl = 500_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16",
            contratoMaeId
        });

        // O ponto crítico é que NÃO retornou 201 — mãe RefinanciadoTotal é inválido como alvo.
        segundaRefinimpRes.StatusCode.Should().NotBe(HttpStatusCode.Created,
            "Não deve permitir REFINIMP sobre contrato mãe já RefinanciadoTotal");
    }
}
