namespace Sgcf.Application.Painel;

/// <summary>Share de dívida por banco credor (para o gráfico de barras/pizza).</summary>
public sealed record ShareBancoDto(Guid BancoId, decimal ValorBrl, decimal PercentualPct);

/// <summary>Variação mês a mês dos principais indicadores de dívida.</summary>
public sealed record KpiComparativoDto(
    decimal DividaTotalBrlMesAnterior,
    decimal DividaLiquidaBrlMesAnterior,
    decimal VariacaoDividaTotalPct,
    decimal VariacaoDividaLiquidaPct);

/// <summary>
/// KPIs executivos do dashboard: endividamento total e líquido, custo médio ponderado,
/// prazo médio remanescente, share por banco e comparativo com o mês anterior.
/// <para>
/// <see cref="DividaEbitda"/> é null quando não há EBITDA cadastrado para o mês corrente.
/// <see cref="Comparativo"/> é null no primeiro mês de operação (nenhum histórico disponível).
/// </para>
/// </summary>
public sealed record KpiDto(
    decimal DividaTotalBrl,
    decimal DividaLiquidaBrl,
    decimal? DividaEbitda,
    IReadOnlyList<ShareBancoDto> SharePorBanco,
    decimal CustoMedioPonderadoAaPct,
    decimal PrazoMedioRemanescenteDias,
    KpiComparativoDto? Comparativo);
