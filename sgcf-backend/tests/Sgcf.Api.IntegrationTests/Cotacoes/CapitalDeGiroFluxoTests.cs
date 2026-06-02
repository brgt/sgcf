using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Testes E2E do fluxo Capital de Giro BRL (Onda 3b).
/// Cobre: fluxo completo (criar → 2 propostas BRL → comparativo → aceitar → converter),
/// rejeição de proposta USD em cotação Capital de Giro,
/// rejeição de proposta com ExigeNdf=true,
/// e verificação de que capitalDeGiroDetail está presente no contrato retornado.
/// SPEC docs/specs/cotacoes/modalidades/capital-de-giro.md §4, §5, §7.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class CapitalDeGiroFluxoTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Seed Helpers ─────────────────────────────────────────────────────────

    /// <summary>Cria banco com codigoCompe único (via <see cref="TestBancoCodigo"/>) e retorna o bancoId.</summary>
    private static async Task<Guid> SeedBancoAsync(HttpClient client)
    {
        string codigo = TestBancoCodigo.Next();
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco Capital de Giro {codigo} S.A.",
            apelido = $"CDG{codigo}",
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
        // 409 = já existe de outro teste na mesma fixture; ambos são aceitáveis.
        res.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
    }

    private static async Task SeedLimiteCapitalDeGiroAsync(HttpClient client, Guid bancoId, decimal valorLimiteBrl)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "CapitalDeGiro",
            valorLimiteBrl,
            dataVigenciaInicio = "2026-01-01"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed limite CapitalDeGiro falhou: {await res.Content.ReadAsStringAsync()}");
    }

    // ─── Cenário 1: Fluxo Completo Capital de Giro BRL ────────────────────────

    /// <summary>
    /// Fluxo completo: criar cotação CapitalDeGiro → 2 propostas BRL →
    /// comparativo → aceitar melhor proposta → converter em contrato com capitalDeGiroDetail.
    /// SPEC §4 e §7.
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_CapitalDeGiroBrl_RetornaContratoComCapitalDeGiroDetail()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteCapitalDeGiroAsync(client, bancoId, 10_000_000m);

        // 1. Criar cotação CapitalDeGiro — BRL puro: sem PTAX, sem dataPtaxReferencia.
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "CapitalDeGiro",
            valorAlvoBrl = 1_500_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-18",
            observacoes = "Capital de Giro BRL — fluxo E2E Onda 3b"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar CapitalDeGiro falhou: {await criarRes.Content.ReadAsStringAsync()}");

        JsonElement cotacaoBody = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid cotacaoId = cotacaoBody.GetProperty("id").GetGuid();
        cotacaoBody.GetProperty("modalidade").GetString().Should().Be("CapitalDeGiro");

        // Capital de Giro é BRL puro — ptaxUsadaUsdBrl deve ser null.
        if (cotacaoBody.TryGetProperty("ptaxUsadaUsdBrl", out JsonElement ptaxEl))
        {
            ptaxEl.ValueKind.Should().Be(JsonValueKind.Null,
                "CapitalDeGiro é BRL puro — ptaxUsadaUsdBrl deve ser null");
        }

        // 2. Adicionar banco ao rol de convidados
        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Enviar para captação (Rascunho → EmCaptacao)
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Registrar 2 propostas BRL com taxas diferentes — para exercitar o comparativo.
        // Proposta 1: taxa 14,5% a.a. (mais cara)
        HttpResponseMessage p1Res = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Brl",
                valorOferecido = 1_500_000m,
                taxaAa = 14.5m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval dos sócios",
                valorGarantiaBrl = 1_800_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        p1Res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta 1 CapitalDeGiro falhou: {await p1Res.Content.ReadAsStringAsync()}");

        JsonElement p1Body = await p1Res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid proposta1Id = p1Body.GetProperty("id").GetGuid();

        // CET deve ser calculado na criação da proposta e deve ser > 0.
        p1Body.TryGetProperty("cetCalculadoAaPercentual", out JsonElement cetP1El).Should().BeTrue();
        cetP1El.GetDecimal().Should().BeGreaterThan(0m, "CET CapitalDeGiro deve ser calculado na criação da proposta");

        // CET > taxa nominal porque IOF 0,38% incide sobre principal em t=0 (SPEC §7).
        cetP1El.GetDecimal().Should().BeGreaterThan(14.5m,
            "CET deve ser maior que a taxa nominal quando IOF > 0");

        // Proposta 2: taxa 13,8% a.a. (mais barata — vencerá o comparativo)
        HttpResponseMessage p2Res = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Brl",
                valorOferecido = 1_500_000m,
                taxaAa = 13.8m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval dos sócios",
                valorGarantiaBrl = 1_800_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        p2Res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta 2 CapitalDeGiro falhou: {await p2Res.Content.ReadAsStringAsync()}");
        Guid proposta2Id = (await p2Res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        // 5. Encerrar captação (EmCaptacao → Comparada)
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Comparativo — deve ordenar pela menor CET primeiro.
        HttpResponseMessage compRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}/comparativo");
        compRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement[] linhas = (await compRes.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts))!;
        linhas.Should().HaveCount(2, "cotação tem exatamente 2 propostas");

        // Melhor proposta (menor CET) deve ser a de taxa 13,8%.
        Guid melhorPropostaId = linhas[0].GetProperty("propostaId").GetGuid();
        melhorPropostaId.Should().Be(proposta2Id,
            "a proposta com taxa 13,8% (menor CET) deve liderar o comparativo");

        // Verificar presença das 3 métricas obrigatórias no comparativo.
        JsonElement melhor = linhas[0];
        melhor.TryGetProperty("taxaNominalAaPercentual", out _).Should().BeTrue("métrica taxaNominalAaPercentual ausente");
        melhor.TryGetProperty("cetAaPercentual", out _).Should().BeTrue("métrica cetAaPercentual ausente");
        melhor.TryGetProperty("custoTotalEquivalenteBrl", out _).Should().BeTrue("métrica custoTotalEquivalenteBrl ausente");

        // 7. Aceitar a melhor proposta
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/propostas/{melhorPropostaId}/aceitar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 8. Converter em contrato com capitalDeGiroDetail (numeroOperacao é opcional — SPEC EC-10).
        string numeroExterno = $"CDG-E2E-{Guid.NewGuid():N}"[..20];
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = numeroExterno,
                dataContratacao = "2026-05-20",
                dataVencimento = "2026-11-16",
                taxaAa = 13.8m,
                capitalDeGiro = new
                {
                    numeroOperacao = "OP-2026-CDG-001"
                }
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter CapitalDeGiro falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.GetProperty("modalidade").GetString().Should().Be("CapitalDeGiro");

        // capitalDeGiroDetail deve estar presente no DTO retornado.
        contratoBody.TryGetProperty("capitalDeGiroDetail", out JsonElement cgDetail).Should().BeTrue(
            "capitalDeGiroDetail deve estar no ContratoDto para modalidade CapitalDeGiro");
        cgDetail.GetProperty("numeroOperacao").GetString().Should().Be("OP-2026-CDG-001",
            "numeroOperacao deve ser propagado para o detail");

        // 9. Verificar que a cotação transitou para Convertida.
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        JsonElement cotacaoFinal = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        cotacaoFinal.GetProperty("status").GetString().Should().Be("Convertida",
            "cotação deve estar Convertida após conversão bem-sucedida");
    }

    // ─── Cenário 2: Proposta USD em cotação CapitalDeGiro deve retornar 400 ───

    /// <summary>
    /// EC-CG-1: CapitalDeGiro é BRL puro — proposta em USD deve retornar 400.
    /// SPEC §4.2 e §5.2.
    /// </summary>
    [Fact]
    public async Task PropostaUsd_EmCotacaoCapitalDeGiro_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedLimiteCapitalDeGiroAsync(client, bancoId, 10_000_000m);

        // Criar cotação CapitalDeGiro
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "CapitalDeGiro",
            valorAlvoBrl = 1_000_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-18"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta com moedaOriginal=Usd deve ser rejeitada com 400.
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 200_000m,
                taxaAa = 6.0m,
                iofPct = 0.38m,
                spreadAa = 0.5m,
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

        propRes.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
            "CapitalDeGiro é BRL puro — proposta em Usd deve retornar 400/422 (SPEC §4.2)");
    }

    // ─── Cenário 3: Proposta com ExigeNdf=true deve retornar 400 ──────────────

    /// <summary>
    /// EC-CG-2: CapitalDeGiro não admite NDF — ExigeNdf=true deve retornar 400.
    /// SPEC §5.2.
    /// </summary>
    [Fact]
    public async Task PropostaComNdf_EmCotacaoCapitalDeGiro_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedLimiteCapitalDeGiroAsync(client, bancoId, 10_000_000m);

        // Criar cotação CapitalDeGiro
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "CapitalDeGiro",
            valorAlvoBrl = 1_000_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-18"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();

        (await client.PostAsJsonAsync($"/api/v1/cotacoes/{cotacaoId}/bancos", new { bancoId }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Proposta BRL com ExigeNdf=true deve ser rejeitada com 400.
        HttpResponseMessage propRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Brl",
                valorOferecido = 1_000_000m,
                taxaAa = 14.0m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 180,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = true,           // inválido para Capital de Giro
                custoNdfAa = 1.5m,
                garantiaExigida = "Aval",
                valorGarantiaBrl = 1_200_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });

        propRes.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
            "CapitalDeGiro não admite NDF — ExigeNdf=true deve retornar 400/422 (SPEC §5.2)");
    }

    // ─── Cenário 4: Converter sem capitalDeGiroDetail inclui detail com NumeroOperacao null ─

    /// <summary>
    /// NumeroOperacao é opcional (SPEC EC-10): omitir capitalDeGiro no payload
    /// deve converter com sucesso, retornando capitalDeGiroDetail com NumeroOperacao = null.
    /// </summary>
    [Fact]
    public async Task ConverterSemCapitalDeGiroInputs_RetornaDetailComNumeroOperacaoNull()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await SeedBancoAsync(client);
        await SeedCdiAsync(client);
        await SeedLimiteCapitalDeGiroAsync(client, bancoId, 10_000_000m);

        // Fluxo até aceitar proposta
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "CapitalDeGiro",
            valorAlvoBrl = 800_000m,
            prazoMaximoDias = 90,
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
                moedaOriginal = "Brl",
                valorOferecido = 800_000m,
                taxaAa = 13.5m,
                iofPct = 0.38m,
                spreadAa = 0.0m,
                prazoDias = 90,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Duplicatas",
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

        // Converter SEM capitalDeGiro no payload — NumeroOperacao fica null.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = $"CDG-NODET-{Guid.NewGuid():N}"[..22],
                dataContratacao = "2026-05-20",
                dataVencimento = "2026-08-18",
                taxaAa = 13.5m
                // capitalDeGiro ausente — NumeroOperacao deve ser null
            });
        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"converter sem capitalDeGiroInputs falhou: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.GetProperty("modalidade").GetString().Should().Be("CapitalDeGiro");

        // capitalDeGiroDetail deve existir com NumeroOperacao = null.
        contratoBody.TryGetProperty("capitalDeGiroDetail", out JsonElement cgDetail).Should().BeTrue(
            "capitalDeGiroDetail deve estar no DTO mesmo sem capitalDeGiro inputs");

        // NumeroOperacao deve ser null (JsonValueKind.Null ou propriedade ausente).
        if (cgDetail.TryGetProperty("numeroOperacao", out JsonElement numOpEl))
        {
            numOpEl.ValueKind.Should().Be(JsonValueKind.Null,
                "NumeroOperacao deve ser null quando não informado (SPEC EC-10)");
        }
    }
}
