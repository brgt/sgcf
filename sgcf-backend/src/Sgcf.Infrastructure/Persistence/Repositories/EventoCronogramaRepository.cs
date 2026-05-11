using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class EventoCronogramaRepository(SgcfDbContext context) : IEventoCronogramaRepository
{
    public async Task<IReadOnlyList<EventoCronograma>> GetByContratoIdAsync(Guid contratoId, CancellationToken cancellationToken = default)
    {
        List<EventoCronograma> list = await context.Set<EventoCronograma>()
            .Where(e => e.ContratoId == contratoId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public void Add(EventoCronograma evento) => context.Set<EventoCronograma>().Add(evento);

    public void AddRange(IEnumerable<EventoCronograma> eventos) => context.Set<EventoCronograma>().AddRange(eventos);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    public Task<bool> ExistsForContratoAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<EventoCronograma>().AnyAsync(e => e.ContratoId == contratoId, cancellationToken);

    public async Task DeleteAllByContratoIdAsync(Guid contratoId, CancellationToken cancellationToken)
    {
        List<EventoCronograma> existing = await context.Set<EventoCronograma>()
            .Where(e => e.ContratoId == contratoId)
            .ToListAsync(cancellationToken);
        context.Set<EventoCronograma>().RemoveRange(existing);
    }
}
