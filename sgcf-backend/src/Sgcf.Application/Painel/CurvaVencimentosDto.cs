namespace Sgcf.Application.Painel;

/// <summary>
/// Granularidade do agrupamento temporal nos buckets da curva de vencimentos.
/// </summary>
public enum GranularidadeHorizonte
{
    /// <summary>Agrupa por mês — label <c>YYYY-MM</c> (ex: <c>2026-03</c>).</summary>
    Mes,

    /// <summary>Agrupa por trimestre — label <c>YYYY-Qx</c> (ex: <c>2026-Q1</c>).</summary>
    Trimestre,

    /// <summary>Agrupa por ano — label <c>YYYY</c> (ex: <c>2026</c>).</summary>
    Ano
}

/// <summary>
/// Curva de vencimentos agregada por buckets temporais, com breakdown por modalidade.
/// </summary>
/// <param name="Buckets">Lista de buckets ordenados cronologicamente.</param>
/// <param name="TotalBrl">Soma de todos os buckets em BRL — deve ser igual a <c>Buckets.Sum(b => b.TotalBrl)</c>.</param>
public sealed record CurvaVencimentosDto(
    IReadOnlyList<BucketVencimentoDto> Buckets,
    decimal TotalBrl);

/// <summary>
/// Um bucket temporal da curva de vencimentos.
/// </summary>
/// <param name="Label">
/// Rótulo do bucket conforme granularidade: <c>"2026-01"</c>, <c>"2026-Q1"</c> ou <c>"2026"</c>.
/// </param>
/// <param name="TotalBrl">Total em BRL de todos os eventos do bucket.</param>
/// <param name="PorModalidade">Breakdown por modalidade de contrato dentro do bucket.</param>
public sealed record BucketVencimentoDto(
    string Label,
    decimal TotalBrl,
    IReadOnlyList<BucketModalidadeDto> PorModalidade);

/// <summary>
/// Valor agregado em BRL para uma modalidade de contrato dentro de um bucket.
/// </summary>
/// <param name="Modalidade">Nome da modalidade (ex: <c>"CapitalDeGiro"</c>, <c>"Finimp"</c>).</param>
/// <param name="ValorBrl">Total em BRL de eventos dessa modalidade no bucket.</param>
public sealed record BucketModalidadeDto(string Modalidade, decimal ValorBrl);
