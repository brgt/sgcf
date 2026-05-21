using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Documentos;
using Sgcf.Domain.Documentos;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class DocumentoContratualRepository(SgcfDbContext context) : IDocumentoContratualRepository
{
    public async Task<IReadOnlyList<DocumentoContratual>> ListByContratoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        List<DocumentoContratual> list = await context.Set<DocumentoContratual>()
            .Where(d => d.ContratoId == contratoId)
            .OrderBy(d => d.Nome)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public async Task<DocumentoContratual?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.Set<DocumentoContratual>().FindAsync([id], cancellationToken);

    public async Task AddAsync(DocumentoContratual documento, CancellationToken cancellationToken)
        => await context.Set<DocumentoContratual>().AddAsync(documento, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(DocumentoContratual documento, CancellationToken cancellationToken)
    {
        context.Set<DocumentoContratual>().Remove(documento);
        return Task.CompletedTask;
    }
}
