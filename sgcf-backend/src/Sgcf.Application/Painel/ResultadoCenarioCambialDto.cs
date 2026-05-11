namespace Sgcf.Application.Painel;

/// <summary>Impacto de um determinado cenário de câmbio sobre a exposição em uma moeda.</summary>
public sealed record LinhaCenarioMoedaDto(
    string Moeda,
    decimal CotacaoBase,
    decimal CotacaoEstressada,
    decimal DeltaPct,
    decimal SaldoBrlBase,
    decimal SaldoBrlEstressado,
    decimal ImpactoBrl);

/// <summary>
/// Resultado de um cenário de estresse cambial: cotações base vs estressadas,
/// impacto na dívida bruta em BRL e detalhamento por moeda.
/// </summary>
public sealed record CenarioDto(
    string Nome,
    IReadOnlyList<LinhaCenarioMoedaDto> BreakdownPorMoeda,
    decimal DividaBrutaBrl,
    decimal DividaLiquidaBrl,
    decimal DeltaVsRealistaBrl);

/// <summary>
/// Agrupa os quatro cenários pré-calculados (pessimista, realista, otimista) mais o cenário customizado
/// definido pelos parâmetros da query.
/// </summary>
public sealed record ResultadoCenarioCambialDto(
    CenarioDto CenarioCustomizado,
    CenarioDto CenarioPessimista,
    CenarioDto CenarioRealista,
    CenarioDto CenarioOtimista);
