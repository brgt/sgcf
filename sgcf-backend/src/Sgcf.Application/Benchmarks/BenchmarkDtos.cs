namespace Sgcf.Application.Benchmarks;

public sealed record TaxaBenchmarkDto(
    string TipoBenchmark,
    string DataReferencia,
    decimal TaxaAaPercentual,
    string Fonte);

public sealed record EconomiaBenchmarkItemDto(
    int Ano,
    int Mes,
    int QuantidadeOperacoes,
    decimal EconomiaBrl,
    decimal EconomiaVsBenchmarkBrl);

/// <summary>
/// Resultado agregado do relatório de economia comparativa com benchmark de mercado. GAP-CKP-14.
/// </summary>
public sealed record EconomiaBenchmarkDto(
    string Benchmark,
    IReadOnlyList<EconomiaBenchmarkItemDto> PorMes,
    decimal TotalEconomiaBrl,
    decimal TotalEconomiaVsBenchmarkBrl,
    int TotalOperacoes,
    int OperacoesSemTaxa);
