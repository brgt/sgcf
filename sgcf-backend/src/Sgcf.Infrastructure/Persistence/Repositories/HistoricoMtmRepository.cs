using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Hedge;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class HistoricoMtmRepository(SgcfDbContext context) : IHistoricoMtmRepository
{
    public Task<HistoricoMtmDiario?> GetAsync(Guid hedgeId, LocalDate data, CancellationToken ct) =>
        context.HistoricosMtm
            .FirstOrDefaultAsync(h => h.HedgeId == hedgeId && h.DataReferencia == data, ct);

    public async Task<IReadOnlyList<HistoricoMtmDiario>> ListByHedgeIdAsync(
        Guid hedgeId,
        LocalDate de,
        LocalDate ate,
        CancellationToken ct)
    {
        List<HistoricoMtmDiario> list = await context.HistoricosMtm
            .Where(h => h.HedgeId == hedgeId && h.DataReferencia >= de && h.DataReferencia <= ate)
            .OrderBy(h => h.DataReferencia)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public void Add(HistoricoMtmDiario h) => context.HistoricosMtm.Add(h);

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
