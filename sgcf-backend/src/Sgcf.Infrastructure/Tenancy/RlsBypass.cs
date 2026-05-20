namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Helper para operações cross-tenant que precisam ignorar o RLS.
///
/// TODO (Task -1.12 / healthcheck): implementar bypass real usando a role
/// <c>sgcf_super</c> (BYPASSRLS) em uma conexão dedicada, separada da
/// conexão do <see cref="Persistence.SgcfDbContext"/> principal.
/// Não usar set_config com string vazia pois isso causa 0 linhas visíveis,
/// não bypass — essa semântica é "acesso bloqueado", não "acesso total".
///
/// O bypass correto exige uma segunda connection string que conecta como
/// o role sgcf_super, fora do pool de conexões padrão do EF Core.
/// </summary>
internal static class RlsBypass
{
    // Placeholder — implementação completa aguarda Task -1.12.
}
