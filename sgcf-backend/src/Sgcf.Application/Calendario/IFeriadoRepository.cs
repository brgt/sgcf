using NodaTime;

using Sgcf.Domain.Calendario;

namespace Sgcf.Application.Calendario;

/// <summary>
/// Acesso de persistência aos feriados utilizados pelo motor de dias úteis.
/// </summary>
public interface IFeriadoRepository
{
    /// <summary>
    /// Lista feriados de um ano específico, opcionalmente filtrados por escopo
    /// geográfico. Quando <paramref name="escopo"/> é nulo, retorna todos.
    /// </summary>
    public Task<IReadOnlyList<Feriado>> ListByYearAsync(
        int ano,
        EscopoFeriado? escopo,
        CancellationToken ct = default);

    /// <summary>
    /// Lista feriados no intervalo fechado [inicio, fim], opcionalmente
    /// filtrados por escopo. Quando <paramref name="escopo"/> é nulo, retorna todos.
    /// </summary>
    public Task<IReadOnlyList<Feriado>> ListByRangeAsync(
        LocalDate inicio,
        LocalDate fim,
        EscopoFeriado? escopo,
        CancellationToken ct = default);

    /// <summary>
    /// Retorna true se já existe um feriado com exatamente a mesma data, tipo e escopo.
    /// </summary>
    public Task<bool> ExistsAsync(
        LocalDate data,
        TipoFeriado tipo,
        EscopoFeriado escopo,
        CancellationToken ct = default);

    public void Add(Feriado feriado);

    public Task<Feriado?> GetByIdAsync(Guid id, CancellationToken ct = default);

    public void Remove(Feriado feriado);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
