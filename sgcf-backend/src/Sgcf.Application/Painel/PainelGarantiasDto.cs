namespace Sgcf.Application.Painel;

/// <summary>Distribuição de garantias ativas por tipo.</summary>
public sealed record LinhaDistribuicaoTipoDto(
    string Tipo,
    decimal ValorBrl,
    decimal PercentualDoTotal);

/// <summary>Distribuição de garantias ativas por banco credor.</summary>
public sealed record LinhaDistribuicaoBancoDto(
    Guid BancoId,
    decimal ValorBrl,
    decimal PercentualDoTotal);

/// <summary>
/// Painel consolidado das garantias ativas vinculadas a contratos.
/// Inclui distribuição por tipo e por banco, além de alertas de vencimento iminente.
/// </summary>
public sealed record PainelGarantiasDto(
    string DataCalculo,
    decimal TotalGarantiasAtivasBrl,
    IReadOnlyList<LinhaDistribuicaoTipoDto> DistribuicaoPorTipo,
    IReadOnlyList<LinhaDistribuicaoBancoDto> DistribuicaoPorBanco,
    IReadOnlyList<string> Alertas);
