using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Painel;

/// <summary>
/// Task 1.3 — AD-10: garante que cada parcela do calendário de vencimentos retorna
/// bancoId e bancoApelido do contrato credor, via HTTP.
/// </summary>
[Collection("PainelVencimentosApi")]
[Trait("Category", "Slow")]
public sealed class CalendarioVencimentosBancoApiTests(PainelVencimentosApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers de seed ──────────────────────────────────────────────────────────

    private static async Task<(Guid BancoId, string Apelido)> CriarBancoAsync(HttpClient client, string sufixo)
    {
        // CodigoCompe deve ter exatamente 3 dígitos
        string codigo = (100 + Math.Abs(sufixo.GetHashCode() % 900)).ToString(CultureInfo.InvariantCulture);
        // Apelido limitado para evitar conflito: prefixo curto + hash
        string apelido = $"Bco{Math.Abs(sufixo.GetHashCode() % 10000):D4}";

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco {sufixo} S.A.",
            apelido,
            padraoAntecipacao = "A"
        });

        res.IsSuccessStatusCode.Should().BeTrue(
            $"seed banco '{apelido}' falhou: {await res.Content.ReadAsStringAsync()}");

        Guid id = (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
        return (id, apelido);
    }

    private static async Task<Guid> CriarContratoAsync(HttpClient client, Guid bancoId, string numero)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/contratos", new
        {
            numeroExterno = numero,
            bancoId,
            modalidade = "CapitalDeGiro",
            moeda = "Brl",
            valorPrincipal = 1_000_000m,
            dataContratacao = "2026-01-02",
            dataVencimento = "2026-10-31",
            taxaAa = 12m,
            baseCalculo = "Dias252",
            contratoPaiId = (Guid?)null,
            observacoes = (string?)null,
            periodicidade = "Bullet",
            estruturaAmortizacao = "Bullet",
            quantidadeParcelas = 1,
            dataPrimeiroVencimento = "2026-10-31",
            capitalDeGiroDetail = new { numeroOperacao = (string?)null, tipoProduto = "CCB", temFgi = false }
        });

        res.IsSuccessStatusCode.Should().BeTrue(
            $"seed contrato '{numero}' falhou: {await res.Content.ReadAsStringAsync()}");

        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    private static async Task ImportarPrincipalAsync(HttpClient client, Guid contratoId, string dataVencimento)
    {
        var parcelas = new[]
        {
            new { dataVencimento, valorPrincipal = 1_000_000m, valorJuros = 100_000m }
        };

        HttpResponseMessage res = await client.PostAsJsonAsync(
            $"/api/v1/contratos/{contratoId}/importar-cronograma", parcelas);

        res.IsSuccessStatusCode.Should().BeTrue(
            $"import cronograma falhou: {await res.Content.ReadAsStringAsync()}");
    }

    // ── testes ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// AD-10 (Task 1.3): a resposta JSON do calendário de vencimentos deve incluir
    /// bancoId e bancoApelido em cada parcela.
    /// </summary>
    [Fact]
    public async Task CalendarioVencimentos_ResponseDto_IncluiBancoIdEBancoApelido()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        (Guid bancoId, string apelido) = await CriarBancoAsync(client, $"T13-{Guid.NewGuid():N}");
        Guid contratoId = await CriarContratoAsync(client, bancoId, $"T13-{Guid.NewGuid():N}");
        await ImportarPrincipalAsync(client, contratoId, "2026-10-31");

        // Act — filtra por bancoId para isolar dos contratos de outros testes
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/vencimentos?ano=2026&bancoId={bancoId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("ano").GetInt32().Should().Be(2026);

        // Localiza o mês com parcelas
        JsonElement? mesComParcela = body.GetProperty("meses").EnumerateArray()
            .FirstOrDefault(m => m.GetProperty("quantidadeParcelas").GetInt32() > 0);

        mesComParcela.Should().NotBeNull("deve haver pelo menos um mês com parcela para este banco");

        JsonElement parcela = mesComParcela!.Value.GetProperty("parcelas").EnumerateArray().First();

        // bancoId deve estar presente e corresponder ao banco criado
        parcela.TryGetProperty("bancoId", out JsonElement bancoIdEl).Should().BeTrue(
            "campo bancoId deve estar presente em cada parcela (AD-10)");
        bancoIdEl.GetGuid().Should().Be(bancoId,
            "bancoId deve corresponder ao banco do contrato");

        // bancoApelido deve estar presente e não vazio
        parcela.TryGetProperty("bancoApelido", out JsonElement bancoApelidoEl).Should().BeTrue(
            "campo bancoApelido deve estar presente em cada parcela (AD-10)");
        bancoApelidoEl.GetString().Should().NotBeNullOrEmpty(
            "bancoApelido deve ser um string não vazio");
    }

    /// <summary>
    /// AD-10 (Task 1.3): dois contratos de bancos diferentes no mesmo mês devem
    /// retornar parcelas com bancoId distintos na resposta.
    /// </summary>
    [Fact]
    public async Task CalendarioVencimentos_DoisBancosDiferentes_ParcelasComBancosDistintos()
    {
        // Arrange
        using HttpClient client = fixture.CreateAuthenticatedClient();

        (Guid bancoId1, string _) = await CriarBancoAsync(client, $"T13A-{Guid.NewGuid():N}");
        (Guid bancoId2, string _) = await CriarBancoAsync(client, $"T13B-{Guid.NewGuid():N}");

        Guid contratoId1 = await CriarContratoAsync(client, bancoId1, $"T13A-{Guid.NewGuid():N}");
        Guid contratoId2 = await CriarContratoAsync(client, bancoId2, $"T13B-{Guid.NewGuid():N}");

        await ImportarPrincipalAsync(client, contratoId1, "2026-11-30");
        await ImportarPrincipalAsync(client, contratoId2, "2026-11-30");

        // Act — busca sem filtro de banco para ver ambos os contratos
        // Nota: outros testes podem ter contratos em novembro — usamos os IDs para filtrar no assert
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/painel/vencimentos?ano=2026");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        JsonElement novembro = body.GetProperty("meses").EnumerateArray()
            .Single(m => m.GetProperty("mes").GetInt32() == 11);

        JsonElement[] parcelas = novembro.GetProperty("parcelas").EnumerateArray().ToArray();
        parcelas.Length.Should().BeGreaterThanOrEqualTo(2,
            "pelo menos 2 parcelas em novembro (os dois contratos desta fixture)");

        // Cada contrato criado deve ter seu bancoId correto
        JsonElement parcela1 = parcelas.First(p =>
            p.GetProperty("contratoId").GetGuid() == contratoId1);
        JsonElement parcela2 = parcelas.First(p =>
            p.GetProperty("contratoId").GetGuid() == contratoId2);

        parcela1.GetProperty("bancoId").GetGuid().Should().Be(bancoId1);
        parcela2.GetProperty("bancoId").GetGuid().Should().Be(bancoId2);

        parcela1.GetProperty("bancoId").GetGuid().Should().NotBe(
            parcela2.GetProperty("bancoId").GetGuid(),
            "bancos distintos devem gerar bancoIds distintos nas parcelas");
    }
}
