using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna a dívida ativa agregada por modalidade de contrato, com conversão para BRL
/// e cálculo de percentual de participação de cada modalidade no total.
/// </summary>
public sealed record GetBreakdownModalidadeQuery : IRequest<EnvelopeResponse<BreakdownModalidadeDto>>;
