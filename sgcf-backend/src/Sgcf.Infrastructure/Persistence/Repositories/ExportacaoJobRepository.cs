using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Exportacao;
using Sgcf.Domain.Exportacao;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class ExportacaoJobRepository(SgcfDbContext context) : IExportacaoJobRepository
{
    public async Task<ExportacaoJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.Set<ExportacaoJob>().FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<ExportacaoJob>> ListPendentesAsync(CancellationToken cancellationToken)
    {
        List<ExportacaoJob> list = await context.Set<ExportacaoJob>()
            .Where(j => j.Status == StatusExportacao.Pendente)
            .OrderBy(j => j.CriadoEm)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public async Task AddAsync(ExportacaoJob job, CancellationToken cancellationToken)
        => await context.Set<ExportacaoJob>().AddAsync(job, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);
}
