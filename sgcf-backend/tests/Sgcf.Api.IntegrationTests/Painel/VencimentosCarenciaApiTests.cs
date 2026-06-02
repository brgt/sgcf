using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Painel;

/// <summary>
/// Prove-It: bug 2026-05-18 — parcelas de carência de contrato CDI-indexado
/// retornavam jurosBrlProjetado = null quando cdiAnualPct não era informado.
///
/// Estes testes fariam FALHAR contra o código original e PASSAM após a correção.
/// </summary>
[Collection("PainelVencimentosApi")]
[Trait("Category", "Slow")]
public sealed class VencimentosCarenciaApiTests(PainelVencimentosApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers de seed ──────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client)
    {
        string codigo = TestBancoCodigo.Next();
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco CCB Teste {codigo} S.A.",
            apelido = $"CCB{codigo}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarContratoCdiAsync(HttpClient client, Guid bancoId)
    {
        // CCB Balcão Caixa indexado a CDI: taxaAa = 3,17 representa o spread
        // (0,26% a.m. capitalizado anualmente ≈ 3,17% a.a.)
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/contratos", new
        {
            numeroExterno = $"CEF-CCB-E2E-{Guid.NewGuid():N}",
            bancoId,
            modalidade = "CapitalDeGiro",
            moeda = "Brl",
            valorPrincipal = 5_000_000m,
            dataContratacao = "2026-02-26",
            dataVencimento = "2029-02-26",
            taxaAa = 3.17m,
            baseCalculo = "Dias252",
            contratoPaiId = (Guid?)null,
            observacoes = "CCB 100% CDI + 0,26% a.m. — 36 meses (6 carência + 30 amortização)",
            periodicidade = "Bullet",
            estruturaAmortizacao = "Bullet",
            quantidadeParcelas = 1,
            dataPrimeiroVencimento = "2029-02-26",
            capitalDeGiroDetail = new { numeroOperacao = (string?)null, tipoProduto = "CCB", temFgi = false }
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed contrato falhou: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    private static async Task ImportarCronogramaCarenciaAsync(HttpClient client, Guid contratoId)
    {
        // 6 parcelas de Juros com valor 0 simulando carência CDI (Mar-Ago/2026)
        // Principal = 0 durante carência; o bullet final (fev/2029) não está no ano 2026.
        var parcelas = new[]
        {
            new { dataVencimento = "2026-03-26", valorPrincipal = 0m, valorJuros = 0m },
            new { dataVencimento = "2026-04-26", valorPrincipal = 0m, valorJuros = 0m },
            new { dataVencimento = "2026-05-26", valorPrincipal = 0m, valorJuros = 0m },
            new { dataVencimento = "2026-06-26", valorPrincipal = 0m, valorJuros = 0m },
            new { dataVencimento = "2026-07-26", valorPrincipal = 0m, valorJuros = 0m },
            new { dataVencimento = "2026-08-26", valorPrincipal = 0m, valorJuros = 0m },
        };

        HttpResponseMessage res = await client.PostAsJsonAsync(
            $"/api/v1/contratos/{contratoId}/importar-cronograma", parcelas);
        res.IsSuccessStatusCode.Should().BeTrue($"import cronograma falhou: {await res.Content.ReadAsStringAsync()}");
    }

    private static async Task SeedCdiSnapshotAsync(HttpClient client, decimal cdiAaPercentual = 10.75m)
    {
        // Ignora 409 (snapshot já existe de outro teste na mesma fixture)
        await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-18",
            cdiAaPercentual
        });
    }

    // ── testes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Prove-It — bug principal: sem cdiAnualPct na query, as parcelas de carência
    /// CDI-indexada devem usar o snapshot mais recente e retornar jurosBrlProjetado > 0.
    ///
    /// ANTES da correção: jurosBrlProjetado = null para todos os meses de carência.
    /// APÓS  a correção:  jurosBrlProjetado > 0 para todos os meses de carência.
    /// </summary>
    [Fact]
    public async Task GetVencimentos_ContratoCdiComCarencia_SemCdiNaQuery_RetornaJurosProjetados()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        Guid contratoId = await CriarContratoCdiAsync(client, bancoId);
        await ImportarCronogramaCarenciaAsync(client, contratoId);
        await SeedCdiSnapshotAsync(client, cdiAaPercentual: 10.75m);

        // Act — chamada SEM cdiAnualPct (exatamente como o frontend faz hoje).
        // Filtra por bancoId para isolar dos contratos criados pelos outros testes.
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/vencimentos?ano=2026&bancoId={bancoId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("ano").GetInt32().Should().Be(2026);
        body.GetProperty("taxaCdiUsadaPct").GetDecimal().Should().Be(10.75m,
            "o handler deve auto-usar o snapshot de CDI quando não informado na query");

        int[] mesesCarencia = [3, 4, 5, 6, 7, 8];
        JsonElement meses = body.GetProperty("meses");

        foreach (int numeroMes in mesesCarencia)
        {
            JsonElement mes = meses.EnumerateArray()
                .Single(m => m.GetProperty("mes").GetInt32() == numeroMes);

            mes.GetProperty("quantidadeParcelas").GetInt32().Should().Be(1,
                $"mês {numeroMes} deve ter exatamente 1 parcela de carência para este banco");

            // jurosBrl = 0 é esperado (valor realizado CDI ainda não está calculado)
            mes.GetProperty("totalJurosBrl").GetDecimal().Should().Be(0m,
                $"mês {numeroMes}: juros realizados devem ser 0 durante carência CDI");

            // totalJurosBrlProjetado deve ser positivo — esse era o bug
            mes.TryGetProperty("totalJurosBrlProjetado", out JsonElement totalProjetadoEl).Should().BeTrue(
                $"mês {numeroMes}: resposta deve ter campo totalJurosBrlProjetado");

            decimal totalProjetado = totalProjetadoEl.ValueKind == JsonValueKind.Null
                ? 0m
                : totalProjetadoEl.GetDecimal();

            totalProjetado.Should().BeGreaterThan(0m,
                $"mês {numeroMes}: projeção de juros CDI sobre R$5M deve ser positiva (era null antes do fix)");

            // Verifica o nível de parcela individual também
            JsonElement parcela = mes.GetProperty("parcelas").EnumerateArray().First();

            parcela.GetProperty("jurosBrl").GetDecimal().Should().Be(0m);

            parcela.TryGetProperty("jurosBrlProjetado", out JsonElement jProjetadoEl).Should().BeTrue(
                $"parcela de março deve ter jurosBrlProjetado");

            decimal jProjetado = jProjetadoEl.ValueKind == JsonValueKind.Null
                ? 0m
                : jProjetadoEl.GetDecimal();

            jProjetado.Should().BeGreaterThan(0m,
                $"jurosBrlProjetado da parcela no mês {numeroMes} deve ser > 0");
        }
    }

    /// <summary>
    /// Regressão: quando cdiAnualPct é informado explicitamente, o comportamento
    /// existente (usar a taxa do caller) deve ser preservado.
    /// </summary>
    [Fact]
    public async Task GetVencimentos_ComCdiExplicito_UsaTaxaInformada()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        Guid contratoId = await CriarContratoCdiAsync(client, bancoId);
        await ImportarCronogramaCarenciaAsync(client, contratoId);
        await SeedCdiSnapshotAsync(client, cdiAaPercentual: 10.75m);

        // Act — caller informa CDI = 12% explicitamente (diferente do snapshot).
        // Filtra por bancoId para isolar dos contratos de outros testes.
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/vencimentos?ano=2026&bancoId={bancoId}&cdiAnualPct=12.00");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("taxaCdiUsadaPct").GetDecimal().Should().Be(12.00m,
            "quando cdiAnualPct é informado explicitamente, a taxa do caller deve prevalecer sobre o snapshot");
    }

    /// <summary>
    /// Regressão: contrato pré-fixado (jurosBrl já calculado) não deve ter
    /// jurosBrlProjetado — o campo deve permanecer null para não poluir a tela.
    /// </summary>
    [Fact]
    public async Task GetVencimentos_ContratoPrefixado_SemProjecao()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        Guid contratoId = await CriarContratoCdiAsync(client, bancoId);
        await SeedCdiSnapshotAsync(client);

        // Importa parcela com juros realizados (pré-fixado — valor não-zero)
        var parcelaRealizada = new[]
        {
            new { dataVencimento = "2026-09-26", valorPrincipal = 0m, valorJuros = 42_500m }
        };
        HttpResponseMessage importRes = await client.PostAsJsonAsync(
            $"/api/v1/contratos/{contratoId}/importar-cronograma", parcelaRealizada);
        importRes.IsSuccessStatusCode.Should().BeTrue();

        // Act — filtra por bancoId para isolar dos contratos de outros testes.
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/vencimentos?ano=2026&bancoId={bancoId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        JsonElement mes9 = body.GetProperty("meses").EnumerateArray()
            .Single(m => m.GetProperty("mes").GetInt32() == 9);

        mes9.GetProperty("totalJurosBrl").GetDecimal().Should().Be(42_500m,
            "juros pré-fixados devem aparecer em jurosBrl, não em projetado");

        // totalJurosBrlProjetado deve ser null — eventos com valor != 0 não geram projeção
        if (mes9.TryGetProperty("totalJurosBrlProjetado", out JsonElement projetadoEl)
            && projetadoEl.ValueKind != JsonValueKind.Null)
        {
            projetadoEl.GetDecimal().Should().Be(0m,
                "evento de juros com valor realizado não deve gerar projeção CDI");
        }
    }
}
