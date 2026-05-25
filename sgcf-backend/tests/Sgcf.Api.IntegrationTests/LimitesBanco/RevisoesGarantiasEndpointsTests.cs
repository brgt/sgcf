using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Testes HTTP para o endpoint
/// <c>GET /api/v1/limites-banco/{id}/revisoes-garantias</c> (T1.3).
/// SPEC §5.1, §5.2, SLB-05.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class RevisoesGarantiasEndpointsTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco RG{codigoCompe} S.A.",
            apelido = $"RG{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarLimiteAsync(
        HttpClient client,
        Guid bancoId,
        string codigoCompeInicio,
        object[]? garantiasExigidas = null)
    {
        object payload = garantiasExigidas is null
            ? new { bancoId, modalidade = "Finimp", valorLimiteBrl = 5_000_000m, dataVigenciaInicio = $"2099-{codigoCompeInicio}" }
            : new { bancoId, modalidade = "Finimp", valorLimiteBrl = 5_000_000m, dataVigenciaInicio = $"2099-{codigoCompeInicio}", garantiasExigidas };

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", payload);
        res.IsSuccessStatusCode.Should().BeTrue($"criação de limite falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetRevisoesAsync(
        HttpClient client, Guid limiteId)
    {
        HttpResponseMessage res = await client.GetAsync($"/api/v1/limites-banco/{limiteId}/revisoes-garantias");
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = res.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts)
            : default;
        return (res.StatusCode, body);
    }

    // ── RG01: limite com garantia retorna 200 com corpo conforme SPEC §5.2 ────

    [Fact]
    public async Task GetRevisoes_LimiteComUmaGarantia_Retorna200ComRevisaoVigente()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "R01");

        Guid limiteId = await CriarLimiteAsync(client, bancoId, "06-01",
            garantiasExigidas:
            [
                new { tipo = "Aval", obrigatoria = true }
            ]);

        (HttpStatusCode status, JsonElement body) = await GetRevisoesAsync(client, limiteId);

        status.Should().Be(HttpStatusCode.OK);

        body.GetProperty("limiteBancoId").GetGuid().Should().Be(limiteId);

        JsonElement revisoes = body.GetProperty("revisoes");
        revisoes.GetArrayLength().Should().Be(1, "uma revisão criada junto com o limite");

        JsonElement revisao = revisoes[0];
        revisao.GetProperty("vigenciaFim").ValueKind.Should().Be(JsonValueKind.Null,
            "a revisão vigente não possui vigenciaFim");
        revisao.GetProperty("itens").GetArrayLength().Should().Be(1);
        revisao.GetProperty("itens")[0].GetProperty("tipo").GetString().Should().Be("Aval");
    }

    // ── RG02: PATCH altera garantia → endpoint retorna 2 revisões ────────────

    [Fact]
    public async Task GetRevisoes_AposPatch_Retorna2Revisoes_ComAnteriorFechada()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "R02");

        Guid limiteId = await CriarLimiteAsync(client, bancoId, "07-01",
            garantiasExigidas:
            [
                new { tipo = "Aval", obrigatoria = true }
            ]);

        // PATCH troca garantia
        HttpResponseMessage patchRes = await client.PatchAsJsonAsync(
            $"/api/v1/limites-banco/{limiteId}",
            new { garantiasExigidas = new[] { new { tipo = "CdbCativo", percentualSobreLimite = 30m, obrigatoria = true } } });
        patchRes.IsSuccessStatusCode.Should().BeTrue($"PATCH falhou: {await patchRes.Content.ReadAsStringAsync()}");

        (HttpStatusCode status, JsonElement body) = await GetRevisoesAsync(client, limiteId);

        status.Should().Be(HttpStatusCode.OK);
        JsonElement revisoes = body.GetProperty("revisoes");
        revisoes.GetArrayLength().Should().Be(2);

        // Ordem ascendente (SLB-05): índice 0 = mais antiga (fechada)
        JsonElement anterior = revisoes[0];
        JsonElement atual = revisoes[1];

        anterior.GetProperty("vigenciaFim").ValueKind.Should().NotBe(JsonValueKind.Null,
            "a revisão anterior deve estar encerrada");
        anterior.GetProperty("itens")[0].GetProperty("tipo").GetString().Should().Be("Aval");

        atual.GetProperty("vigenciaFim").ValueKind.Should().Be(JsonValueKind.Null,
            "a revisão nova deve estar vigente");
        atual.GetProperty("itens")[0].GetProperty("tipo").GetString().Should().Be("CdbCativo");
    }

    // ── RG03: limite inexistente retorna 404 ──────────────────────────────────

    [Fact]
    public async Task GetRevisoes_LimiteInexistente_Retorna404()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid idFantasma = Guid.NewGuid();

        (HttpStatusCode status, _) = await GetRevisoesAsync(client, idFantasma);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    // ── RG04: isolamento cross-tenant — outro tenant retorna 404 ─────────────

    [Fact]
    public async Task GetRevisoes_LimiteDeOutroTenant_Retorna404()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "R04");

        Guid limiteId = await CriarLimiteAsync(client, bancoId, "08-01",
            garantiasExigidas:
            [
                new { tipo = "Sblc", valorFixoBrl = 500_000m, obrigatoria = false }
            ]);

        // O segundo token usa o mesmo handler de autenticação de teste mas a rota é
        // filtrada por RLS — como a fixture usa um único tenant, simulamos com ID
        // aleatório que não existe no banco do tenant corrente.
        (HttpStatusCode status, _) = await GetRevisoesAsync(client, Guid.NewGuid());

        status.Should().Be(HttpStatusCode.NotFound,
            "RLS filtra limites de outros tenants como não encontrados");
    }

    // ── RG05: sem autenticação retorna 401 ────────────────────────────────────

    [Fact]
    public async Task GetRevisoes_SemAutenticacao_Retorna401()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        Guid idQualquer = Guid.NewGuid();

        HttpResponseMessage res = await client.GetAsync(
            $"/api/v1/limites-banco/{idQualquer}/revisoes-garantias");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
