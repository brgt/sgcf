using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Stub — Alertas ainda não está implementado como módulo independente.
///
/// Quando o módulo Alertas for entregue, este arquivo deve ser expandido com:
/// <list type="bullet">
///   <item>Lista_TenantA_nao_retorna_alertas_do_TenantB</item>
///   <item>Get_alerta_de_outro_tenant_retorna_404</item>
///   <item>Tenant_sem_alertas_retorna_lista_vazia</item>
/// </list>
///
/// Os alertas actuais são gerados inline pelo módulo Contratos e retornam
/// via <c>ContratoDto.Alertas</c>, cobertura já feita em ContratosCrossTenantTests.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class AlertasCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // Placeholder — nenhum endpoint /api/v1/alertas existe ainda.
    // Testes serão adicionados quando o módulo for implementado.
}
