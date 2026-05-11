namespace Sgcf.Application.Painel;

/// <summary>Totais de principal e juros a vencer em um mês específico.</summary>
public sealed record MesVencimentoDto(
    int Ano,
    int Mes,
    decimal TotalPrincipalBrl,
    decimal TotalJurosBrl,
    decimal TotalBrl,
    int QuantidadeParcelas);

/// <summary>
/// Calendário de vencimentos de parcelas abertas para um ano civil,
/// agrupadas por mês, com totais em BRL (convertidos via spot ou PTAX).
/// </summary>
public sealed record CalendarioVencimentosDto(
    int Ano,
    IReadOnlyList<MesVencimentoDto> Meses,
    decimal TotalAnoBrl);
