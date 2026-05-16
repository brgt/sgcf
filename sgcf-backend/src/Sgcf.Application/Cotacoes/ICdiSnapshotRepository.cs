using NodaTime;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Porta de persistência para snapshots diários de CDI.
/// No MVP o CDI é cadastrado manualmente. SPEC §13 decisão 2.
/// </summary>
public interface ICdiSnapshotRepository
{
    public void Add(CdiSnapshot snapshot);

    /// <summary>Retorna o snapshot de CDI para a data exata, ou null se não cadastrado.</summary>
    public Task<CdiSnapshot?> GetByDataAsync(LocalDate data, CancellationToken cancellationToken = default);

    /// <summary>Retorna o snapshot mais recente disponível em ou antes de <paramref name="dataMaxima"/>.</summary>
    public Task<CdiSnapshot?> GetMaisRecenteAsync(LocalDate dataMaxima, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CdiSnapshot>> ListByPeriodoAsync(
        LocalDate de,
        LocalDate ate,
        CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
