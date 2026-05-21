namespace Sgcf.Application.Painel.EconomiaTributaria;

/// <summary>
/// Agregação acumulada de economia tributária estimada para o período informado.
///
/// <para>
/// O benefício tributário é calculado sobre <see cref="TotalEconomiaAjustadaCdiBrl"/>
/// (economia equalizada por CDI), multiplicando pela alíquota efetiva combinada de
/// IRPJ (15% + adicional 10%) + CSLL (9%) = 34%.
/// </para>
/// </summary>
public sealed record EconomiaTributariaDto(
    int DeAno,
    int DeMes,
    int AteAno,
    int AteMes,
    decimal TotalEconomiaBrl,
    decimal TotalEconomiaAjustadaCdiBrl,
    /// <summary>Benefício tributário estimado: EconomiaAjustadaCdiBrl × 0,34 (IRPJ + CSLL).</summary>
    decimal BeneficioTributarioEstimadoBrl,
    int TotalOperacoes,
    IReadOnlyList<EconomiaTributariaPorBancoDto> PorBanco);

/// <summary>
/// Subtotal de economia tributária estimada para um banco credor específico dentro do período.
/// </summary>
public sealed record EconomiaTributariaPorBancoDto(
    Guid? BancoId,
    decimal EconomiaBrl,
    decimal EconomiaAjustadaCdiBrl,
    decimal BeneficioTributarioEstimadoBrl,
    int Operacoes);
