namespace Sgcf.Application.Painel;

/// <summary>
/// Resultado do breakdown da dívida agrupado por <c>ModalidadeContrato</c>.
/// </summary>
/// <param name="Items">
/// Lista de itens por modalidade, ordenada por <see cref="BreakdownModalidadeItemDto.ValorBrl"/> decrescente.
/// </param>
/// <param name="TotalBrl">
/// Soma de todos os <see cref="BreakdownModalidadeItemDto.ValorBrl"/>.
/// Deve satisfazer: Items.Sum(i =&gt; i.ValorBrl) == TotalBrl (até arredondamento comercial).
/// </param>
public sealed record BreakdownModalidadeDto(
    IReadOnlyList<BreakdownModalidadeItemDto> Items,
    decimal TotalBrl);

/// <summary>
/// Linha do breakdown de dívida para uma única modalidade de contrato.
/// </summary>
/// <param name="Modalidade">Nome do enum <c>ModalidadeContrato</c> (ex: "Finimp", "CapitalDeGiro").</param>
/// <param name="QuantidadeContratos">Número de contratos ativos nessa modalidade.</param>
/// <param name="ValorBrl">Saldo devedor total da modalidade convertido para BRL.</param>
/// <param name="PercentualTotal">
/// Participação percentual no total da carteira. Varia de 0 a 100.
/// A soma dos percentuais de todos os itens é 100 (tolerância de 0,01 por arredondamento).
/// </param>
public sealed record BreakdownModalidadeItemDto(
    string Modalidade,
    int QuantidadeContratos,
    decimal ValorBrl,
    decimal PercentualTotal);
