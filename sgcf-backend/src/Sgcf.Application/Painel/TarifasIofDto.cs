namespace Sgcf.Application.Painel;

/// <summary>
/// Resultado da agregação de IOF e tarifas dos cronogramas de contratos.
/// </summary>
public sealed record TarifasIofDto(
    decimal TotalIofBrl,
    decimal TotalTarifasBrl,
    decimal TotalGeralBrl,
    IReadOnlyList<TarifasIofPorBancoDto> PorBanco,
    IReadOnlyList<TarifasIofPorModalidadeDto> PorModalidade);

/// <summary>
/// Subtotal de IOF e tarifas para um banco específico.
/// </summary>
public sealed record TarifasIofPorBancoDto(
    Guid BancoId,
    string NomeBanco,
    decimal TotalIofBrl,
    decimal TotalTarifasBrl,
    decimal TotalBrl);

/// <summary>
/// Subtotal de IOF e tarifas para uma modalidade de contrato.
/// </summary>
public sealed record TarifasIofPorModalidadeDto(
    string Modalidade,
    decimal TotalIofBrl,
    decimal TotalTarifasBrl,
    decimal TotalBrl);
