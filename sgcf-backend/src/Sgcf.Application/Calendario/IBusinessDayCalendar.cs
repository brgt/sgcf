using NodaTime;

using Sgcf.Domain.Calendario;

namespace Sgcf.Application.Calendario;

/// <summary>
/// Serviço de consulta de dias úteis baseado no calendário de feriados
/// persistido. Considera sábados, domingos e feriados como dias não úteis.
/// Sempre opera no escopo <see cref="EscopoFeriado.Brasil"/> (calendário
/// nacional ANBIMA) — feriados regionais são ignorados pelo motor de
/// cronograma por decisão arquitetural (ADR Calendário).
/// </summary>
public interface IBusinessDayCalendar
{
    public Task<bool> IsBusinessDayAsync(LocalDate data, CancellationToken ct = default);

    public Task<LocalDate> NextBusinessDayAsync(LocalDate data, CancellationToken ct = default);

    public Task<LocalDate> PreviousBusinessDayAsync(LocalDate data, CancellationToken ct = default);

    public Task<LocalDate> AddBusinessDaysAsync(LocalDate data, int n, CancellationToken ct = default);

    public Task<int> CountBusinessDaysAsync(LocalDate inicio, LocalDate fim, CancellationToken ct = default);

    public Task<LocalDate> AjustarPorConvencaoAsync(
        LocalDate data,
        ConvencaoDataNaoUtil convencao,
        CancellationToken ct = default);
}
