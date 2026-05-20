using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio;

public interface IParametroCotacaoRepository
{
    public Task<ParametroCotacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ParametroCotacao>> ListAllAsync(CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ParametroCotacao>> ListAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se já existe ao menos um parâmetro de cotação para o tenant informado.
    /// Usado pelo provisionador para garantir idempotência no seed inicial.
    /// </summary>
    public Task<bool> ExisteParaTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    public void Add(ParametroCotacao parametro);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
