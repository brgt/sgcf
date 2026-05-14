using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Alertas;
using Sgcf.Domain.Alertas;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class AlertaExposicaoBancoRepository(SgcfDbContext context) : IAlertaExposicaoBancoRepository
{
    public Task<bool> ExisteAsync(Guid bancoId, LocalDate dataAlerta, CancellationToken ct) =>
        context.AlertasExposicaoBanco.AnyAsync(
            a => a.BancoId == bancoId && a.DataAlerta == dataAlerta,
            ct);

    public void Add(AlertaExposicaoBanco alerta) => context.AlertasExposicaoBanco.Add(alerta);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
