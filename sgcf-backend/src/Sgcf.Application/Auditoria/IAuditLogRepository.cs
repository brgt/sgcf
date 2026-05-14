using Sgcf.Application.Common;

namespace Sgcf.Application.Auditoria;

public interface IAuditLogRepository
{
    public Task<PagedResult<AuditEventoDto>> ListAsync(AuditFilter filter, CancellationToken ct);
}
