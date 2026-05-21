using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Covenants;
using Sgcf.Domain.Covenants;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class CovenantRepository(SgcfDbContext context) : ICovenantRepository
{
    public Task<Covenant?> GetAsync(Guid id, CancellationToken ct) =>
        context.Covenants.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Covenant>> ListByContratoAsync(Guid contratoId, CancellationToken ct)
    {
        List<Covenant> list = await context.Covenants
            .Where(c => c.ContratoId == contratoId)
            .OrderBy(c => c.Descricao)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Covenant>> ListVioladosAsync(CancellationToken ct)
    {
        List<Covenant> list = await context.Covenants
            .Where(c => c.Status == StatusCovenant.Violado || c.Status == StatusCovenant.EmCura)
            .OrderBy(c => c.ContratoId)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Covenant>> ListVencendoAsync(LocalDate ate, CancellationToken ct)
    {
        List<Covenant> list = await context.Covenants
            .Where(c => c.ProximaVerificacaoEm != null && c.ProximaVerificacaoEm <= ate)
            .OrderBy(c => c.ProximaVerificacaoEm)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public void Add(Covenant c) => context.Covenants.Add(c);

    public void Remove(Covenant c) => context.Covenants.Remove(c);

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
