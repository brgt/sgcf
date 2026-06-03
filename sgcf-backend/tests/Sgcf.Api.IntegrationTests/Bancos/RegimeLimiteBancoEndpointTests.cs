using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Sgcf.Api.IntegrationTests.LimitesBanco;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Bancos;

/// <summary>
/// Testes de integração HTTP para PUT /api/v1/bancos/{id}/regime-limite e a coerência REG-01/REG-02.
/// SPEC_REGIME_LIMITE_EXPLICITO §5. Reutiliza a fixture da coleção de Limites de Banco.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class RegimeLimiteBancoEndpointTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco Regime {codigoCompe} S.A.",
            apelido = $"BR{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> DefinirRegimeAsync(HttpClient client, Guid bancoId, string regime) =>
        client.PutAsJsonAsync($"/api/v1/bancos/{bancoId}/regime-limite", new { regimeLimite = regime });

    private static Task<HttpResponseMessage> CriarLimiteBancoAsync(HttpClient client, Guid bancoId) =>
        client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 1_000_000m,
            dataVigenciaInicio = "2099-01-01",
        });

    // ─── Caminho feliz ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DefinirRegime_GlobalPuro_SemLimiteBanco_Retorna200()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "RA1");

        HttpResponseMessage res = await DefinirRegimeAsync(client, bancoId, "GlobalPuro");

        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("regimeLimite").GetString().Should().Be("GlobalPuro");
    }

    // ─── REG-02: migrar para GlobalPuro com LimiteBanco ativo → 409 ───────────────

    [Fact]
    public async Task DefinirRegime_GlobalPuro_ComLimiteBancoAtivo_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "RA2");

        HttpResponseMessage limiteRes = await CriarLimiteBancoAsync(client, bancoId);
        limiteRes.StatusCode.Should().Be(HttpStatusCode.Created, await limiteRes.Content.ReadAsStringAsync());

        HttpResponseMessage res = await DefinirRegimeAsync(client, bancoId, "GlobalPuro");

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "REG-02: não é possível migrar para GlobalPuro com LimiteBanco ativo");
        (await res.Content.ReadAsStringAsync()).Should().Contain("[REG-02]");
    }

    // ─── REG-01 (E2E): criar LimiteBanco em banco GlobalPuro → 409 ────────────────

    [Fact]
    public async Task CriarLimiteBanco_EmBancoGlobalPuro_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "RA3");

        HttpResponseMessage regimeRes = await DefinirRegimeAsync(client, bancoId, "GlobalPuro");
        regimeRes.StatusCode.Should().Be(HttpStatusCode.OK, await regimeRes.Content.ReadAsStringAsync());

        HttpResponseMessage res = await CriarLimiteBancoAsync(client, bancoId);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "REG-01: banco GlobalPuro não admite LimiteBanco por modalidade");
        (await res.Content.ReadAsStringAsync()).Should().Contain("[REG-01]");
    }

    // ─── Regime inválido → 400 ────────────────────────────────────────────────────

    [Fact]
    public async Task DefinirRegime_ValorInvalido_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "RA4");

        HttpResponseMessage res = await DefinirRegimeAsync(client, bancoId, "RegimeInexistente");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
