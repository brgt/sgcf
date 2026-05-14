using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Bancos;
using Sgcf.Domain.Bancos;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class BancoRepository(SgcfDbContext context) : IBancoRepository
{
    public Task<Banco?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Bancos.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Banco?> GetByCodigoCompeAsync(string codigoCompe, CancellationToken ct) =>
        context.Bancos.FirstOrDefaultAsync(b => b.CodigoCompe == codigoCompe, ct);

    public Task<Banco?> GetByApelidoAsync(string apelido, CancellationToken ct) =>
        context.Bancos.FirstOrDefaultAsync(
            b => EF.Functions.ILike(b.Apelido, apelido), ct);

    public async Task<IReadOnlyList<Banco>> ListAllAsync(CancellationToken ct)
    {
        List<Banco> list = await context.Bancos.ToListAsync(ct);
        return list.AsReadOnly();
    }

    public void Add(Banco banco) => context.Bancos.Add(banco);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<Banco>> ListComLimiteCreditoSetadoAsync(CancellationToken ct)
    {
        List<Banco> list = await context.Bancos
            .Where(b => b.LimiteCreditoBrlDecimal != null)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Banco>> ListFilteredAsync(string? search, CancellationToken ct)
    {
        IQueryable<Banco> q = context.Bancos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            q = q.Where(b => EF.Functions.ILike(b.CodigoCompe, pattern)
                          || EF.Functions.ILike(b.RazaoSocial, pattern)
                          || EF.Functions.ILike(b.Apelido, pattern));
        }

        List<Banco> list = await q.OrderBy(b => b.Apelido).ToListAsync(ct);
        return list.AsReadOnly();
    }
}
