using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Contrato de erros RFC 7807 nas operações de cotação — SPEC S40 §5.
/// Garante que 409/404 são ProblemDetails com type estável (e não o antigo { error }).
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class CotacaoErrosHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Conflito_de_estado_retorna_problemdetails_409_com_type_estavel()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage criar = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 12,
            prazoMaximoUnidade = "Meses",
            dataAbertura = "2026-05-16",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = (await criar.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        // Cancela uma vez (Rascunho → Recusada).
        HttpResponseMessage cancela1 = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{id}/cancelar", new { motivo = "Não necessária" });
        cancela1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Cancela de novo: estado final → conflito.
        HttpResponseMessage cancela2 = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{id}/cancelar", new { motivo = "De novo" });

        cancela2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement body = await cancela2.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("type").GetString().Should().Be("https://sgcf.nordware.io/errors/conflito-de-estado");
        body.GetProperty("status").GetInt32().Should().Be(409);
        body.TryGetProperty("error", out _).Should().BeFalse("o corpo legado foi substituído por ProblemDetails");
    }

    [Fact]
    public async Task Recurso_inexistente_retorna_problemdetails_404_com_type_estavel()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.GetAsync($"/api/v1/cotacoes/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("type").GetString().Should().Be("https://sgcf.nordware.io/errors/nao-encontrado");
    }
}
