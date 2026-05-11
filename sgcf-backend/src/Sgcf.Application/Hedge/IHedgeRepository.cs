using NodaTime;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge;

public interface IHedgeRepository
{
    public Task<IReadOnlyList<InstrumentoHedge>> ListByContratoAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<InstrumentoHedge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all hedges with <see cref="StatusHedge.Ativo"/>.</summary>
    public Task<IReadOnlyList<InstrumentoHedge>> ListAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent snapshot for the given hedge, or <see langword="null"/> if none exists.</summary>
    public Task<PosicaoSnapshot?> GetSnapshotMaisRecenteAsync(Guid hedgeId, CancellationToken cancellationToken = default);

    /// <summary>Returns all active hedges whose <c>DataVencimento</c> equals <paramref name="data"/>.</summary>
    public Task<IReadOnlyList<InstrumentoHedge>> ListAtivosVencendoEmAsync(LocalDate data, CancellationToken cancellationToken = default);

    public void Add(InstrumentoHedge hedge);
    public void AddSnapshot(PosicaoSnapshot snapshot);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
