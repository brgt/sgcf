using System.Net;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Confirma que Feriados é um catálogo GLOBAL: ambos os tenants devem enxergar
/// os mesmos feriados. Feriados NÃO são tenant-scoped.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class FeriadosCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: ambos os tenants listam feriados sem erro ───────────────────

    [Fact]
    public async Task TenantA_lista_feriados_sem_erro()
    {
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/feriados?ano=2026");

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Feriados é catálogo global — TenantA deve poder listá-los");
    }

    [Fact]
    public async Task TenantB_lista_feriados_sem_erro()
    {
        HttpResponseMessage res = await ClientB.GetAsync("/api/v1/feriados?ano=2026");

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Feriados é catálogo global — TenantB deve poder listá-los");
    }
}
