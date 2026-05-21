using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna a estrutura de capital consolidada: dívida total, Patrimônio Líquido,
/// EBITDA dos últimos 12 meses, Despesa Financeira dos últimos 12 meses e ICR.
/// </summary>
public sealed record GetEstruturaCapitalQuery : IRequest<EnvelopeResponse<EstruturaCapitalDto>>;
