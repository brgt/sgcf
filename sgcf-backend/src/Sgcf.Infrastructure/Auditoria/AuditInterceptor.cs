using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;

namespace Sgcf.Infrastructure.Auditoria;

public sealed class AuditInterceptor(
    ICurrentUserService currentUser,
    IRequestContextService requestContext,
    IClock clock)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        Instant now       = clock.GetCurrentInstant();
        string actorSub   = currentUser.ActorSub;
        string actorRole  = currentUser.ActorRole;
        string source     = requestContext.Source;
        Guid requestId    = requestContext.RequestId;
        byte[]? ipHash    = requestContext.IpHash;

        List<AuditLog> logs = new();

        foreach (EntityEntry entry in eventData.Context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is IAuditable))
        {
            string operation = entry.State switch
            {
                EntityState.Added    => "CREATE",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted  => "DELETE",
                _                    => "UPDATE",
            };

            Guid? entityId   = (entry.Entity as Entity)?.Id;
            string entityName = entry.Entity.GetType().Name;
            string? diffJson  = BuildDiff(entry);

            logs.Add(AuditLog.Create(
                now, actorSub, actorRole, source,
                entityName, entityId, operation,
                diffJson, requestId, ipHash));
        }

        if (logs.Count > 0)
        {
            eventData.Context.Set<AuditLog>().AddRange(logs);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string? BuildDiff(EntityEntry entry)
    {
        Dictionary<string, object?>? before = null;
        Dictionary<string, object?>? after  = null;

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            before = entry.OriginalValues.Properties
                .ToDictionary(p => p.Name, p => entry.OriginalValues[p]);
        }

        if (entry.State is EntityState.Added or EntityState.Modified)
        {
            after = entry.CurrentValues.Properties
                .ToDictionary(p => p.Name, p => entry.CurrentValues[p]);
        }

        if (before is null && after is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(new { before, after });
    }
}
