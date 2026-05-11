using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Dados de entrada para geração do cronograma SAC (Sistema de Amortização Constante).
/// Utilizado em contratos Lei 4.131/62 com amortizações periódicas constantes.
/// </summary>
public sealed record EntradaSac(
    Money ValorPrincipal,
    Percentual TaxaAa,
    BaseCalculo BaseCalculo,
    LocalDate DataDesembolso,
    LocalDate DataVencimento,
    int NumeroParcelas,
    Percentual? AliqIrrf
);
