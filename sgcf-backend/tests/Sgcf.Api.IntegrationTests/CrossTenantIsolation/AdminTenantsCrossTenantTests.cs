using System.Net;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que endpoints de administração de tenants só são acessíveis por super-admins.
/// Um tenant regular (sem role "super-admin") deve receber 403 ao tentar acessar esses endpoints.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class AdminTenantsCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: tenant regular não pode listar tenants ──────────────────────

    [Fact]
    public async Task TenantA_sem_role_super_admin_recebe_403_ao_listar_tenants()
    {
        // ClientA tem roles "admin" e "tesouraria", mas NÃO "super-admin".
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/admin/tenants");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a policy SuperAdmin exige o role 'super-admin' que TenantA não possui");
    }

    // ── Teste 2: tenant regular não pode provisionar outro tenant ────────────

    [Fact]
    public async Task TenantA_sem_role_super_admin_recebe_403_ao_provisionar()
    {
        HttpResponseMessage res = await ClientA.PostAsync(
            $"/api/v1/admin/tenants/{TenantTestData.TenantBSlug}/provisionar", content: null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "apenas super-admins podem provisionar tenants");
    }

    // ── Teste 3: super-admin pode listar tenants ──────────────────────────────

    [Fact]
    public async Task SuperAdmin_pode_listar_todos_os_tenants()
    {
        // AdminClient inclui role "super-admin" via CrossTenantTestAuthHandler.
        HttpResponseMessage res = await AdminClient.GetAsync("/api/v1/admin/tenants");

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "super-admins devem ter acesso irrestrito ao catálogo de tenants");
    }

    // ── Teste 4: super-admin vê ambos os tenants da suite ────────────────────

    [Fact]
    public async Task SuperAdmin_ve_TenantA_e_TenantB_na_listagem()
    {
        HttpResponseMessage res = await AdminClient.GetAsync("/api/v1/admin/tenants?pageSize=100");
        res.EnsureSuccessStatusCode();

        string body = await res.Content.ReadAsStringAsync();

        body.Should().Contain(TenantTestData.TenantASlug,
            because: "TenantA deve estar visível para o super-admin");
        body.Should().Contain(TenantTestData.TenantBSlug,
            because: "TenantB deve estar visível para o super-admin");
    }
}
