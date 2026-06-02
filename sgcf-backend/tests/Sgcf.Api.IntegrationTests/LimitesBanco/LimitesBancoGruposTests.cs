using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Integração da Fase 1 de Garantias Alternativas (T4): cadastrar um grupo "OU"
/// ("CDB OU Recebíveis") via API e relê-lo preservando GrupoAlternativaId/GrupoRotulo,
/// tanto na resposta do POST quanto em GET /revisoes-garantias. RF-11/RF-12/RF-15.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class LimitesBancoGruposTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco GRP{codigoCompe} S.A.",
            apelido = $"GRP{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task CriarLimite_ComGrupoCdbOuRecebiveis_PersisteEReleAgrupamento()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "GR1");
        Guid grupo = Guid.NewGuid();
        const string rotulo = "Colateral mínimo FINIMP";

        // POST limite com um grupo "OU": CDB OU Recebíveis (boleto), mesmos GrupoAlternativaId/rótulo.
        HttpResponseMessage postRes = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 5_000_000m,
            dataVigenciaInicio = "2099-01-01",
            garantiasExigidas = new object[]
            {
                new
                {
                    tipo = "CdbCativo",
                    percentualSobreLimite = 100m,
                    grupoAlternativaId = grupo.ToString(),
                    grupoRotulo = rotulo,
                },
                new
                {
                    tipo = "BoletoBancario",
                    percentualSobreLimite = 90m,
                    grupoAlternativaId = grupo.ToString(),
                    grupoRotulo = rotulo,
                },
            },
        });

        string postRaw = await postRes.Content.ReadAsStringAsync();
        postRes.StatusCode.Should().Be(HttpStatusCode.Created, postRaw);

        JsonElement postBody = JsonSerializer.Deserialize<JsonElement>(postRaw, JsonOpts);
        Guid limiteId = postBody.GetProperty("id").GetGuid();

        // Resposta do POST: os 2 itens carregam o mesmo grupo e são obrigatórios (GA-04).
        JsonElement garantias = postBody.GetProperty("garantiasExigidas");
        garantias.GetArrayLength().Should().Be(2);
        foreach (JsonElement item in garantias.EnumerateArray())
        {
            item.GetProperty("grupoAlternativaId").GetGuid().Should().Be(grupo);
            item.GetProperty("grupoRotulo").GetString().Should().Be(rotulo);
            item.GetProperty("obrigatoria").GetBoolean().Should().BeTrue("item de grupo é sempre obrigatório (GA-04)");
        }

        // GET /revisoes-garantias: relê o agrupamento na revisão vigente.
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/limites-banco/{limiteId}/revisoes-garantias");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement getBody = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        JsonElement revisoes = getBody.GetProperty("revisoes");
        revisoes.GetArrayLength().Should().Be(1);
        JsonElement itens = revisoes[0].GetProperty("itens");
        itens.GetArrayLength().Should().Be(2);

        itens.EnumerateArray().Select(i => i.GetProperty("grupoAlternativaId").GetGuid())
            .Should().AllBeEquivalentTo(grupo);
        itens.EnumerateArray().Select(i => i.GetProperty("tipo").GetString())
            .Should().BeEquivalentTo(["CdbCativo", "BoletoBancario"]);
    }

    [Fact]
    public async Task CriarLimite_SemCamposDeGrupo_PermaneceRetrocompativel()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "GR2");

        HttpResponseMessage postRes = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 5_000_000m,
            dataVigenciaInicio = "2099-01-01",
            garantiasExigidas = new object[]
            {
                new { tipo = "Sblc", valorFixoBrl = 1_000_000m, obrigatoria = true },
            },
        });

        string postRaw = await postRes.Content.ReadAsStringAsync();
        postRes.StatusCode.Should().Be(HttpStatusCode.Created, postRaw);

        JsonElement postBody = JsonSerializer.Deserialize<JsonElement>(postRaw, JsonOpts);
        JsonElement item = postBody.GetProperty("garantiasExigidas")[0];

        // Campos de grupo aditivos: ausentes ou nulos quando o item não pertence a um grupo.
        if (item.TryGetProperty("grupoAlternativaId", out JsonElement grupoId))
        {
            grupoId.ValueKind.Should().Be(JsonValueKind.Null);
        }

        if (item.TryGetProperty("grupoRotulo", out JsonElement rotulo))
        {
            rotulo.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }
}
