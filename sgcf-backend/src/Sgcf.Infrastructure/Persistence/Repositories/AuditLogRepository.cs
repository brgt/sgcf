using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Common;
using Sgcf.Domain.Auditoria;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(SgcfDbContext context) : IAuditLogRepository
{
    public async Task<PagedResult<AuditEventoDto>> ListAsync(AuditFilter filter, CancellationToken ct)
    {
        IQueryable<AuditLog> q = context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Entity))
        {
            q = q.Where(a => a.Entity == filter.Entity);
        }

        if (filter.EntityId.HasValue)
        {
            q = q.Where(a => a.EntityId == filter.EntityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorSub))
        {
            q = q.Where(a => a.ActorSub == filter.ActorSub);
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            q = q.Where(a => a.Source == filter.Source);
        }

        if (!string.IsNullOrWhiteSpace(filter.Operation))
        {
            q = q.Where(a => a.Operation == filter.Operation);
        }

        if (filter.De.HasValue)
        {
            Instant de = Instant.FromDateTimeOffset(filter.De.Value);
            q = q.Where(a => a.OccurredAt >= de);
        }

        if (filter.Ate.HasValue)
        {
            Instant ate = Instant.FromDateTimeOffset(filter.Ate.Value);
            q = q.Where(a => a.OccurredAt <= ate);
        }

        int total = await q.CountAsync(ct);

        int page = Math.Max(1, filter.Page);
        int pageSize = Math.Clamp(filter.PageSize, 1, 200);

        List<AuditLog> items = await q
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        List<AuditEventoDto> dtos = new(items.Count);
        foreach (AuditLog a in items)
        {
            dtos.Add(new AuditEventoDto(
                Id: a.Id,
                OccurredAt: a.OccurredAt.ToDateTimeOffset(),
                ActorSub: a.ActorSub,
                ActorRole: a.ActorRole,
                Source: a.Source,
                Entity: a.Entity,
                EntityId: a.EntityId,
                Operation: a.Operation,
                DiffJson: a.DiffJson,
                RequestId: a.RequestId));
        }

        return new PagedResult<AuditEventoDto>(dtos.AsReadOnly(), total, page, pageSize);
    }
}
