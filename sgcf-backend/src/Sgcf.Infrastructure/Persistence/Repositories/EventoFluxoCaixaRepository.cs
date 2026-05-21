using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Tesouraria;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class EventoFluxoCaixaRepository(SgcfDbContext context)
    : IEventoFluxoCaixaRepository
{
    public async Task<IReadOnlyList<EventoFluxoCaixa>> ListByPeriodoAsync(
        LocalDate dataDe,
        LocalDate dataAte,
        CancellationToken ct = default)
    {
        List<EventoFluxoCaixa> list = await context.EventosFluxoCaixa
            .Where(e => e.Data >= dataDe && e.Data <= dataAte)
            .OrderBy(e => e.Data)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task AddAsync(EventoFluxoCaixa evento, CancellationToken ct = default)
    {
        await context.EventosFluxoCaixa.AddAsync(evento, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
