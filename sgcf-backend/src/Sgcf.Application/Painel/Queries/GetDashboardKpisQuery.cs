using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna os KPIs executivos do dashboard: dívida total e líquida, custo médio ponderado,
/// prazo médio remanescente, share por banco, índice Dívida/EBITDA e comparativo com mês anterior.
/// </summary>
public sealed record GetDashboardKpisQuery : IRequest<KpiDto>;
