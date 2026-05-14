using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Alertas;
using Sgcf.Domain.Alertas;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class AlertaVencimentoRepository(SgcfDbContext context) : IAlertaVencimentoRepository
{
    public Task<bool> ExisteAsync(Guid contratoId, string tipoAlerta, LocalDate dataVencimento, CancellationToken ct) =>
        context.AlertasVencimento.AnyAsync(
            a => a.ContratoId == contratoId && a.TipoAlerta == tipoAlerta && a.DataVencimento == dataVencimento,
            ct);

    public void Add(AlertaVencimento alerta) => context.AlertasVencimento.Add(alerta);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
