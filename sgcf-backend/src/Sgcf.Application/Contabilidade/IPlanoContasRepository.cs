using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

public interface IPlanoContasRepository
{
    public Task<PlanoContasGerencial?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<PlanoContasGerencial?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<PlanoContasGerencial>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna contas filtrando por texto livre (ILIKE em CodigoGerencial e Nome) e/ou situação (Ativo).
    /// Quando ambos os parâmetros são nulos, comporta-se como <see cref="ListAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<PlanoContasGerencial>> ListFilteredAsync(string? search, bool? ativo, CancellationToken cancellationToken = default);
    public void Add(PlanoContasGerencial conta);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
