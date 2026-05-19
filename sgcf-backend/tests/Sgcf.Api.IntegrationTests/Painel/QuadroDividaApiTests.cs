using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Painel;

// Helpers de seed reutilizáveis entre testes de quadro-divida
file static class QuadroDividaSeedHelper
{
    public static async Task<Guid> CriarBancoAsync(HttpClient client, string codigo, string apelido)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco {apelido} S.A.",
            apelido,
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou ({res.StatusCode})");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))
               .GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CriarCenarioAsync(HttpClient client, int anoBase)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/simulacoes/cenarios", new
        {
            nome = $"Cenário E2E {anoBase}",
            anoBase
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed cenario falhou ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))
               .GetProperty("id").GetGuid();
    }

    public static async Task AdicionarSimulacaoAsync(
        HttpClient client,
        Guid cenarioId,
        Guid bancoId,
        decimal valorPrincipal,
        string dataContratacao,
        string dataPrimeiroVencimento)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync(
            $"/api/v1/simulacoes/cenarios/{cenarioId}/simulacoes",
            new
            {
                bancoId,
                modalidade = "CapitalDeGiro",
                moeda = "Brl",
                valorPrincipal,
                dataContratacaoPrevista = dataContratacao,
                dataPrimeiroVencimento,
                tipoTaxa = "Fixa",
                taxaAa = 12.0m,
                baseCalculo = "Dias252",
                estruturaAmortizacao = "Bullet",
                periodicidade = "Bullet",
                quantidadeParcelas = 1,
                anchorDiaMes = "DiaContratacao"
            });
        res.IsSuccessStatusCode.Should().BeTrue($"seed simulacao falhou ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");
    }
}

/// <summary>
/// Testes E2E do endpoint <c>GET /api/v1/painel/quadro-divida</c>.
///
/// Usa a fixture <see cref="PainelVencimentosApiFixture"/> (PostgreSQL + WebApplicationFactory)
/// com clock fixado em 2026-05-18 → ano corrente = 2026.
///
/// Cenários cobertos:
///   9.  Ano corrente com pelo menos um banco + contrato → retorna estrutura válida com snapshot e projeção.
///   10. Sem parâmetro ano → usa o ano corrente (2026) automaticamente.
///   11. Ano inválido (fora 2020–2050) → HTTP 400.
///   12. (Fase 3) Com cenarioId válido → retorna cenarioAplicado + captação no mês correto.
///   13. (Fase 3) Com cenarioId inexistente → HTTP 404.
/// </summary>
[Collection("PainelVencimentosApi")]
[Trait("Category", "Slow")]
public sealed class QuadroDividaApiTests(PainelVencimentosApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers de seed (mesma estrutura dos outros testes da fixture) ─────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigo, string apelido)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco {apelido} S.A.",
            apelido,
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should()
           .BeTrue($"seed banco falhou ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");

        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
               .GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarContratoBrlAsync(
        HttpClient client, Guid bancoId, decimal valor)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/contratos", new
        {
            numeroExterno = $"QD-E2E-{Guid.NewGuid():N}",
            bancoId,
            modalidade = "CapitalDeGiro",
            moeda = "Brl",
            valorPrincipal = valor,
            dataContratacao = "2026-01-01",
            dataVencimento = "2028-12-01",
            taxaAa = 10.0m,
            baseCalculo = "Dias252",
            contratoPaiId = (Guid?)null,
            periodicidade = "Bullet",
            estruturaAmortizacao = "Bullet",
            quantidadeParcelas = 1,
            dataPrimeiroVencimento = "2028-12-01",
            capitalDeGiroDetail = new { numeroOperacao = (string?)null, tipoProduto = "CCB", temFgi = false }
        });
        res.IsSuccessStatusCode.Should()
           .BeTrue($"seed contrato falhou ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");

        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
               .GetProperty("id").GetGuid();
    }

    // ── Teste 9: ano corrente com contrato → retorna snapshot + projeção ──────

    /// <summary>
    /// Cria banco e contrato, chama o endpoint com o ano corrente e valida que:
    ///   - retorna 200 OK com JSON bem formado;
    ///   - <c>snapshotInicial.saldoTotalBrl</c> > 0;
    ///   - <c>projecao.meses</c> tem exatamente 12 entradas;
    ///   - <c>projecao.meses[0].saldoTotalInicio == snapshotInicial.saldoTotalBrl</c> (invariante AD-8).
    /// </summary>
    [Fact]
    public async Task GetQuadroDivida_AnoCorrente_RetornaSnapshotProjecaoSumario()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client, "200", "QuadroBancoE2E1");
        await CriarContratoBrlAsync(client, bancoId, 3_000_000m);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/painel/quadro-divida?ano=2026");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // Estrutura básica
        body.GetProperty("ano").GetInt32().Should().Be(2026);

        JsonElement snapshot = body.GetProperty("snapshotInicial");
        decimal saldoTotal = snapshot.GetProperty("saldoTotalBrl").GetDecimal();
        saldoTotal.Should().BeGreaterThan(0m, "o contrato sedeado deve aparecer no snapshot");

        // 12 meses
        JsonElement meses = body.GetProperty("projecao").GetProperty("meses");
        meses.GetArrayLength().Should().Be(12);

        // Invariante AD-8: snapshotInicial.saldoTotalBrl == projecao.meses[0].saldoTotalInicio
        decimal saldoMesInicio = meses[0].GetProperty("saldoTotalInicio").GetDecimal();
        saldoMesInicio.Should().Be(saldoTotal,
            "snapshotInicial.saldoTotalBrl deve ser igual ao início do primeiro mês projetado (AD-8)");

        // Sumário
        JsonElement sumario = body.GetProperty("sumario");
        sumario.GetProperty("saldoTotalInicioAno").GetDecimal().Should().Be(saldoTotal);

        // Alertas vazio (Task 3.4)
        body.GetProperty("alertas").GetArrayLength().Should().Be(0);
    }

    // ── Teste 10: sem parâmetro ano → usa ano corrente ────────────────────────

    /// <summary>
    /// Chama o endpoint sem o parâmetro <c>ano</c> e verifica que o campo <c>ano</c>
    /// no corpo da resposta é o ano corrente do clock fixo da fixture (2026).
    /// </summary>
    [Fact]
    public async Task GetQuadroDivida_SemAno_UsaAnoCorrenteAtual()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        // Act — sem parâmetro ano
        HttpResponseMessage response = await client.GetAsync("/api/v1/painel/quadro-divida");

        // Assert — 200 OK com ano = 2026 (ano do clock fixo da fixture)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("ano").GetInt32().Should().Be(2026,
            "sem parâmetro 'ano' o endpoint deve usar o ano corrente do clock (2026)");

        // 12 meses
        body.GetProperty("projecao").GetProperty("meses").GetArrayLength().Should().Be(12);
    }

    // ── Teste 11: ano inválido (fora 2020–2050) → HTTP 400 ───────────────────

    /// <summary>
    /// Verifica que anos fora do intervalo válido 2020–2050 retornam 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetQuadroDivida_AnoInvalido_Retorna400()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        // Act — ano muito no passado
        HttpResponseMessage r1 = await client.GetAsync("/api/v1/painel/quadro-divida?ano=1999");
        r1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Act — ano muito no futuro
        HttpResponseMessage r2 = await client.GetAsync("/api/v1/painel/quadro-divida?ano=2099");
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Teste 12: (Fase 3) cenarioId válido → retorna cenarioAplicado ─────────

    /// <summary>
    /// Cria cenário com uma simulação de 400k em julho de 2026.
    /// Valida que o endpoint retorna cenarioAplicado populado e que julho
    /// apresenta totalCaptacaoMes &gt; 0.
    /// </summary>
    [Fact]
    public async Task GetQuadroDivida_ComCenarioId_RetornaCenarioAplicado()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await QuadroDividaSeedHelper.CriarBancoAsync(client, "999", "BancoCenarioE2E");
        Guid cenarioId = await QuadroDividaSeedHelper.CriarCenarioAsync(client, 2026);
        await QuadroDividaSeedHelper.AdicionarSimulacaoAsync(
            client,
            cenarioId,
            bancoId,
            valorPrincipal: 400_000m,
            dataContratacao: "2026-07-01",
            dataPrimeiroVencimento: "2027-07-01");

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/quadro-divida?ano=2026&cenarioId={cenarioId}");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"resposta foi {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // cenarioAplicado deve estar presente
        body.TryGetProperty("cenarioAplicado", out JsonElement cenarioAplicado).Should().BeTrue();
        cenarioAplicado.GetProperty("id").GetGuid().Should().Be(cenarioId);
        cenarioAplicado.GetProperty("quantidadeSimulacoes").GetInt32().Should().Be(1);

        // Julho (índice 6) deve ter captação
        JsonElement julho = body.GetProperty("projecao").GetProperty("meses")[6];
        julho.GetProperty("totalCaptacaoMes").GetDecimal().Should().BeGreaterThan(0m,
            "a simulação de 400k em julho deve elevar o totalCaptacaoMes");
    }

    // ── Teste 13: (Fase 3) cenarioId inexistente → HTTP 404 ──────────────────

    /// <summary>
    /// Verifica que um cenarioId que não existe retorna 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetQuadroDivida_CenarioIdInexistente_Retorna404()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid cenarioIdFake = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/quadro-divida?ano=2026&cenarioId={cenarioIdFake}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
