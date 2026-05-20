using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Common;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository(SgcfDbContext context) : ITenantRepository
{
    public Task<Tenant?> GetAsync(Guid id, CancellationToken ct) =>
        context.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        // Slugs are always stored lowercase by Tenant.Criar — compare directly.
        string normalizedSlug = slug.ToLowerInvariant();
        return context.Tenants.FirstOrDefaultAsync(t => t.Slug == normalizedSlug, ct);
    }

    public Task<Tenant?> GetByIdOrSlugAsync(string idOrSlug, CancellationToken ct)
    {
        if (Guid.TryParse(idOrSlug, out Guid id))
        {
            return context.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        }

        string normalizedSlug = idOrSlug.ToLowerInvariant();
        return context.Tenants.FirstOrDefaultAsync(t => t.Slug == normalizedSlug, ct);
    }

    public async Task<PagedResult<Tenant>> ListAsync(
        StatusTenant? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        IQueryable<Tenant> q = context.Tenants.AsNoTracking();

        if (status.HasValue)
        {
            q = q.Where(t => t.Status == status.Value);
        }

        int total = await q.CountAsync(ct);
        List<Tenant> items = await q
            .OrderBy(t => t.Slug)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Tenant>(items.AsReadOnly(), total, page, pageSize);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        string normalizedSlug = slug.ToLowerInvariant();
        return context.Tenants.AnyAsync(t => t.Slug == normalizedSlug, ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct)
    {
        await context.Tenants.AddAsync(tenant, ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
