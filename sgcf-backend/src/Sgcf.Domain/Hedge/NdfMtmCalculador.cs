namespace Sgcf.Domain.Hedge;

/// <summary>
/// Cálculos puros de Mark-to-Market para NDFs. Sem I/O, sem efeitos colaterais.
/// </summary>
public static class NdfMtmCalculador
{
    /// <summary>
    /// Payoff = Notional × (S_atual − K).
    /// Positivo = empresa recebe; negativo = empresa paga.
    /// </summary>
    public static decimal CalcularMtmForward(decimal notional, decimal strike, decimal spotAtual) =>
        Math.Round(notional * (spotAtual - strike), 6, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Collar payoff com 3 ramos:
    ///   S &lt; K_put  → Notional × (S − K_put)  [negativo]
    ///   K_put ≤ S ≤ K_call → 0
    ///   S &gt; K_call → Notional × (S − K_call) [positivo]
    /// </summary>
    public static decimal CalcularMtmCollar(decimal notional, decimal strikePut, decimal strikeCall, decimal spotAtual)
    {
        if (spotAtual < strikePut)
        {
            return Math.Round(notional * (spotAtual - strikePut), 6, MidpointRounding.AwayFromZero);
        }

        if (spotAtual > strikeCall)
        {
            return Math.Round(notional * (spotAtual - strikeCall), 6, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }
}
