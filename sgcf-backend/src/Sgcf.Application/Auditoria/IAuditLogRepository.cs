using NodaTime;
using Sgcf.Application.Common;

namespace Sgcf.Application.Auditoria;

public interface IAuditLogRepository
{
    /// <summary>
    /// Lista eventos de auditoria do tenant corrente (EF global filter aplica tenant_id).
    /// Para acesso cross-tenant, use <see cref="ListForTenantAsync"/>.
    /// </summary>
    public Task<PagedResult<AuditLogDto>> ListAsync(AuditFilter filter, CancellationToken ct);

    /// <summary>
    /// Lista eventos de auditoria de um tenant específico, ignorando o global filter.
    /// Uso exclusivo de endpoints super-admin.
    /// </summary>
    public Task<PagedResult<AuditLogDto>> ListForTenantAsync(Guid tenantId, AuditFilter filter, CancellationToken ct);

    /// <summary>
    /// Agrega operações por analista no período informado e calcula o SLA médio de atendimento
    /// (duração entre primeira e última operação sobre o mesmo EntityId).
    /// O EF global filter aplica tenant_id automaticamente.
    /// </summary>
    /// <param name="de">Início do período (inclusivo), em UTC.</param>
    /// <param name="ate">Fim do período (inclusivo), em UTC.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public Task<IReadOnlyList<ProdutividadeAnalistaDto>> GetProdutividadeAsync(
        Instant de, Instant ate, CancellationToken cancellationToken);
}
