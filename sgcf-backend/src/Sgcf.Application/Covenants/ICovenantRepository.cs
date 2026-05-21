using NodaTime;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants;

public interface ICovenantRepository
{
    public Task<Covenant?> GetAsync(Guid id, CancellationToken ct = default);
    public Task<IReadOnlyList<Covenant>> ListByContratoAsync(Guid contratoId, CancellationToken ct = default);
    public Task<IReadOnlyList<Covenant>> ListVioladosAsync(CancellationToken ct = default);
    public Task<IReadOnlyList<Covenant>> ListVencendoAsync(LocalDate ate, CancellationToken ct = default);
    public void Add(Covenant c);
    public void Remove(Covenant c);
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
