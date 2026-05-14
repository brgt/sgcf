using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Dados de entrada para cronograma bullet com juros periódicos.
/// Principal único no vencimento; juros pagos a cada <see cref="PeriodoJurosMeses"/> meses.
/// Utilizado em FINIMP 720 dias (BB) e contratos Lei 4.131 com cupons semestrais.
/// </summary>
public sealed record EntradaBulletComJuros(
    Money ValorPrincipal,
    Percentual TaxaAa,
    BaseCalculo BaseCalculo,
    LocalDate DataDesembolso,
    LocalDate DataVencimento,
    int PeriodoJurosMeses,
    Percentual? AliqIrrf = null,
    Percentual? AliqIofCambio = null,
    decimal? TarifaRofBrl = null,
    decimal? TarifaCadempBrl = null
);
