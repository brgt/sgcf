using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Simula a antecipação de portfólio: dado o caixa disponível e uma taxa CDI de referência,
/// ranqueia os top-5 contratos com maior economia líquida (após custo de oportunidade).
/// </summary>
public sealed record SimularAntecipacaoPortfolioQuery(
    decimal CaixaDisponivelBrl,
    decimal? TaxaCdiAa = null) : IRequest<ResultadoAntecipacaoPortfolioDto>;
