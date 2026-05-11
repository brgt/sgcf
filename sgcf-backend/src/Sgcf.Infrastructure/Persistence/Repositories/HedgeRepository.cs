using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Hedge;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class HedgeRepository(SgcfDbContext context) : IHedgeRepository
{
    public async Task<IReadOnlyList<InstrumentoHedge>> ListByContratoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        List<InstrumentoHedge> list = await context.InstrumentosHedge
            .Where(h => h.ContratoId == contratoId)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public Task<InstrumentoHedge?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.InstrumentosHedge.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InstrumentoHedge>> ListAtivosAsync(CancellationToken cancellationToken)
    {
        List<InstrumentoHedge> list = await context.InstrumentosHedge
            .Where(h => h.Status == StatusHedge.Ativo)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public Task<PosicaoSnapshot?> GetSnapshotMaisRecenteAsync(Guid hedgeId, CancellationToken cancellationToken) =>
        context.PosicoesSnapshot
            .Where(s => s.HedgeId == hedgeId)
            .OrderByDescending(s => s.CalculadoEm)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<InstrumentoHedge>> ListAtivosVencendoEmAsync(
        LocalDate data,
        CancellationToken cancellationToken)
    {
        List<InstrumentoHedge> list = await context.InstrumentosHedge
            .Where(h => h.Status == StatusHedge.Ativo && h.DataVencimento == data)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public void Add(InstrumentoHedge hedge) => context.InstrumentosHedge.Add(hedge);

    public void AddSnapshot(PosicaoSnapshot snapshot) => context.PosicoesSnapshot.Add(snapshot);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
