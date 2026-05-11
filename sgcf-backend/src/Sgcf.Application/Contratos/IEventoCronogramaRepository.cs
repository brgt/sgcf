using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos;

public interface IEventoCronogramaRepository
{
    public Task<IReadOnlyList<EventoCronograma>> GetByContratoIdAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public void Add(EventoCronograma evento);
    public void AddRange(IEnumerable<EventoCronograma> eventos);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    public Task<bool> ExistsForContratoAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task DeleteAllByContratoIdAsync(Guid contratoId, CancellationToken cancellationToken = default);
}
