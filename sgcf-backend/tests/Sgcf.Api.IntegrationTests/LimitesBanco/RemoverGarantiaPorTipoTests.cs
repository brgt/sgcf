using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Testes HTTP para o endpoint
/// <c>DELETE /api/v1/limites-banco/{id}/garantias-exigidas?tipo=X</c> (T1.4).
/// SPEC §5.1.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class RemoverGarantiaPorTipoTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco Del{codigoCompe} S.A.",
            apelido = $"DEL{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid LimiteId, string Raw)> CriarLimiteAsync(
        HttpClient client, Guid bancoId, string dataInicio, object[] garantiasExigidas)
    {
        object payload = new { bancoId, modalidade = "Finimp", valorLimiteBrl = 5_000_000m, dataVigenciaInicio = dataInicio, garantiasExigidas };
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", payload);
        string raw = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"criação falhou: {raw}");
        Guid id = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts).GetProperty("id").GetGuid();
        return (id, raw);
    }

    private static async Task<JsonElement> GetRevisoesAsync(HttpClient client, Guid limiteId)
    {
        HttpResponseMessage res = await client.GetAsync($"/api/v1/limites-banco/{limiteId}/revisoes-garantias");
        res.IsSuccessStatusCode.Should().BeTrue();
        return JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync(), JsonOpts);
    }

    // ── D01: DELETE com tipo válido → 204, nova revisão sem aquele tipo ───────

    [Fact]
    public async Task Delete_TipoValido_Retorna204EAbreNovaRevisaoSemOTipo()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "D01");

        (Guid limiteId, _) = await CriarLimiteAsync(client, bancoId, "2099-09-01",
        [
            new { tipo = "Aval", obrigatoria = true },
            new { tipo = "CdbCativo", percentualSobreLimite = 20m, obrigatoria = true }
        ]);

        // Act — remove Aval
        HttpResponseMessage delRes = await client.DeleteAsync(
            $"/api/v1/limites-banco/{limiteId}/garantias-exigidas?tipo=Aval");

        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"DELETE com tipo válido deve retornar 204 — corpo: {await delRes.Content.ReadAsStringAsync()}");

        // Verifica via GET /revisoes-garantias que nova revisão existe sem Aval
        JsonElement revisoes = await GetRevisoesAsync(client, limiteId);
        JsonElement listaRevisoes = revisoes.GetProperty("revisoes");
        listaRevisoes.GetArrayLength().Should().Be(2, "DELETE deve criar nova revisão (append-only)");

        // Última revisão (vigente) não deve conter Aval
        JsonElement revisaoVigente = listaRevisoes[1];
        revisaoVigente.GetProperty("vigenciaFim").ValueKind.Should().Be(JsonValueKind.Null);
        revisaoVigente.GetProperty("itens").GetArrayLength().Should().Be(1);
        revisaoVigente.GetProperty("itens")[0].GetProperty("tipo").GetString().Should().Be("CdbCativo");
    }

    // ── D02: DELETE com tipo inválido (não-enum) → 400 ────────────────────────

    [Fact]
    public async Task Delete_TipoInvalido_Retorna400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "D02");

        (Guid limiteId, _) = await CriarLimiteAsync(client, bancoId, "2099-10-01",
        [
            new { tipo = "Aval", obrigatoria = true }
        ]);

        HttpResponseMessage res = await client.DeleteAsync(
            $"/api/v1/limites-banco/{limiteId}/garantias-exigidas?tipo=TipoInexistente");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "tipo não mapeável ao enum TipoGarantia deve resultar em 400");
    }

    // ── D03: DELETE com tipo ausente na revisão vigente → 409 ─────────────────

    [Fact]
    public async Task Delete_TipoAusenteNaRevisao_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "D03");

        // Limite com apenas Aval
        (Guid limiteId, _) = await CriarLimiteAsync(client, bancoId, "2099-11-01",
        [
            new { tipo = "Aval", obrigatoria = true }
        ]);

        // Tenta remover CdbCativo — que não existe
        HttpResponseMessage res = await client.DeleteAsync(
            $"/api/v1/limites-banco/{limiteId}/garantias-exigidas?tipo=CdbCativo");

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "domínio lança InvalidOperationException quando tipo não está na revisão vigente → 409");
    }

    // ── D04: DELETE em limite inexistente → 404 ────────────────────────────────

    [Fact]
    public async Task Delete_LimiteInexistente_Retorna404()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.DeleteAsync(
            $"/api/v1/limites-banco/{Guid.NewGuid()}/garantias-exigidas?tipo=Aval");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── D05: DELETE em limite sem revisão vigente → 409 ───────────────────────

    [Fact]
    public async Task Delete_LimiteSemRevisaoVigente_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "D05");

        // Limite criado sem garantias — não possui revisão vigente
        HttpResponseMessage postRes = await client.PostAsJsonAsync("/api/v1/limites-banco",
            new { bancoId, modalidade = "Finimp", valorLimiteBrl = 1_000_000m, dataVigenciaInicio = "2099-12-01" });
        postRes.IsSuccessStatusCode.Should().BeTrue();
        JsonElement body = await postRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid limiteId = body.GetProperty("id").GetGuid();

        HttpResponseMessage res = await client.DeleteAsync(
            $"/api/v1/limites-banco/{limiteId}/garantias-exigidas?tipo=Aval");

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "domínio lança InvalidOperationException quando não há revisão vigente → 409");
    }
}
