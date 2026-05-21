using Sgcf.Domain.Documentos;

namespace Sgcf.Application.Documentos;

public interface IDocumentoContratualRepository
{
    public Task<IReadOnlyList<DocumentoContratual>> ListByContratoAsync(Guid contratoId, CancellationToken cancellationToken);
    public Task<DocumentoContratual?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task AddAsync(DocumentoContratual documento, CancellationToken cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken);
    public Task DeleteAsync(DocumentoContratual documento, CancellationToken cancellationToken);
}
