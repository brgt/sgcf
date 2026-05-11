namespace Sgcf.Application.Painel;

/// <summary>Breakdown de exposição em uma determinada moeda estrangeira.</summary>
public sealed record LinhaBreakdownMoedaDto(
    string Moeda,
    decimal SaldoMoedaOriginal,
    decimal CotacaoAplicada,
    decimal SaldoBrl,
    int QuantidadeContratos);

/// <summary>Componentes do ajuste MTM líquido sobre os hedges ativos.</summary>
public sealed record AjusteMtmDto(
    decimal MtmAReceberBrl,
    decimal MtmAPagarBrl,
    decimal MtmLiquidoBrl);

/// <summary>
/// Resultado do painel consolidado de dívida (Annexo A §3.1).
/// Apresenta a exposição total em BRL após conversão com cotação spot (ou PTAX fallback)
/// e o ajuste líquido dos instrumentos de hedge ativos.
/// </summary>
public sealed record PainelDividaDto(
    string DataHoraCalculo,
    string TipoCotacao,
    IReadOnlyList<LinhaBreakdownMoedaDto> BreakdownPorMoeda,
    decimal DividaBrutaBrl,
    AjusteMtmDto AjusteMtm,
    decimal DividaLiquidaPosHedgeBrl,
    IReadOnlyList<string> Alertas);
