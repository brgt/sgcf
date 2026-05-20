using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que contas do plano de contas ficam isoladas por tenant.
/// Uma conta criada por TenantA não pode ser lida por TenantB.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class PlanoContasCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: conta criada em TenantA não aparece para TenantB ────────────

    [Fact]
    public async Task Conta_criada_em_TenantA_nao_aparece_para_TenantB()
    {
        // Arrange — cria uma conta no plano de TenantA
        string codigo = $"9{Guid.NewGuid():N}"[..6];

        HttpResponseMessage resCreate = await ClientA.PostAsJsonAsync(
            "/api/v1/plano-contas",
            new
            {
                codigoGerencial = codigo,
                nome            = "Conta CrossTenant Teste",
                natureza        = "Passivo",
            });

        resCreate.EnsureSuccessStatusCode();

        JsonElement created = JsonSerializer.Deserialize<JsonElement>(
            await resCreate.Content.ReadAsStringAsync(), JsonOpts);
        Guid idContaA = created.GetProperty("id").GetGuid();

        // Act — TenantB lista suas contas
        HttpResponseMessage resB = await ClientB.GetAsync("/api/v1/plano-contas");
        resB.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement bodyB = JsonSerializer.Deserialize<JsonElement>(
            await resB.Content.ReadAsStringAsync(), JsonOpts);

        IEnumerable<string> idsB = bodyB.EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid().ToString());

        idsB.Should().NotContain(idContaA.ToString(),
            because: "TenantB não deve ver contas do plano de TenantA");
    }

    // ── Teste 2: get por ID de outro tenant retorna 404 ──────────────────────

    [Fact]
    public async Task Get_conta_de_outro_tenant_retorna_404()
    {
        // Arrange — cria conta em TenantB
        string codigo = $"8{Guid.NewGuid():N}"[..6];

        HttpResponseMessage resCreate = await ClientB.PostAsJsonAsync(
            "/api/v1/plano-contas",
            new
            {
                codigoGerencial = codigo,
                nome            = "Conta TenantB Cross Test",
                natureza        = "Ativo",
            });

        resCreate.EnsureSuccessStatusCode();

        JsonElement created = JsonSerializer.Deserialize<JsonElement>(
            await resCreate.Content.ReadAsStringAsync(), JsonOpts);
        Guid idContaB = created.GetProperty("id").GetGuid();

        // Act — TenantA tenta acessar a conta de TenantB
        HttpResponseMessage resA = await ClientA.GetAsync($"/api/v1/plano-contas/{idContaB}");

        // Assert
        resA.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "o EF global filter bloqueia acesso cross-tenant ao plano de contas");
    }
}
