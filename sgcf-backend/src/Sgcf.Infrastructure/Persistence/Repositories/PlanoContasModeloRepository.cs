using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Contabilidade;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório para o modelo global de plano de contas.
///
/// Usa <c>IgnoreQueryFilters()</c> para garantir acesso independente de tenant
/// — o modelo não é tenant-scoped, mas o DbContext pode ter tenant resolvido.
/// </summary>
internal sealed class PlanoContasModeloRepository(SgcfDbContext context) : IPlanoContasModeloRepository
{
    public Task<PlanoContasModelo?> GetByCodigoAsync(string codigoGerencial, CancellationToken ct = default) =>
        context.Set<PlanoContasModelo>()
            .FirstOrDefaultAsync(m => m.CodigoGerencial == codigoGerencial, ct);

    public async Task<IReadOnlyList<PlanoContasModelo>> ListAllAsync(CancellationToken ct = default)
    {
        List<PlanoContasModelo> list = await context.Set<PlanoContasModelo>()
            .AsNoTracking()
            .OrderBy(m => m.CodigoGerencial)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public void Add(PlanoContasModelo modelo) => context.Set<PlanoContasModelo>().Add(modelo);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
