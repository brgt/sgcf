using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna a agregação de IOF e tarifas dos cronogramas de contratos, agrupada por banco e modalidade.
/// </summary>
public sealed record GetTarifasIofQuery() : IRequest<EnvelopeResponse<TarifasIofDto>>;
