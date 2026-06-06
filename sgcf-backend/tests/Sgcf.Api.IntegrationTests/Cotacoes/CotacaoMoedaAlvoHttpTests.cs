using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// PTAX multimoeda e moedaAlvo via HTTP — SPEC S40 §4.5, §6.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class CotacaoMoedaAlvoHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Modalidade_brl_pura_rejeita_moeda_estrangeira_400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 12,
            prazoMaximoUnidade = "Meses",
            moedaAlvo = "Usd",
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ptax_indisponivel_para_moeda_sem_cotacao_retorna_409_tipado()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        // A fixture semeia apenas PTAX USD; Lei4131 em EUR não encontra PTAX → 409 tipado.
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Lei4131",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 36,
            prazoMaximoUnidade = "Meses",
            moedaAlvo = "Eur",
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("type").GetString().Should().Be("https://sgcf.nordware.io/errors/ptax-indisponivel");
        body.GetProperty("moedaAlvo").GetString().Should().Be("Eur");
        body.GetProperty("dataPtaxReferencia").GetString().Should().NotBeNullOrEmpty();
    }
}
