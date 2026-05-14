using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Common;
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

    public async Task<IReadOnlyList<EventoCronograma>> ListPendentesVencendoEmAsync(LocalDate data, CancellationToken cancellationToken)
    {
        List<EventoCronograma> list = await context.EventosCronograma
            .Where(e => e.Status == StatusEventoCronograma.Previsto && e.DataPrevista == data)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<(decimal Valor, Moeda Moeda)>> ListValoresPendentesAsync(CancellationToken cancellationToken)
    {
        List<(decimal, Moeda)> list = await context.EventosCronograma
            .Where(e => e.Status == StatusEventoCronograma.Previsto)
            .Select(e => new ValueTuple<decimal, Moeda>(e.ValorMoedaOriginalDecimal, e.Moeda))
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<EventoCronograma>> ListAbertosParaAnoAsync(
        int ano,
        IReadOnlyCollection<Guid> contratoIds,
        CancellationToken cancellationToken)
    {
        List<EventoCronograma> list = await context.EventosCronograma
            .Where(e => contratoIds.Contains(e.ContratoId)
                     && e.DataPrevista.Year == ano
                     && e.Status != StatusEventoCronograma.Pago
                     && e.Status != StatusEventoCronograma.Cancelado
                     && e.Status != StatusEventoCronograma.Refinanciado)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<EventoCronograma>> ListPrincipaisOrdenadosByContratoIdsAsync(
        IReadOnlyCollection<Guid> contratoIds,
        CancellationToken cancellationToken)
    {
        List<EventoCronograma> list = await context.EventosCronograma
            .Where(e => contratoIds.Contains(e.ContratoId)
                     && e.Tipo == TipoEventoCronograma.Principal)
            .OrderBy(e => e.ContratoId)
            .ThenBy(e => e.DataPrevista)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }
}
