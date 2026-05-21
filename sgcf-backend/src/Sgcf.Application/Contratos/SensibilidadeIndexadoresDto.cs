namespace Sgcf.Application.Contratos;

/// <summary>
/// Sensibilidade do portfólio ativo a uma variação de +<see cref="DeltaBps"/> bps nos indexadores.
/// </summary>
/// <param name="DeltaBps">Variação hipotética em basis points (e.g., 100 = +1%).</param>
/// <param name="SaldoDevedorTotalBrl">Saldo devedor consolidado em BRL de todos os contratos ativos.</param>
/// <param name="DeltaCustoAnualTotalBrl">Custo anual adicional total em BRL para o delta informado.</param>
/// <param name="PorIndexador">Breakdown do impacto por grupo de indexador.</param>
public sealed record SensibilidadeIndexadoresDto(
    int DeltaBps,
    decimal SaldoDevedorTotalBrl,
    decimal DeltaCustoAnualTotalBrl,
    IReadOnlyList<SensibilidadePorIndexadorDto> PorIndexador);

/// <summary>
/// Sensibilidade de um grupo de contratos que compartilham o mesmo indexador.
/// </summary>
/// <param name="Indexador">Nome do indexador: "CDI", "SOFR" ou "FIXO".</param>
/// <param name="QuantidadeContratos">Número de contratos neste grupo.</param>
/// <param name="SaldoDevedorBrl">Saldo devedor deste grupo convertido em BRL.</param>
/// <param name="DeltaCustoAnualBrl">Custo anual adicional em BRL para o delta informado.</param>
/// <param name="DeltaCustoAnualPercentual">Proporção do custo adicional deste grupo sobre o total do portfólio.</param>
public sealed record SensibilidadePorIndexadorDto(
    string Indexador,
    int QuantidadeContratos,
    decimal SaldoDevedorBrl,
    decimal DeltaCustoAnualBrl,
    decimal DeltaCustoAnualPercentual);
