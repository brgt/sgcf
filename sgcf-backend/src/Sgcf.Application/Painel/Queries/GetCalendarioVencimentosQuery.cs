using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna o calendário de vencimentos de parcelas abertas para um ano civil, agrupadas por mês,
/// com totais em BRL (convertidos via spot ou PTAX). Filtros opcionais por banco, modalidade e moeda.
/// Quando <paramref name="CdiAnualPct"/> é informado, o campo <c>JurosBrlProjetado</c> é calculado
/// para contratos indexados ao CDI cujos eventos de juros foram importados com valor zero.
/// </summary>
public sealed record GetCalendarioVencimentosQuery(
    int Ano,
    Guid? BancoId = null,
    string? Modalidade = null,
    string? Moeda = null,
    decimal? CdiAnualPct = null) : IRequest<CalendarioVencimentosDto>;
