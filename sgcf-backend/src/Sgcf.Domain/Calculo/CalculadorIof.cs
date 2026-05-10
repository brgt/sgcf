using Sgcf.Domain.Common;

namespace Sgcf.Domain.Calculo;

/// <summary>
/// Cálculo de IOF câmbio sobre operações de financiamento estrangeiro.
/// </summary>
public static class CalculadorIof
{
    /// <summary>
    /// Alíquota padrão de IOF câmbio: 0,38% (vigente desde 2013).
    /// </summary>
    public static readonly Percentual AliquotaPadrao = Percentual.De(0.38m);

    /// <summary>
    /// Calcula IOF sobre o valor principal em BRL.
    /// IOF incide sobre o principal convertido para BRL na data do desembolso.
    /// </summary>
    public static Money CalcularIof(Money principalBrl, Percentual aliquota)
    {
        if (principalBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("IOF câmbio calcula sobre valor em BRL.", nameof(principalBrl));
        }

        return principalBrl.Multiplicar(aliquota.AsDecimal);
    }
}
