using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade;

public interface IRegistroRegulatorioRepository
{
    public Task<IReadOnlyList<RegistroRegulatorio>> ListByContratoAsync(Guid contratoId, CancellationToken cancellationToken);
    public Task<IReadOnlyList<RegistroRegulatorio>> ListPendentesAsync(CancellationToken cancellationToken);
    public Task<RegistroRegulatorio?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task AddAsync(RegistroRegulatorio registro, CancellationToken cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken);
    public Task DeleteAsync(RegistroRegulatorio registro, CancellationToken cancellationToken);
}
