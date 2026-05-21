using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Tesouraria;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class ContaBancariaRepository(SgcfDbContext context) : IContaBancariaRepository
{
    public Task<ContaBancaria?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.ContasBancarias.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ContaBancaria>> ListAsync(bool? apenasAtivas, CancellationToken ct)
    {
        IQueryable<ContaBancaria> query = context.ContasBancarias.AsNoTracking();

        if (apenasAtivas.HasValue)
        {
            query = query.Where(c => c.Ativa == apenasAtivas.Value);
        }

        List<ContaBancaria> list = await query
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task AddAsync(ContaBancaria conta, CancellationToken ct)
    {
        await context.ContasBancarias.AddAsync(conta, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
