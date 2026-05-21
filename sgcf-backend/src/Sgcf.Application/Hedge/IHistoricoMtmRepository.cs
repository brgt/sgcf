using NodaTime;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge;

/// <summary>
/// Abstração de persistência para <see cref="HistoricoMtmDiario"/>.
/// Os métodos de escrita usam o padrão unit-of-work — <see cref="SaveChangesAsync"/> deve ser
/// chamado explicitamente pelo handler após agrupar todas as operações de escrita.
/// </summary>
public interface IHistoricoMtmRepository
{
    /// <summary>
    /// Retorna o snapshot de MtM para o par (hedge, data) dentro do tenant corrente,
    /// ou <see langword="null"/> quando nenhum registro existir.
    /// </summary>
    public Task<HistoricoMtmDiario?> GetAsync(Guid hedgeId, LocalDate data, CancellationToken ct);

    /// <summary>
    /// Retorna todos os snapshots de MtM do hedge informado no intervalo fechado [de, ate],
    /// ordenados por <c>DataReferencia</c> ascendente.
    /// </summary>
    public Task<IReadOnlyList<HistoricoMtmDiario>> ListByHedgeIdAsync(
        Guid hedgeId,
        LocalDate de,
        LocalDate ate,
        CancellationToken ct);

    /// <summary>Rastreia o snapshot para inserção no próximo <see cref="SaveChangesAsync"/>.</summary>
    public void Add(HistoricoMtmDiario h);

    /// <summary>Persiste as alterações pendentes no contexto EF e retorna o número de linhas afetadas.</summary>
    public Task<int> SaveChangesAsync(CancellationToken ct);
}
