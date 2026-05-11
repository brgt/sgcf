using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna o calendário de vencimentos de parcelas abertas para um ano civil, agrupadas por mês,
/// com totais em BRL (convertidos via spot ou PTAX). Filtros opcionais por banco, modalidade e moeda.
/// </summary>
public sealed record GetCalendarioVencimentosQuery(
    int Ano,
    Guid? BancoId = null,
    string? Modalidade = null,
    string? Moeda = null) : IRequest<CalendarioVencimentosDto>;
