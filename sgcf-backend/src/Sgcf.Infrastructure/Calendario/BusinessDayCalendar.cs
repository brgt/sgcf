using System.Collections.Concurrent;

using NodaTime;

using Sgcf.Application.Calendario;
using Sgcf.Domain.Calendario;

namespace Sgcf.Infrastructure.Calendario;

/// <summary>
/// Implementação de <see cref="IBusinessDayCalendar"/> que carrega feriados
/// do repositório sob demanda e mantém um cache em memória por ano. O cache
/// é populado uma vez por ano consultado e reutilizado durante o tempo de
/// vida do escopo de DI.
/// </summary>
internal sealed class BusinessDayCalendar(IFeriadoRepository feriadoRepository) : IBusinessDayCalendar
{
    private readonly ConcurrentDictionary<int, IReadOnlySet<LocalDate>> _porAno = new();

    public async Task<bool> IsBusinessDayAsync(LocalDate data, CancellationToken ct = default)
    {
        IReadOnlySet<LocalDate> feriados = await GetFeriadosAsync(data.Year, ct);
        return BusinessDayCalculator.IsBusinessDay(data, feriados);
    }

    public async Task<LocalDate> NextBusinessDayAsync(LocalDate data, CancellationToken ct = default)
    {
        IReadOnlySet<LocalDate> feriados = await GetFeriadosCobrindoAsync(data.Year, ct);
        return BusinessDayCalculator.NextBusinessDay(data, feriados);
    }

    public async Task<LocalDate> PreviousBusinessDayAsync(LocalDate data, CancellationToken ct = default)
    {
        IReadOnlySet<LocalDate> feriados = await GetFeriadosCobrindoAsync(data.Year, ct);
        return BusinessDayCalculator.PreviousBusinessDay(data, feriados);
    }

    public async Task<LocalDate> AddBusinessDaysAsync(LocalDate data, int n, CancellationToken ct = default)
    {
        IReadOnlySet<LocalDate> feriados = await GetFeriadosCobrindoAsync(data.Year, ct);
        return BusinessDayCalculator.AddBusinessDays(data, n, feriados);
    }

    public async Task<int> CountBusinessDaysAsync(
        LocalDate inicio,
        LocalDate fim,
        CancellationToken ct = default)
    {
        IReadOnlySet<LocalDate> feriados = await GetFeriadosRangeAsync(inicio.Year, fim.Year, ct);
        return BusinessDayCalculator.CountBusinessDays(inicio, fim, feriados);
    }

    public async Task<LocalDate> AjustarPorConvencaoAsync(
        LocalDate data,
        ConvencaoDataNaoUtil convencao,
        CancellationToken ct = default)
    {
        IReadOnlySet<LocalDate> feriados = await GetFeriadosCobrindoAsync(data.Year, ct);
        return BusinessDayCalculator.AjustarPorConvencao(data, convencao, feriados);
    }

    /// <summary>
    /// Carrega feriados nacionais do ano informado em cache.
    /// </summary>
    private async Task<IReadOnlySet<LocalDate>> GetFeriadosAsync(int ano, CancellationToken ct)
    {
        if (_porAno.TryGetValue(ano, out IReadOnlySet<LocalDate>? cached))
        {
            return cached;
        }

        IReadOnlyList<Feriado> registros = await feriadoRepository.ListByYearAsync(
            ano, EscopoFeriado.Brasil, ct);

        HashSet<LocalDate> set = registros
            .Where(static f => f.Escopo == EscopoFeriado.Brasil)
            .Select(static f => f.Data)
            .ToHashSet();

        return _porAno.GetOrAdd(ano, set);
    }

    /// <summary>
    /// Carrega o ano informado mais o ano anterior e seguinte, para suportar
    /// operações que atravessam fronteira de ano (ex.: AddBusinessDays com
    /// data próxima ao fim do ano).
    /// </summary>
    private async Task<IReadOnlySet<LocalDate>> GetFeriadosCobrindoAsync(int ano, CancellationToken ct)
    {
        return await GetFeriadosRangeAsync(ano - 1, ano + 1, ct);
    }

    private async Task<IReadOnlySet<LocalDate>> GetFeriadosRangeAsync(
        int anoInicio,
        int anoFim,
        CancellationToken ct)
    {
        HashSet<LocalDate> resultado = [];
        for (int ano = anoInicio; ano <= anoFim; ano++)
        {
            IReadOnlySet<LocalDate> doAno = await GetFeriadosAsync(ano, ct);
            foreach (LocalDate d in doAno)
            {
                resultado.Add(d);
            }
        }
        return resultado;
    }
}
