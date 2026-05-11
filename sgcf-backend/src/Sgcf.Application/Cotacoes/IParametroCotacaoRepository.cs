using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public interface IParametroCotacaoRepository
{
    public Task<ParametroCotacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ParametroCotacao>> ListAllAsync(CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ParametroCotacao>> ListAtivosAsync(CancellationToken cancellationToken = default);
    public void Add(ParametroCotacao parametro);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
