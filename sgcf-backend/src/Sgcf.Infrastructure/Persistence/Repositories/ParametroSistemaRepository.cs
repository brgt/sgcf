using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Sistema;
using Sgcf.Domain.Sistema;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IParametroSistemaRepository"/> — per-tenant.
///
/// O EF Core global query filter (Task −1.4) garante que todas as queries
/// neste repositório retornam apenas dados do tenant ativo no contexto atual.
/// Nenhum parâmetro <c>tenantId</c> é passado explicitamente — confiar no filter.
/// </summary>
internal sealed class ParametroSistemaRepository(SgcfDbContext context) : IParametroSistemaRepository
{
    /// <inheritdoc/>
    public Task<ParametroSistema?> GetAsync(CancellationToken ct = default) =>
        context.Set<ParametroSistema>()
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc/>
    public Task<bool> ExisteParaTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        context.Set<ParametroSistema>()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId, ct);

    /// <inheritdoc/>
    public void Add(ParametroSistema parametro) =>
        context.Set<ParametroSistema>().Add(parametro);

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
