using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Campos de domínio (carência, indexador) via HTTP, incluindo round-trip das colunas planas — SPEC S40 §2.3, §2.4.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class CotacaoDominioHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Carencia_negativa_retorna_400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 12,
            prazoMaximoUnidade = "Meses",
            carenciaMeses = -1,
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Carencia_e_indexador_fazem_round_trip_via_get()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage criar = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 48,
            prazoMaximoUnidade = "Meses",
            carenciaMeses = 18,
            indexadorBase = new { tipo = "Tlp", spreadAa = 1.5m },
            dataAbertura = "2026-05-16",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created, await criar.Content.ReadAsStringAsync());
        Guid id = (await criar.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        JsonElement body = await (await client.GetAsync($"/api/v1/cotacoes/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("carenciaMeses").GetInt32().Should().Be(18);
        JsonElement idx = body.GetProperty("indexadorBase");
        idx.GetProperty("tipo").GetString().Should().Be("Tlp");
        idx.GetProperty("spreadAa").GetDecimal().Should().Be(1.5m);
    }

    [Fact]
    public async Task Cobertura_fgi_fora_da_faixa_retorna_400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 24,
            prazoMaximoUnidade = "Meses",
            percentualCoberturaFgi = 150m,
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Estruturantes_fgi_fazem_round_trip_via_get()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage criar = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Fgi",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 60,
            prazoMaximoUnidade = "Meses",
            percentualCoberturaFgi = 80m,
            finalidadeBndes = "Investimento",
            bancoRepassadorPretendido = "BancoDoBrasil",
            dataAbertura = "2026-05-16",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created, await criar.Content.ReadAsStringAsync());
        Guid id = (await criar.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        JsonElement body = await (await client.GetAsync($"/api/v1/cotacoes/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("percentualCoberturaFgi").GetDecimal().Should().Be(80m);
        body.GetProperty("finalidadeBndes").GetString().Should().Be("Investimento");
        body.GetProperty("bancoRepassadorPretendido").GetString().Should().Be("BancoDoBrasil");
    }
}
