namespace Sgcf.Domain.Common;

/// <summary>
/// Tipo de operação de antecipação de pagamento (Anexo C §1).
/// </summary>
public enum TipoAntecipacao
{
    /// <summary>Quita 100% do saldo restante; encerra o contrato.</summary>
    LiquidacaoTotalAntecipada = 1,

    /// <summary>Paga parte do principal, mantém valor das parcelas, encurta prazo.</summary>
    LiquidacaoParcialReducaoPrazo = 2,

    /// <summary>Paga parte do principal, mantém prazo, reduz valor das parcelas.</summary>
    LiquidacaoParcialReducaoParcela = 3,

    /// <summary>Antecipa uma parcela inteira sem alterar a estrutura.</summary>
    AmortizacaoExtraordinariaAvulsa = 4,

    /// <summary>Liquida com recursos de nova operação no mesmo banco (pode ter isenções).</summary>
    RefinanciamentoInterno = 5
}
