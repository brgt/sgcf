using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Contabilidade;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class PlanoContasRepository(SgcfDbContext context) : IPlanoContasRepository
{
    public Task<PlanoContasGerencial?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<PlanoContasGerencial>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PlanoContasGerencial?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        context.Set<PlanoContasGerencial>().FirstOrDefaultAsync(p => p.CodigoGerencial == codigo, cancellationToken);

    public async Task<IReadOnlyList<PlanoContasGerencial>> ListAllAsync(CancellationToken cancellationToken)
    {
        List<PlanoContasGerencial> list = await context.Set<PlanoContasGerencial>().ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<PlanoContasGerencial>> ListFilteredAsync(string? search, bool? ativo, CancellationToken cancellationToken)
    {
        IQueryable<PlanoContasGerencial> q = context.Set<PlanoContasGerencial>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.CodigoGerencial, pattern)
                          || EF.Functions.ILike(p.Nome, pattern));
        }

        if (ativo.HasValue)
        {
            q = q.Where(p => p.Ativo == ativo.Value);
        }

        List<PlanoContasGerencial> list = await q.OrderBy(p => p.CodigoGerencial).ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public void Add(PlanoContasGerencial conta) => context.Set<PlanoContasGerencial>().Add(conta);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
