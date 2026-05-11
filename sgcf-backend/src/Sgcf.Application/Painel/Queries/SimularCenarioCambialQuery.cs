using MediatR;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Simula o impacto de variações cambiais sobre a dívida total em BRL.
/// Os deltas são percentuais (ex: -10 para -10%). Zero ou null = sem variação.
/// Além do cenário customizado, retorna pessimista (-10%), realista (sem variação) e otimista (+10%).
/// </summary>
public sealed record SimularCenarioCambialQuery(
    decimal? DeltaUsdPct = null,
    decimal? DeltaEurPct = null,
    decimal? DeltaJpyPct = null,
    decimal? DeltaCnyPct = null) : IRequest<ResultadoCenarioCambialDto>;
