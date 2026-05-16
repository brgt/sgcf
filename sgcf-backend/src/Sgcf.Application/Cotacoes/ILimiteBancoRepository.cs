using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Porta de persistência para o agregado <see cref="LimiteBanco"/>.
/// Implementação pertence a Sgcf.Infrastructure.
/// </summary>
public interface ILimiteBancoRepository
{
    public void Add(LimiteBanco limite);
    public void Update(LimiteBanco limite);

    /// <summary>Retorna o limite vigente para banco+modalidade, ou null se não cadastrado.</summary>
    public Task<LimiteBanco?> GetByBancoModalidadeAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<LimiteBanco>> ListAsync(
        Guid? bancoId,
        ModalidadeContrato? modalidade,
        CancellationToken cancellationToken = default);

    public Task<LimiteBanco?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
