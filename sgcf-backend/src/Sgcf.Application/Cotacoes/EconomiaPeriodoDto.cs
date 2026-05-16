namespace Sgcf.Application.Cotacoes;

/// <summary>Item de economia por mês no relatório agregado.</summary>
public sealed record EconomiaMesDto(
    int Ano,
    int Mes,
    int QuantidadeOperacoes,
    decimal EconomiaBrutaBrl,
    decimal EconomiaAjustadaCdiBrl);

/// <summary>Subtotal por banco no relatório de economia por período.</summary>
public sealed record EconomiaPorBancoDto(
    Guid BancoId,
    int QuantidadeOperacoes,
    decimal EconomiaBrutaBrl,
    decimal EconomiaAjustadaCdiBrl);

/// <summary>
/// Resultado agregado do relatório de economia por período.
/// SPEC §6.2 GetEconomiaPeriodoQuery.
/// </summary>
public sealed record EconomiaPeriodoDto(
    IReadOnlyList<EconomiaMesDto> PorMes,
    IReadOnlyList<EconomiaPorBancoDto> PorBanco,
    decimal TotalEconomiaBrutaBrl,
    decimal TotalEconomiaAjustadaCdiBrl,
    int TotalOperacoes);
