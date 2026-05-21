using Sgcf.Domain.Exportacao;

namespace Sgcf.Application.Exportacao;

public interface IExportacaoJobRepository
{
    public Task<ExportacaoJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<IReadOnlyList<ExportacaoJob>> ListPendentesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lista todos os jobs pendentes de todos os tenants.
    /// Uso exclusivo de background services que não têm TenantContext resolvido.
    /// </summary>
    public Task<IReadOnlyList<ExportacaoJob>> ListPendentesTodosTenantsAsync(CancellationToken cancellationToken);

    public Task AddAsync(ExportacaoJob job, CancellationToken cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
