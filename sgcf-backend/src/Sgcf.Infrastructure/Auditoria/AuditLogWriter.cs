using System.Text.Json;
using NodaTime;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Common;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Auditoria;
using Sgcf.Infrastructure.Persistence;

namespace Sgcf.Infrastructure.Auditoria;

/// <summary>
/// Implementação de <see cref="IAuditLogWriter"/> que persiste via EF Core.
///
/// Lê o contexto de impersonação diretamente de <see cref="ITenantContext"/>:
/// quando <c>IsImpersonating = true</c>, o super-admin está operando em nome do tenant
/// e a trilha de auditoria deve registrar o sub real do super-admin em <c>ImpersonatedBy</c>.
/// </summary>
internal sealed class AuditLogWriter(
    SgcfDbContext db,
    ICurrentUserService currentUser,
    IRequestContextService requestContext,
    ITenantContext tenantContext,
    IClock clock) : IAuditLogWriter
{
    public async Task WriteAsync(
        string entity,
        Guid? entityId,
        string operation,
        object? diff,
        CancellationToken ct)
    {
        bool impersonating     = tenantContext.IsResolved && tenantContext.IsImpersonating;
        string actorSub        = currentUser.ActorSub;
        string? impersonatedBy = impersonating ? actorSub : null;

        AuditLog log = AuditLog.Create(
            occurredAt:      clock.GetCurrentInstant(),
            actorSub:        actorSub,
            actorRole:       currentUser.ActorRole,
            source:          requestContext.Source,
            entity:          entity,
            entityId:        entityId,
            operation:       operation,
            diffJson:        diff is null ? null : JsonSerializer.Serialize(diff),
            requestId:       requestContext.RequestId,
            ipHash:          requestContext.IpHash,
            impersonating:   impersonating,
            impersonatedBy:  impersonatedBy);

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}
