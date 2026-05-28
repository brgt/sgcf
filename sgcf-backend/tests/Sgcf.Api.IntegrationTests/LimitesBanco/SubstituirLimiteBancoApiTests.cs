using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Testes de integração para RV-02: POST /limites-banco/{id}/substituir.
/// Verifica a substituição atômica — encerramento do anterior e criação do sucessor.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class SubstituirLimiteBancoApiTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco S{codigoCompe} S.A.",
            apelido = $"BS{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> CriarLimiteAsync(
        HttpClient client,
        Guid bancoId,
        string inicio = "2099-01-01",
        string? fim = null,
        decimal valor = 10_000_000m,
        string modalidade = "Finimp")
    {
        object payload = fim is null
            ? new { bancoId, modalidade, valorLimiteBrl = valor, dataVigenciaInicio = inicio }
            : new { bancoId, modalidade, valorLimiteBrl = valor, dataVigenciaInicio = inicio, dataVigenciaFim = fim };

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> SubstituirLimiteAsync(
        HttpClient client,
        Guid limiteId,
        object payload)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync($"/api/v1/limites-banco/{limiteId}/substituir", payload);
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

    // ─── S01: substituição bem-sucedida → 201 + Location correto ─────────────

    [Fact]
    public async Task Substituir_Sucesso_Retorna201ComSucessorELocationCorreto()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "S01");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid anteriorId = criado.GetProperty("id").GetGuid();

        (HttpResponseMessage res, string raw, JsonElement body) = await SubstituirLimiteAsync(client, anteriorId,
            new { novoInicio = "2100-01-01", novoValorLimiteBrl = 15_000_000m });

        res.StatusCode.Should().Be(HttpStatusCode.Created, raw);
        res.Headers.Location.Should().NotBeNull("Location header deve apontar para o sucessor");

        // Successor DTO
        body.GetProperty("dataVigenciaInicio").GetString().Should().Be("2100-01-01");
        body.GetProperty("valorLimiteBrl").GetDecimal().Should().Be(15_000_000m);
        body.GetProperty("bancoId").GetGuid().Should().Be(bancoId);

        // Successor id deve ser diferente do anterior
        Guid sucessorId = body.GetProperty("id").GetGuid();
        sucessorId.Should().NotBe(anteriorId);
    }

    // ─── S02: anterior tem dataVigenciaFim = novoInicio - 1 dia ──────────────

    [Fact]
    public async Task Substituir_Sucesso_AnteriorRecebeDataVigenciaFim()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "S02");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid anteriorId = criado.GetProperty("id").GetGuid();

        await SubstituirLimiteAsync(client, anteriorId,
            new { novoInicio = "2100-06-01", novoValorLimiteBrl = 12_000_000m });

        // GET anterior — deve ter dataVigenciaFim = 2100-05-31
        (HttpResponseMessage getRes, string getRaw, JsonElement getBody) = await ObterLimiteAsync(client, anteriorId);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK, getRaw);
        getBody.GetProperty("dataVigenciaFim").GetString().Should().Be("2100-05-31",
            "anterior deve ser encerrado no dia anterior ao novoInicio");
    }

    // ─── S03: motivoEncerramento é persistido no anterior ────────────────────

    [Fact]
    public async Task Substituir_ComMotivoEncerramento_EhPersistidoNoAnterior()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "S03");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid anteriorId = criado.GetProperty("id").GetGuid();

        const string motivo = "Renovação anual — comitê mai/2026";
        await SubstituirLimiteAsync(client, anteriorId,
            new { novoInicio = "2100-01-01", novoValorLimiteBrl = 10_000_000m, motivoEncerramento = motivo });

        (_, string getRaw, JsonElement getBody) = await ObterLimiteAsync(client, anteriorId);
        getBody.GetProperty("motivoEncerramento").GetString()
            .Should().Be(motivo, getRaw);
    }

    // ─── S04: novoInicio <= início do anterior → 400 ─────────────────────────

    [Fact]
    public async Task Substituir_NovoInicioAnteriorAoExistente_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "S04");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-06-01");
        Guid anteriorId = criado.GetProperty("id").GetGuid();

        (HttpResponseMessage res, string raw, _) = await SubstituirLimiteAsync(client, anteriorId,
            new { novoInicio = "2099-01-01", novoValorLimiteBrl = 10_000_000m });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, raw);
    }

    // ─── S05: sucessor sobrepoõe outro limite existente → 409 ────────────────

    [Fact]
    public async Task Substituir_SucessorSobrepoeOutroLimite_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "S05");

        // Limite A: 2099-01-01 a 2099-12-31
        (_, _, JsonElement limiteA) = await CriarLimiteAsync(client, bancoId, "2099-01-01", "2099-12-31");
        Guid limiteAId = limiteA.GetProperty("id").GetGuid();

        // Limite B: 2100-01-01 a 2100-12-31 (independente)
        (_, _, _) = await CriarLimiteAsync(client, bancoId, "2100-01-01", "2100-12-31");

        // Tentar substituir A com novoInicio 2100-06-01 → sucessor sobrepõe B
        (HttpResponseMessage res, string raw, _) = await SubstituirLimiteAsync(client, limiteAId,
            new { novoInicio = "2100-06-01", novoValorLimiteBrl = 15_000_000m });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict, raw);
    }

    // ─── S06: limite inexistente → 404 ───────────────────────────────────────

    [Fact]
    public async Task Substituir_LimiteInexistente_Retorna404()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        (HttpResponseMessage res, _, _) = await SubstituirLimiteAsync(client, Guid.NewGuid(),
            new { novoInicio = "2100-01-01", novoValorLimiteBrl = 10_000_000m });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── S07: sucessor não herda antecipação do anterior ─────────────────────

    [Fact]
    public async Task Substituir_SucessorNaoHerdaAntecipacao()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "S07");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid anteriorId = criado.GetProperty("id").GetGuid();

        // Configurar antecipação no anterior via PATCH
        await client.PatchAsJsonAsync($"/api/v1/limites-banco/{anteriorId}", new
        {
            configurarAntecipacao = true,
            padraoAntecipacao = "A",
            breakFundingFeePct = 0.5m
        });

        // Substituir
        (HttpResponseMessage res, string raw, JsonElement body) = await SubstituirLimiteAsync(client, anteriorId,
            new { novoInicio = "2100-01-01", novoValorLimiteBrl = 10_000_000m });

        res.StatusCode.Should().Be(HttpStatusCode.Created, raw);

        // Sucessor não deve ter antecipação configurada
        bool hasPadrao = body.TryGetProperty("padraoAntecipacao", out JsonElement padraoEl)
            && padraoEl.ValueKind != JsonValueKind.Null;
        hasPadrao.Should().BeFalse("sucessor não deve herdar antecipação do anterior");
    }
}
