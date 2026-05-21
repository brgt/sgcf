using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna os contratos inadimplentes com dias de atraso médio e exposição total em BRL.
/// </summary>
public sealed record GetInadimplenciaQuery : IRequest<EnvelopeResponse<InadimplenciaDto>>;
