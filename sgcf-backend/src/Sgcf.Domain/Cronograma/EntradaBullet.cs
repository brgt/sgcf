using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Dados de entrada para geração do cronograma bullet (amortização única no vencimento).
/// </summary>
public sealed record EntradaBullet(
    Money ValorPrincipal,
    Percentual TaxaAa,
    BaseCalculo BaseCalculo,
    LocalDate DataDesembolso,
    LocalDate DataVencimento,
    Percentual? AliqIrrf,
    Percentual? AliqIofCambio,
    decimal? TarifaRofBrl,
    decimal? TarifaCadempBrl
);
