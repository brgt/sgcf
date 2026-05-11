using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

public interface IPlanoContasRepository
{
    public Task<PlanoContasGerencial?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<PlanoContasGerencial?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<PlanoContasGerencial>> ListAllAsync(CancellationToken cancellationToken = default);
    public void Add(PlanoContasGerencial conta);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
