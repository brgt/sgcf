using Sgcf.Domain.Exportacao;

namespace Sgcf.Application.Exportacao;

public interface IExportacaoJobRepository
{
    public Task<ExportacaoJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<IReadOnlyList<ExportacaoJob>> ListPendentesAsync(CancellationToken cancellationToken);
    public Task AddAsync(ExportacaoJob job, CancellationToken cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
