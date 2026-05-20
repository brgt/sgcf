using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que limites operacionais de banco ficam isolados por tenant.
/// O limite criado por TenantA não deve aparecer para TenantB.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class LimitesBancoCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: lista isolada ────────────────────────────────────────────────

    [Fact]
    public async Task Lista_TenantA_nao_retorna_limites_de_TenantB()
    {
        // Arrange — cria um limite em TenantA
        Guid bancoId = await GarantirBancoAsync("907", "Banco Limites");

        HttpResponseMessage resCreate = await ClientA.PostAsJsonAsync(
            "/api/v1/limites-banco",
            new
            {
                bancoId,
                modalidade              = "Nce",
                valorLimiteBrl          = 10_000_000m,
                dataVigenciaInicio      = "2026-01-01",
                observacoes             = "Limite CrossTenant teste A",
            });

        resCreate.EnsureSuccessStatusCode();

        JsonElement created = JsonSerializer.Deserialize<JsonElement>(
            await resCreate.Content.ReadAsStringAsync(), JsonOpts);
        Guid idLimiteA = created.GetProperty("id").GetGuid();

        // Act — TenantB lista seus limites
        HttpResponseMessage resB = await ClientB.GetAsync("/api/v1/limites-banco");
        resB.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement bodyB = JsonSerializer.Deserialize<JsonElement>(
            await resB.Content.ReadAsStringAsync(), JsonOpts);

        IEnumerable<string> idsB = bodyB.EnumerateArray()
            .Select(l => l.GetProperty("id").GetGuid().ToString());

        idsB.Should().NotContain(idLimiteA.ToString(),
            because: "TenantB não deve ver limites criados por TenantA");
    }

    // ── Teste 2: get por ID de outro tenant retorna 404 ──────────────────────

    [Fact]
    public async Task Get_limite_de_outro_tenant_retorna_404()
    {
        // Arrange — cria um limite em TenantB
        Guid bancoId = await GarantirBancoAsync("908", "Banco Limites 2");

        HttpResponseMessage resCreate = await ClientB.PostAsJsonAsync(
            "/api/v1/limites-banco",
            new
            {
                bancoId,
                modalidade          = "Nce",
                valorLimiteBrl      = 5_000_000m,
                dataVigenciaInicio  = "2026-01-01",
                observacoes         = "Limite CrossTenant teste B",
            });

        resCreate.EnsureSuccessStatusCode();

        JsonElement created = JsonSerializer.Deserialize<JsonElement>(
            await resCreate.Content.ReadAsStringAsync(), JsonOpts);
        Guid idLimiteB = created.GetProperty("id").GetGuid();

        // Act — TenantA tenta acessar o limite de TenantB
        HttpResponseMessage resA = await ClientA.GetAsync($"/api/v1/limites-banco/{idLimiteB}");

        // Assert
        resA.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "o EF global filter bloqueia acesso cross-tenant ao limite");
    }
}
