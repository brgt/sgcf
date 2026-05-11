namespace Sgcf.Application.Painel;

/// <summary>
/// Recomendação de antecipação para um contrato específico do portfólio,
/// ordenada por economia líquida estimada.
/// </summary>
public sealed record RecomendacaoAntecipacaoDto(
    Guid ContratoId,
    string NumeroExterno,
    string Banco,
    string Modalidade,
    decimal EconomiaLiquidaBrl,
    decimal CustoPrepagamentoBrl,
    decimal ValorTotalAntecipacaoBrl,
    string JustificativaOtimizacao,
    IReadOnlyList<string> Restricoes);

/// <summary>
/// Resultado da simulação de antecipação de portfólio: ranking dos top-5 contratos
/// com maior economia líquida, considerando custo de oportunidade do CDI.
/// </summary>
public sealed record ResultadoAntecipacaoPortfolioDto(
    decimal CaixaDisponivelBrl,
    IReadOnlyList<RecomendacaoAntecipacaoDto> RankingTop5,
    IReadOnlyList<string> ContratosExcluidos);
