using System.Net;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que cotações ficam isoladas por tenant.
/// Cotações de TenantA não podem vazar para TenantB e vice-versa.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class CotacoesCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: listagem isolada ─────────────────────────────────────────────

    [Fact]
    public async Task Lista_TenantA_nao_retorna_cotacoes_do_TenantB()
    {
        // Arrange — cria uma cotação em cada tenant com código único
        string codA = $"COT-A-{Guid.NewGuid():N}"[..15];
        string codB = $"COT-B-{Guid.NewGuid():N}"[..15];

        Guid idA = await CriarCotacaoAsync(ClientA, codA);
        await CriarCotacaoAsync(ClientB, codB);

        // Act
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/cotacoes");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        IEnumerable<string> codigosRetornados = body
            .GetProperty("items")
            .EnumerateArray()
            .Select(c => c.GetProperty("codigoInterno").GetString()!);

        codigosRetornados.Should().Contain(codA,
            because: "TenantA deve ver sua própria cotação");
        codigosRetornados.Should().NotContain(codB,
            because: "TenantA não deve ver cotações de TenantB");
    }

    // ── Teste 2: get por ID de outro tenant retorna 404 ──────────────────────

    [Fact]
    public async Task Get_cotacao_de_outro_tenant_retorna_404()
    {
        // Arrange — cria cotação em TenantB
        string codB = $"COT-B2-{Guid.NewGuid():N}"[..15];
        Guid idTenantB = await CriarCotacaoAsync(ClientB, codB);

        // Act — TenantA tenta acessar cotação de TenantB
        HttpResponseMessage res = await ClientA.GetAsync($"/api/v1/cotacoes/{idTenantB}");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "o global filter EF deve bloquear acesso cross-tenant à cotação");
    }

    // ── Teste 3: cotação de B não vaza para A ────────────────────────────────

    [Fact]
    public async Task Cotacao_criada_em_TenantB_nao_aparece_na_lista_de_TenantA()
    {
        // Arrange
        string codB = $"COT-B3-{Guid.NewGuid():N}"[..15];
        Guid idB = await CriarCotacaoAsync(ClientB, codB);

        // Act — TenantA lista suas cotações
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/cotacoes");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        IEnumerable<string> idsRetornados = body
            .GetProperty("items")
            .EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid().ToString());

        idsRetornados.Should().NotContain(idB.ToString(),
            because: "TenantA não deve ver cotações de TenantB");
    }
}
