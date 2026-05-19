namespace Sgcf.Application.Simulacao.Dtos;

/// <summary>
/// Resultado completo do preview de cronograma hipotético (endpoint Task 2.5/2.6).
/// Inclui a lista de eventos e um sumário financeiro para exibição rápida no frontend.
/// </summary>
public sealed record CronogramaHipoteticoDto(
    decimal TaxaEfetivaAaPercentual,
    int QuantidadeEventos,
    decimal PrincipalTotal,
    decimal JurosTotal,
    IReadOnlyList<EventoCronogramaItemDto> Eventos);

/// <summary>
/// Item individual de evento no cronograma hipotético.
/// </summary>
public sealed record EventoCronogramaItemDto(
    int Numero,
    string Tipo,
    DateOnly Data,
    decimal Valor,
    decimal? SaldoDevedorApos);
