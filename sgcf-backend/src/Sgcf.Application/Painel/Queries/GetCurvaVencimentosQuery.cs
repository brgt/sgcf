using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna a curva de vencimentos futuros agrupada em buckets temporais e com breakdown
/// por modalidade. Converte todos os valores para BRL via spot ou PTAX D-1.
/// </summary>
/// <param name="Meses">
/// Horizonte em meses a partir de hoje. Deve ser um de {12, 24, 36, 60};
/// qualquer outro valor é normalizado para 12 pelo handler.
/// </param>
/// <param name="Granularidade">Agrupamento temporal dos buckets.</param>
/// <param name="BancoId">Filtro opcional por banco credor.</param>
/// <param name="Modalidade">Filtro opcional por modalidade de contrato (nome do enum, case-insensitive).</param>
/// <param name="Moeda">Filtro opcional por moeda original do contrato (nome do enum, case-insensitive).</param>
public sealed record GetCurvaVencimentosQuery(
    int Meses,
    GranularidadeHorizonte Granularidade,
    Guid? BancoId = null,
    string? Modalidade = null,
    string? Moeda = null) : IRequest<EnvelopeResponse<CurvaVencimentosDto>>;
