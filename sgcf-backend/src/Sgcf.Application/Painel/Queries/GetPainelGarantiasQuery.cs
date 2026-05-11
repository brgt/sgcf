using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna o painel consolidado de garantias ativas, com distribuição por tipo e por banco,
/// e alertas de vencimento iminente (CDB vencendo em 30 dias, boleto em 2 dias).
/// </summary>
public sealed record GetPainelGarantiasQuery : IRequest<PainelGarantiasDto>;
