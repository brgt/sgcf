using Sgcf.Application.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy;

public interface ITenantRepository
{
    public Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default);
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Localiza por GUID (se <paramref name="idOrSlug"/> puder ser parseado como Guid)
    /// ou por slug caso contrário.
    /// </summary>
    public Task<Tenant?> GetByIdOrSlugAsync(string idOrSlug, CancellationToken ct = default);

    public Task<PagedResult<Tenant>> ListAsync(
        StatusTenant? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    public Task AddAsync(Tenant tenant, CancellationToken ct = default);
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
