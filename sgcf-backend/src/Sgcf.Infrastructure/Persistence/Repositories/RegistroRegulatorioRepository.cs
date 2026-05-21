using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Conformidade;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class RegistroRegulatorioRepository(SgcfDbContext context) : IRegistroRegulatorioRepository
{
    public async Task<IReadOnlyList<RegistroRegulatorio>> ListByContratoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        List<RegistroRegulatorio> list = await context.Set<RegistroRegulatorio>()
            .Where(r => r.ContratoId == contratoId)
            .OrderBy(r => r.CriadoEm)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<RegistroRegulatorio>> ListPendentesAsync(
        CancellationToken cancellationToken)
    {
        // Retorna Pendente e EmAnalise — ambos requerem ação ou acompanhamento.
        List<RegistroRegulatorio> list = await context.Set<RegistroRegulatorio>()
            .Where(r => r.Status == StatusRegistroRegulatorio.Pendente
                        || r.Status == StatusRegistroRegulatorio.EmAnalise)
            .OrderBy(r => r.ContratoId)
            .ThenBy(r => r.CriadoEm)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public async Task<RegistroRegulatorio?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.Set<RegistroRegulatorio>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(RegistroRegulatorio registro, CancellationToken cancellationToken)
        => await context.Set<RegistroRegulatorio>().AddAsync(registro, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(RegistroRegulatorio registro, CancellationToken cancellationToken)
    {
        context.Set<RegistroRegulatorio>().Remove(registro);
        return Task.CompletedTask;
    }
}
