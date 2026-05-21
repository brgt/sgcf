using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Common;
using Sgcf.Domain.Auditoria;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(SgcfDbContext context) : IAuditLogRepository
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditFilter filter, CancellationToken ct)
    {
        // EF global query filter automatically applies tenant_id restriction.
        IQueryable<AuditLog> q = context.AuditLogs.AsNoTracking();

        return await BuildPagedResultAsync(q, filter, ct);
    }

    public async Task<PagedResult<AuditLogDto>> ListForTenantAsync(
        Guid tenantId,
        AuditFilter filter,
        CancellationToken ct)
    {
        // IgnoreQueryFilters bypasses tenant isolation so super-admin can query any tenant.
        IQueryable<AuditLog> q = context.AuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId);

        return await BuildPagedResultAsync(q, filter, ct);
    }

    private static async Task<PagedResult<AuditLogDto>> BuildPagedResultAsync(
        IQueryable<AuditLog> q,
        AuditFilter filter,
        CancellationToken ct)
    {
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

        if (filter.Impersonating.HasValue)
        {
            q = q.Where(a => a.Impersonating == filter.Impersonating.Value);
        }

        int total = await q.CountAsync(ct);

        int page = Math.Max(1, filter.Page);
        int pageSize = Math.Clamp(filter.PageSize, 1, 200);

        List<AuditLog> items = await q
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        List<AuditLogDto> dtos = new(items.Count);
        foreach (AuditLog a in items)
        {
            dtos.Add(new AuditLogDto(
                Id:              a.Id,
                OccurredAt:      a.OccurredAt.ToDateTimeOffset(),
                ActorSub:        a.ActorSub,
                ActorRole:       a.ActorRole,
                Source:          a.Source,
                Entity:          a.Entity,
                EntityId:        a.EntityId,
                Operation:       a.Operation,
                DiffJson:        a.DiffJson,
                RequestId:       a.RequestId,
                Impersonating:   a.Impersonating,
                ImpersonatedBy:  a.ImpersonatedBy));
        }

        return new PagedResult<AuditLogDto>(dtos.AsReadOnly(), total, page, pageSize);
    }
}
