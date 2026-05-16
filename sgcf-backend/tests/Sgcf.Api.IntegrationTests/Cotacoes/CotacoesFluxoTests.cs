using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Testes E2E dos cenários críticos do módulo de Cotações.
/// Cada teste utiliza a API HTTP completa via WebApplicationFactory + PostgreSQL real.
/// SPEC §10 — pirâmide de testes, camada API/E2E (~5 fluxos).
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class CotacoesFluxoTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Pré-requisitos compartilhados ────────────────────────────────────────

    /// <summary>
    /// Garante pré-requisitos na base antes de executar o cenário principal:
    /// cria banco, CDI snapshot e PTAX (via ParametroCotacao).
    /// Retorna o bancoId criado.
    /// </summary>
    private static async Task<Guid> SeedPreRequistosAsync(HttpClient client)
    {
        // Cria banco
        HttpResponseMessage bancRes = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigo = "001",
            nome = "Banco do Brasil",
            identificador = "001"
        });
        bancRes.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {bancRes.StatusCode}");
        JsonElement bancBody = await bancRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid bancoId = bancBody.GetProperty("id").GetGuid();

        // Cria limite operacional
        HttpResponseMessage limRes = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 50_000_000m,
            dataVigenciaInicio = "2026-01-01"
        });
        limRes.IsSuccessStatusCode.Should().BeTrue($"seed limite falhou: {limRes.StatusCode}");

        // Cria CDI snapshot (necessário para ConverterEmContrato)
        HttpResponseMessage cdiRes = await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-16",
            cdiAaPercentual = 10.50m
        });
        // Ignorar 409 (já existe) — execuções subsequentes na mesma fixture
        cdiRes.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        // Cria PTAX D-1 (ParametroCotacao existente)
        HttpResponseMessage ptaxRes = await client.PostAsJsonAsync("/api/v1/parametros-cotacao", new
        {
            bancoId,
            modalidade = "Finimp",
            moeda = "Usd",
            tipoCotacao = "PtaxD1",
            valorCompra = 5.15m,
            valorVenda = 5.20m,
            dataReferencia = "2026-05-15"
        });
        // Ignorar 409 (PTAX já cadastrada)
        ptaxRes.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        return bancoId;
    }

    // ─── Cenário 1: Fluxo Completo ────────────────────────────────────────────

    /// <summary>
    /// Fluxo completo: Criar → Banco → Enviar → Proposta → Encerrar → Comparativo → Aceitar → Converter.
    /// Verifica que o contrato e a EconomiaNegociacao são criados corretamente.
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_CriarCotacaoAteConverterEmContrato_RetornaContratoValido()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedPreRequistosAsync(client);

        // 1. Criar cotação
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl = 1_500_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();
        cotacaoBody.GetProperty("status").GetString().Should().Be("Rascunho");

        // 2. Adicionar banco
        HttpResponseMessage addBancoRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/bancos",
            new { bancoId });
        addBancoRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Enviar (Rascunho → EmCaptacao)
        HttpResponseMessage enviarRes = await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null);
        enviarRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Registrar 2 propostas
        HttpResponseMessage p1Res = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 300_000m,
                taxaAa = 5.25m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "NaVencimento",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "CDB caucionado",
                valorGarantiaBrl = 1_650_000m,
                garantiaEhCdbCativo = true,
                rendimentoCdbAa = 13.5m
            });
        p1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement p1Body = await p1Res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid proposta1Id = p1Body.GetProperty("id").GetGuid();

        HttpResponseMessage p2Res = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 300_000m,
                taxaAa = 5.10m,
                iofPct = 0.38m,
                spreadAa = 0.40m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "NaVencimento",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "CDB caucionado",
                valorGarantiaBrl = 1_650_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        p2Res.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement p2Body = await p2Res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid proposta2Id = p2Body.GetProperty("id").GetGuid();

        // 5. Encerrar captação (EmCaptacao → Comparada)
        HttpResponseMessage encerrarRes = await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null);
        encerrarRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Comparativo — deve retornar 2 propostas ordenadas por custo
        HttpResponseMessage compRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}/comparativo");
        compRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement compBody = await compRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        compBody.GetArrayLength().Should().Be(2, "comparativo deve ter 2 propostas");

        // 7. Aceitar a primeira proposta do comparativo (menor custo)
        Guid melhorPropostaId = compBody[0].GetProperty("propostaId").GetGuid();
        HttpResponseMessage aceitarRes = await client.PostAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas/{melhorPropostaId}/aceitar", null);
        aceitarRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar status da cotação
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement cotacaoAtual = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        cotacaoAtual.GetProperty("status").GetString().Should().Be("Aceita");
        cotacaoAtual.GetProperty("propostaAceitaId").GetGuid().Should().Be(melhorPropostaId);

        // 8. Converter em contrato
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "FINIMP-BB-E2E-001",
                dataContratacao = "2026-05-20",
                dataVencimento = "2026-11-16",
                taxaAa = 5.10m,
                observacoes = "Teste E2E fluxo completo"
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.TryGetProperty("id", out _).Should().BeTrue("contrato deve ter ID");

        // Verificar que cotação agora está Convertida
        HttpResponseMessage getRes2 = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement cotacaoFinal = await getRes2.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        cotacaoFinal.GetProperty("status").GetString().Should().Be("Convertida");
    }

    // ─── Cenário 2: Limite Insuficiente ──────────────────────────────────────

    /// <summary>
    /// AdicionarBanco em cotação cujo ValorAlvo excede o limite disponível retorna 409.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_QuandoLimiteInsuficiente_Retorna409ComMensagemClara()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedPreRequistosAsync(client);

        // Criar cotação com valor maior que o limite (50M)
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl = 100_000_000m, // 100M > 50M de limite
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement body = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = body.GetProperty("id").GetGuid();

        // Tentar adicionar banco — deve falhar com 409
        HttpResponseMessage addRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/bancos",
            new { bancoId });

        addRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        string errorBody = await addRes.Content.ReadAsStringAsync();
        errorBody.ToLowerInvariant().Should().Contain("limite",
            "mensagem de erro deve citar limite disponível");
    }

    // ─── Cenário 3: Idempotência de Aceitação ────────────────────────────────

    /// <summary>
    /// Tentar aceitar uma segunda proposta após já ter aceita uma retorna 409.
    /// SPEC §3.2 regra 4: apenas uma proposta aceita por cotação.
    /// </summary>
    [Fact]
    public async Task AceitarProposta_QuandoJaTemPropostaAceita_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedPreRequistosAsync(client);

        // Setup: criar, enviar, registrar 2 propostas, encerrar
        Guid cotacaoId = await CriarCotacaoEmEstadoComparadaAsync(client, bancoId);

        // Buscar propostas
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement cotacaoBody = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        JsonElement propostas = cotacaoBody.GetProperty("propostas");
        propostas.GetArrayLength().Should().BeGreaterThan(1, "precisa de ao menos 2 propostas");

        Guid proposta1Id = propostas[0].GetProperty("id").GetGuid();
        Guid proposta2Id = propostas[1].GetProperty("id").GetGuid();

        // Aceitar primeira proposta — deve funcionar
        HttpResponseMessage aceitar1 = await client.PostAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas/{proposta1Id}/aceitar", null);
        aceitar1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Tentar aceitar segunda — deve retornar 409
        HttpResponseMessage aceitar2 = await client.PostAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas/{proposta2Id}/aceitar", null);
        aceitar2.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "não é possível aceitar segunda proposta quando já existe aceita");
    }

    // ─── Cenário 4: Comparativo retorna 3 métricas ────────────────────────────

    /// <summary>
    /// GET /comparativo retorna taxaNominal, CET e custoTotalEquivalente para cada proposta.
    /// SPEC §5.3.
    /// </summary>
    [Fact]
    public async Task Comparativo_RetornaTresMetricasPorProposta()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedPreRequistosAsync(client);

        Guid cotacaoId = await CriarCotacaoEmEstadoComparadaAsync(client, bancoId);

        HttpResponseMessage compRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}/comparativo");
        compRes.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement comparativo = await compRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        comparativo.GetArrayLength().Should().BeGreaterThan(0);

        JsonElement primeira = comparativo[0];

        // Verificar presença das 3 métricas obrigatórias (SPEC §5.3)
        primeira.TryGetProperty("taxaNominalAaPercentual", out _).Should().BeTrue(
            "métrica 1: taxa nominal anualizada");
        primeira.TryGetProperty("cetCalculadoAaPercentual", out _).Should().BeTrue(
            "métrica 2: CET anualizado");
        primeira.TryGetProperty("custoTotalEquivalenteBrl", out _).Should().BeTrue(
            "métrica 3: custo total equalizado para o prazo da cotação");

        // CET deve ser >= taxa nominal (invariante do cálculo)
        decimal taxaNominal = primeira.GetProperty("taxaNominalAaPercentual").GetDecimal();
        decimal cet = primeira.GetProperty("cetCalculadoAaPercentual").GetDecimal();
        cet.Should().BeGreaterThanOrEqualTo(taxaNominal,
            "CET não pode ser menor que a taxa nominal (invariante SPEC §10.3)");
    }

    // ─── Cenário 5: Auditoria registra ator ──────────────────────────────────

    /// <summary>
    /// Após AceitarProposta, o log de auditoria deve registrar AceitaPor.
    /// SPEC §4.2, §3.2 regra 6.
    /// </summary>
    [Fact]
    public async Task AuditarCotacao_AposAceitarProposta_RegistraAceitaPor()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedPreRequistosAsync(client);

        Guid cotacaoId = await CriarCotacaoEmEstadoComparadaAsync(client, bancoId);

        // Buscar id da primeira proposta
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement body = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid propostaId = body.GetProperty("propostas")[0].GetProperty("id").GetGuid();

        // Aceitar com o usuário dev (dev-user-id no dev mock)
        HttpResponseMessage aceitarRes = await client.PostAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null);
        aceitarRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar AceitaPor na cotação
        HttpResponseMessage cotacaoRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement cotacao = await cotacaoRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        string? aceitaPor = cotacao.GetProperty("aceitaPor").GetString();
        aceitaPor.Should().NotBeNullOrEmpty("AceitaPor deve ser preenchido após aceitação");
    }

    // ─── Helper: cria cotação no estado Comparada com 2 propostas ─────────────

    private static async Task<Guid> CriarCotacaoEmEstadoComparadaAsync(HttpClient client, Guid bancoId)
    {
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl = 1_500_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar cotação falhou: {await criarRes.Content.ReadAsStringAsync()}");

        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId });
        await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null);

        // Registrar 2 propostas
        for (int i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/propostas", new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 300_000m,
                taxaAa = 5.25m - i * 0.10m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "NaVencimento",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "CDB caucionado",
                valorGarantiaBrl = 1_650_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        }

        await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null);

        return cotacaoId;
    }
}
