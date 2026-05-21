namespace Sgcf.Application.Tenancy.Services;

/// <summary>
/// Valida em runtime que o RLS está habilitado e funcionando em todas as tabelas tenant-scoped.
///
/// Executa 4 checks:
/// 1. rls_enabled_all_tables — todas as tabelas tenant-scoped têm RLS habilitada.
/// 2. policies_present — todas as tabelas têm a policy tenant_isolation definida.
/// 3. isolation_canary_no_context — sem app.tenant_id setado, contrato retorna 0 linhas.
/// 4. isolation_canary_with_proxys — com app.tenant_id do tenant Proxys, contrato retorna linhas (sem erro).
///
/// Uso: endpoint GET /health/rls (super-admin only).
/// </summary>
public interface IRlsHealthCheckService
{
    /// <summary>Executa todos os checks e retorna o relatório consolidado.</summary>
    public Task<RlsHealthReport> CheckAsync(CancellationToken ct);
}
