using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Porta de persistência para <see cref="EconomiaNegociacao"/>.
/// Implementação pertence a Sgcf.Infrastructure.
/// </summary>
public interface IEconomiaRepository
{
    public void Add(EconomiaNegociacao economia);

    public Task<EconomiaNegociacao?> GetByCotacaoIdAsync(Guid cotacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista economias cujo CreatedAt está entre <paramref name="de"/> e <paramref name="ate"/> (inclusivo).
    /// Usado pelo relatório de economia por período. SPEC §6.2 GetEconomiaPeriodoQuery.
    /// </summary>
    public Task<IReadOnlyList<EconomiaNegociacao>> ListByPeriodoAsync(
        YearMonth de,
        YearMonth ate,
        Guid? bancoId,
        CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
