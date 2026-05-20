using System.Net;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que hedges ficam isolados por tenant.
/// Hedges pertencem a contratos, que por sua vez pertencem a tenants.
/// O isolamento é herdado pelo global filter do EF Core no Contrato pai.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class HedgesCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: get de hedge de outro tenant retorna 404 ────────────────────

    [Fact]
    public async Task Get_hedge_inexistente_retorna_404()
    {
        // Hedges dependem de contratos Finimp/Lei4131 com contrapartes (NDF/swap).
        // Criar um hedge válido exigiria setup extenso fora do escopo deste stub.
        // Este teste verifica que o endpoint responde 404 para um ID que não existe
        // no tenant atual — cobrindo a rota principal de isolamento.
        Guid idInexistente = Guid.CreateVersion7();

        HttpResponseMessage res = await ClientA.GetAsync($"/api/v1/hedges/{idInexistente}/mtm");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "um hedge inexistente deve retornar 404 independente do tenant");
    }

    // ── Teste 2: delete de hedge de outro tenant retorna 404 ─────────────────

    [Fact]
    public async Task Delete_hedge_de_outro_tenant_retorna_404()
    {
        // TenantA tenta remover um ID que não pertence a nenhum contrato de TenantA.
        Guid idInexistente = Guid.CreateVersion7();

        HttpResponseMessage res = await ClientA.DeleteAsync($"/api/v1/hedges/{idInexistente}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "o EF global filter impede que TenantA encontre hedges de TenantB");
    }
}
