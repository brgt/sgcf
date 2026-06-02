using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Testes de integração para o sub-recurso de garantias exigidas em LimiteBanco.
/// Cada teste usa seu próprio banco (codigoCompe único) para isolamento.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class LimitesBancoGarantiasTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco G{codigoCompe} S.A.",
            apelido = $"BG{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Cria um limite e retorna (statusCode, rawBody, parsedBody).
    /// Reads the stream once into rawBody, then parses — safe for diagnostics.
    /// </summary>
    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> CriarLimiteAsync(
        HttpClient client,
        Guid bancoId,
        string modalidade = "Finimp",
        string inicio = "2099-01-01",
        object[]? garantiasExigidas = null)
    {
        object payload = garantiasExigidas is null
            ? new { bancoId, modalidade, valorLimiteBrl = 5_000_000m, dataVigenciaInicio = inicio }
            : new { bancoId, modalidade, valorLimiteBrl = 5_000_000m, dataVigenciaInicio = inicio, garantiasExigidas };

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> AtualizarLimiteAsync(
        HttpClient client,
        Guid limiteId,
        object[]? garantiasExigidas)
    {
        object payload = garantiasExigidas is null
            ? new { novoValorLimiteBrl = (decimal?)null! }
            : (object)new { garantiasExigidas };

        HttpResponseMessage res = await client.PatchAsJsonAsync($"/api/v1/limites-banco/{limiteId}", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> ObterLimiteAsync(
        HttpClient client, Guid limiteId)
    {
        HttpResponseMessage res = await client.GetAsync($"/api/v1/limites-banco/{limiteId}");
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    // ─── Cenário G01: criar com 1 garantia CdbCativo 20% → 201 ───────────────

    [Fact]
    public async Task CriarLimite_ComUmaGarantiaCdbCativo_Retorna201ComGarantia()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G01");

        (HttpResponseMessage res, string raw, JsonElement body) = await CriarLimiteAsync(client, bancoId,
            garantiasExigidas:
            [
                new { tipo = "CdbCativo", percentualSobreLimite = 20m, obrigatoria = true }
            ]);

        res.StatusCode.Should().Be(HttpStatusCode.Created, raw);

        JsonElement garantias = body.GetProperty("garantiasExigidas");
        garantias.GetArrayLength().Should().Be(1);

        JsonElement g = garantias[0];
        g.GetProperty("tipo").GetString().Should().Be("CdbCativo");
        g.GetProperty("percentualSobreLimite").GetDecimal().Should().Be(20m);
        g.GetProperty("obrigatoria").GetBoolean().Should().BeTrue();
    }

    // ─── Cenário G02: criar sem garantias → 201, array vazio ─────────────────

    [Fact]
    public async Task CriarLimite_SemGarantias_Retorna201ComArrayVazio()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G02");

        (HttpResponseMessage res, string raw, JsonElement body) = await CriarLimiteAsync(client, bancoId);

        res.StatusCode.Should().Be(HttpStatusCode.Created, raw);
        body.GetProperty("garantiasExigidas").GetArrayLength().Should().Be(0);
    }

    // ─── Cenário G03: duas garantias do mesmo tipo → 409 ─────────────────────

    [Fact]
    public async Task CriarLimite_DuasGarantiasMesmoTipo_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G03");

        (HttpResponseMessage res, _, _) = await CriarLimiteAsync(client, bancoId,
            garantiasExigidas:
            [
                new { tipo = "CdbCativo", percentualSobreLimite = 20m },
                new { tipo = "CdbCativo", percentualSobreLimite = 30m }
            ]);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "dois itens do mesmo Tipo são duplicatas — domínio rejeita com InvalidOperationException");
    }

    // ─── Cenário G04: Aval com ambos null → 201 ───────────────────────────────

    [Fact]
    public async Task CriarLimite_GarantiaAvalSemValores_Retorna201()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G04");

        (HttpResponseMessage res, string raw, JsonElement body) = await CriarLimiteAsync(client, bancoId,
            garantiasExigidas:
            [
                new { tipo = "Aval", obrigatoria = true }
            ]);

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Aval pode ter ambos percentual e valorFixo nulos (AD-4 relaxada) — corpo: {raw}");

        JsonElement g = body.GetProperty("garantiasExigidas")[0];
        g.GetProperty("tipo").GetString().Should().Be("Aval");
        g.TryGetProperty("percentualSobreLimite", out JsonElement pct).Should().BeTrue();
        pct.ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ─── Cenário G05: CdbCativo com ambos null → domínio rejeita ─────────────

    [Fact]
    public async Task CriarLimite_CdbCativoSemValores_RetornaErro()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G05");

        (HttpResponseMessage res, _, _) = await CriarLimiteAsync(client, bancoId,
            garantiasExigidas:
            [
                new { tipo = "CdbCativo", obrigatoria = true }
            ]);

        // Domain throws ArgumentException → controller maps to 400.
        res.StatusCode.Should().BeOneOf(
            [HttpStatusCode.BadRequest, HttpStatusCode.Conflict],
            "CdbCativo sem percentual e sem valorFixo viola AD-4 — deve ser rejeitado pelo domínio");
    }

    // ─── Cenário G06: PATCH adiciona garantias → aparecem no retorno ──────────

    [Fact]
    public async Task PatchLimite_AdicionaGarantias_GarantiasAparecemNaResposta()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G06");

        (HttpResponseMessage postRes, string postRaw, JsonElement postBody) = await CriarLimiteAsync(
            client, bancoId, inicio: "2099-02-01");
        postRes.StatusCode.Should().Be(HttpStatusCode.Created, postRaw);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        (HttpResponseMessage patchRes, string patchRaw, JsonElement patchBody) = await AtualizarLimiteAsync(
            client, limiteId,
            garantiasExigidas:
            [
                new { tipo = "Sblc", valorFixoBrl = 1_000_000m, obrigatoria = true }
            ]);

        patchRes.StatusCode.Should().Be(HttpStatusCode.OK, patchRaw);
        JsonElement garantias = patchBody.GetProperty("limite").GetProperty("garantiasExigidas");
        garantias.GetArrayLength().Should().Be(1);
        garantias[0].GetProperty("tipo").GetString().Should().Be("Sblc");
    }

    // ─── Cenário G07: PATCH com lista vazia limpa garantias ───────────────────

    [Fact]
    public async Task PatchLimite_ListaVazia_LimpaGarantias()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G07");

        (_, _, JsonElement postBody) = await CriarLimiteAsync(client, bancoId, inicio: "2099-03-01",
            garantiasExigidas:
            [
                new { tipo = "Fgi", percentualSobreLimite = 10m }
            ]);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        (HttpResponseMessage patchRes, string patchRaw, JsonElement patchBody) = await AtualizarLimiteAsync(
            client, limiteId, garantiasExigidas: []);

        patchRes.StatusCode.Should().Be(HttpStatusCode.OK, patchRaw);
        patchBody.GetProperty("limite").GetProperty("garantiasExigidas").GetArrayLength().Should().Be(0,
            "lista vazia deve remover todas as garantias existentes");
    }

    // ─── Cenário G08: PATCH sem campo garantias preserva existentes ───────────

    [Fact]
    public async Task PatchLimite_SemCampoGarantias_PreservaGarantiasExistentes()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G08");

        (_, _, JsonElement postBody) = await CriarLimiteAsync(client, bancoId, inicio: "2099-04-01",
            garantiasExigidas:
            [
                new { tipo = "AlienacaoFiduciaria", percentualSobreLimite = 80m }
            ]);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        // PATCH only updates the valor — garantias field is absent from payload.
        HttpResponseMessage patchRes = await client.PatchAsJsonAsync(
            $"/api/v1/limites-banco/{limiteId}",
            new { novoValorLimiteBrl = 6_000_000m });
        string patchRaw = await patchRes.Content.ReadAsStringAsync();

        patchRes.StatusCode.Should().Be(HttpStatusCode.OK,
            $"PATCH com apenas novoValorLimiteBrl deve retornar 200 — corpo: {patchRaw}");

        JsonElement patchBody = JsonSerializer.Deserialize<JsonElement>(patchRaw, JsonOpts);
        patchBody.GetProperty("limite").GetProperty("garantiasExigidas").GetArrayLength().Should().Be(1,
            "garantias não enviadas no PATCH devem ser preservadas");
        patchBody.GetProperty("limite").GetProperty("garantiasExigidas")[0]
            .GetProperty("tipo").GetString().Should().Be("AlienacaoFiduciaria");
    }

    // ─── Cenário G09: GET /api/v1/limites-banco/{id} retorna garantias e histórico ──

    [Fact]
    public async Task GetById_RetornaGarantiasEHistorico()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G09");

        (_, _, JsonElement postBody) = await CriarLimiteAsync(client, bancoId, inicio: "2099-05-01",
            garantiasExigidas:
            [
                new { tipo = "Duplicatas", percentualSobreLimite = 80m }
            ]);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        (HttpResponseMessage getRes, string getRaw, JsonElement getBody) = await ObterLimiteAsync(client, limiteId);

        getRes.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /{limiteId} deve retornar 200 — corpo: {getRaw}");
        getBody.GetProperty("garantiasExigidas").GetArrayLength().Should().Be(1);
        getBody.GetProperty("historico").GetArrayLength().Should().BeGreaterThanOrEqualTo(1,
            "a criação do limite gera pelo menos uma entrada no histórico");
    }
}
