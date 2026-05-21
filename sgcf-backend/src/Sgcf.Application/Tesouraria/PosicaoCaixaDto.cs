namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Consolidado de posição de caixa para uma data de referência.
/// Agrega saldos por moeda e por banco, convertidos para BRL.
/// </summary>
public sealed record PosicaoCaixaDto(
    string DataReferencia,
    decimal SaldoConsolidadoBrl,
    IReadOnlyList<PosicaoCaixaMoedaDto> PorMoeda,
    IReadOnlyList<PosicaoCaixaBancoDto> PorBanco);

/// <summary>
/// Saldo consolidado por moeda, incluindo taxa de conversão utilizada.
/// </summary>
public sealed record PosicaoCaixaMoedaDto(
    string Moeda,
    decimal SaldoBrl,
    decimal SaldoMoedaOriginal,
    decimal Taxa);

/// <summary>
/// Saldo consolidado por banco, com detalhamento por conta.
/// </summary>
public sealed record PosicaoCaixaBancoDto(
    Guid BancoId,
    string NomeBanco,
    decimal SaldoBrl,
    IReadOnlyList<PosicaoCaixaContaDto> Contas);

/// <summary>
/// Saldo de uma conta individual com conversão para BRL.
/// </summary>
public sealed record PosicaoCaixaContaDto(
    Guid ContaId,
    string NomeConta,
    string Agencia,
    string NumeroConta,
    string Moeda,
    decimal Saldo,
    decimal SaldoBrl);
