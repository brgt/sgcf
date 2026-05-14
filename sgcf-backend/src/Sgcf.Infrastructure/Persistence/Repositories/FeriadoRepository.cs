using Microsoft.EntityFrameworkCore;

using NodaTime;

using Sgcf.Application.Calendario;
using Sgcf.Domain.Calendario;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class FeriadoRepository(SgcfDbContext context) : IFeriadoRepository
{
    public async Task<IReadOnlyList<Feriado>> ListByYearAsync(
        int ano,
        EscopoFeriado? escopo,
        CancellationToken ct = default)
    {
        IQueryable<Feriado> q = context.Feriados.AsNoTracking()
            .Where(f => f.AnoReferencia == ano);

        if (escopo.HasValue)
        {
            q = q.Where(f => f.Escopo == escopo.Value);
        }

        List<Feriado> list = await q.OrderBy(f => f.Data).ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Feriado>> ListByRangeAsync(
        LocalDate inicio,
        LocalDate fim,
        EscopoFeriado? escopo,
        CancellationToken ct = default)
    {
        if (inicio > fim)
        {
            throw new ArgumentException(
                $"inicio ({inicio}) não pode ser posterior a fim ({fim}).",
                nameof(inicio));
        }

        IQueryable<Feriado> q = context.Feriados.AsNoTracking()
            .Where(f => f.Data >= inicio && f.Data <= fim);

        if (escopo.HasValue)
        {
            q = q.Where(f => f.Escopo == escopo.Value);
        }

        List<Feriado> list = await q.OrderBy(f => f.Data).ToListAsync(ct);
        return list.AsReadOnly();
    }

    public void Add(Feriado feriado) => context.Feriados.Add(feriado);

    public Task<Feriado?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.Feriados.FirstOrDefaultAsync(f => f.Id == id, ct);

    public void Remove(Feriado feriado) => context.Feriados.Remove(feriado);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
