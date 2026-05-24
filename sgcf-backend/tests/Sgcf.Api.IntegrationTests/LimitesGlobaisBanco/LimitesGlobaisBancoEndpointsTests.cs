using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesGlobaisBanco;

/// <summary>
/// Testes de integração HTTP para os 6 endpoints de LimiteGlobalBanco:
/// 5 em LimitesGlobaisBancoController + 1 GET /bancos/{id}/limite-global-vigente.
/// Cada cenário cria seu próprio banco (codigoCompe único) para isolamento total.
/// </summary>
[Collection("LimitesGlobaisBancoApi")]
[Trait("Category", "Slow")]
public sealed class LimitesGlobaisBancoEndpointsTests(LimitesGlobaisBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um banco exclusivo para o cenário e retorna seu id.
    /// codigoCompe deve ter exatamente 3 caracteres (validação do domínio).
    /// </summary>
    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco Global {codigoCompe} S.A.",
            apelido = $"BGL{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Envia POST /api/v1/limites-globais-banco e retorna (response, rawBody, parsedBody).
    /// </summary>
    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> CriarLimiteGlobalAsync(
        HttpClient client,
        Guid bancoId,
        string inicio = "2099-01-01",
        string? fim = null,
        decimal valorLimiteBrl = 10_000_000m,
        string? observacoes = null)
    {
        object payload = fim is null
            ? new { bancoId, valorLimiteBrl, dataVigenciaInicio = inicio, observacoes }
            : new { bancoId, valorLimiteBrl, dataVigenciaInicio = inicio, dataVigenciaFim = fim, observacoes };

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-globais-banco", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> ObterPorIdAsync(
        HttpClient client, Guid id)
    {
        HttpResponseMessage res = await client.GetAsync($"/api/v1/limites-globais-banco/{id}");
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    // ─── Cenário 1: POST cria limite vigente → 201 + Location + dto ──────────

    [Fact]
    public async Task CriarLimiteGlobal_DadosValidos_Retorna201ComLocationEDto()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G01");

        (HttpResponseMessage res, string raw, JsonElement body) = await CriarLimiteGlobalAsync(
            client, bancoId, inicio: "2099-01-01", valorLimiteBrl: 15_000_000m,
            observacoes: "Limite inicial teste");

        res.StatusCode.Should().Be(HttpStatusCode.Created, raw);
        res.Headers.Location.Should().NotBeNull("deve retornar Location header apontando para o recurso criado");

        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("bancoId").GetGuid().Should().Be(bancoId);
        body.GetProperty("valorLimiteBrl").GetDecimal().Should().Be(15_000_000m);
        body.GetProperty("dataVigenciaInicio").GetString().Should().Be("2099-01-01");
        body.TryGetProperty("dataVigenciaFim", out JsonElement fimEl).Should().BeTrue();
        fimEl.ValueKind.Should().Be(JsonValueKind.Null, "dataVigenciaFim deve ser null quando não informado");
        body.GetProperty("historico").GetArrayLength().Should().BeGreaterThanOrEqualTo(1,
            "criação registra entrada inicial no histórico");

        // Bonus: GET /bancos/{id}/limite-global-vigente deve retornar o limite recém-criado
        HttpResponseMessage vigenteRes = await client.GetAsync($"/api/v1/bancos/{bancoId}/limite-global-vigente");
        vigenteRes.StatusCode.Should().Be(HttpStatusCode.OK, "limite recém-criado deve aparecer como vigente");
        JsonElement vigenteBody = await vigenteRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        vigenteBody.GetProperty("bancoId").GetGuid().Should().Be(bancoId);
        vigenteBody.GetProperty("valorLimiteBrl").GetDecimal().Should().Be(15_000_000m);
    }

    // ─── Cenário 2: POST duplicado (LG-05) → 409 ─────────────────────────────

    [Fact]
    public async Task CriarLimiteGlobal_VigenciaSobreposta_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G02");

        // Primeiro limite: período aberto (sem dataVigenciaFim)
        (HttpResponseMessage primeiro, string primeiroRaw, _) = await CriarLimiteGlobalAsync(
            client, bancoId, inicio: "2099-01-01");
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created,
            $"primeiro limite deve ser aceito — corpo: {primeiroRaw}");

        // Segundo limite: mesmo banco, período sobreposto → deve ser rejeitado (LG-05)
        (HttpResponseMessage segundo, string segundoRaw, _) = await CriarLimiteGlobalAsync(
            client, bancoId, inicio: "2099-06-01");
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"vigência sobreposta para o mesmo banco deve retornar 409 (LG-05) — corpo: {segundoRaw}");
    }

    // ─── Cenário 3: GET lista retorna ao menos 1 item ─────────────────────────

    [Fact]
    public async Task ListarLimitesGlobais_QuandoExistemRegistros_Retorna200ComItens()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G03");

        (HttpResponseMessage postRes, string postRaw, JsonElement postBody) = await CriarLimiteGlobalAsync(
            client, bancoId, inicio: "2099-02-01");
        postRes.StatusCode.Should().Be(HttpStatusCode.Created, postRaw);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        // GET sem filtro deve retornar ao menos o limite criado
        HttpResponseMessage listRes = await client.GetAsync("/api/v1/limites-globais-banco");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement listBody = await listRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        listBody.GetArrayLength().Should().BeGreaterThanOrEqualTo(1,
            "lista deve conter ao menos o limite criado neste cenário");

        // GET com filtro por bancoId deve retornar exatamente o limite deste banco
        HttpResponseMessage filtradoRes = await client.GetAsync(
            $"/api/v1/limites-globais-banco?bancoId={bancoId}");
        filtradoRes.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement filtradoBody = await filtradoRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        filtradoBody.GetArrayLength().Should().Be(1, "filtro por bancoId deve retornar apenas o limite deste banco");
        filtradoBody[0].GetProperty("id").GetGuid().Should().Be(limiteId);
    }

    // ─── Cenário 4: GET by id retorna dto correto ─────────────────────────────

    [Fact]
    public async Task GetPorId_LimiteExistente_Retorna200ComDtoCorreto()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G04");

        (HttpResponseMessage postRes, string postRaw, JsonElement postBody) = await CriarLimiteGlobalAsync(
            client, bancoId, inicio: "2099-03-01", fim: "2099-12-31",
            valorLimiteBrl: 8_000_000m, observacoes: "Limite G04");
        postRes.StatusCode.Should().Be(HttpStatusCode.Created, postRaw);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        (HttpResponseMessage getRes, string getRaw, JsonElement getBody) = await ObterPorIdAsync(client, limiteId);

        getRes.StatusCode.Should().Be(HttpStatusCode.OK, $"GET /{limiteId} — corpo: {getRaw}");
        getBody.GetProperty("id").GetGuid().Should().Be(limiteId);
        getBody.GetProperty("bancoId").GetGuid().Should().Be(bancoId);
        getBody.GetProperty("valorLimiteBrl").GetDecimal().Should().Be(8_000_000m);
        getBody.GetProperty("dataVigenciaInicio").GetString().Should().Be("2099-03-01");
        getBody.GetProperty("dataVigenciaFim").GetString().Should().Be("2099-12-31");
        getBody.GetProperty("observacoes").GetString().Should().Be("Limite G04");
        getBody.GetProperty("historico").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    // ─── Cenário 5: GET by id com id desconhecido → 404 ──────────────────────

    [Fact]
    public async Task GetPorId_IdDesconhecido_Retorna404()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid idInexistente = Guid.NewGuid();

        HttpResponseMessage res = await client.GetAsync($"/api/v1/limites-globais-banco/{idInexistente}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "id inexistente deve retornar 404");
    }

    // ─── Cenário 6: DELETE /vigencia encerra limite → 204, vigente vira 404 ───

    [Fact]
    public async Task EncerrarVigencia_LimiteVigente_Retorna204EVigenteDesaparece()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "G06");

        // Cria limite com início no passado para que seja considerado vigente no instante fixo
        (HttpResponseMessage postRes, string postRaw, JsonElement postBody) = await CriarLimiteGlobalAsync(
            client, bancoId, inicio: "2026-01-01", valorLimiteBrl: 5_000_000m);
        postRes.StatusCode.Should().Be(HttpStatusCode.Created, postRaw);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        // Confirma que está vigente antes do encerramento
        HttpResponseMessage vigenteAntes = await client.GetAsync($"/api/v1/bancos/{bancoId}/limite-global-vigente");
        vigenteAntes.StatusCode.Should().Be(HttpStatusCode.OK,
            "limite deve aparecer como vigente antes do encerramento");

        // DELETE /vigencia: encerra com dataFim = data atual (instante fixo = 2026-05-16)
        HttpResponseMessage deleteRes = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/limites-globais-banco/{limiteId}/vigencia")
        {
            Content = JsonContent.Create(new { dataFim = "2026-05-16" })
        });

        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"encerramento de vigência válido deve retornar 204 — corpo: {await deleteRes.Content.ReadAsStringAsync()}");

        // Após encerramento, GET vigente deve retornar 404 (sem limite aberto para este banco)
        HttpResponseMessage vigenteDepois = await client.GetAsync($"/api/v1/bancos/{bancoId}/limite-global-vigente");
        vigenteDepois.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "após encerrar vigência, banco não deve ter limite vigente");
    }
}
