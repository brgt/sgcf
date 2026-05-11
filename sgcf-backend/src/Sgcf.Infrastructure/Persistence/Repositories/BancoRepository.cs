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

    public async Task<IReadOnlyList<Banco>> ListAllAsync(CancellationToken ct)
    {
        List<Banco> list = await context.Bancos.ToListAsync(ct);
        return list.AsReadOnly();
    }

    public void Add(Banco banco) => context.Bancos.Add(banco);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
