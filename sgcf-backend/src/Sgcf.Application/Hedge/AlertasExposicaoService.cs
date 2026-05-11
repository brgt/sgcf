using NodaTime;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge;

public static class AlertasExposicaoService
{
    /// <summary>
    /// Generates exposure alerts for a hedge position.
    /// </summary>
    /// <param name="hedge">The hedge instrument being evaluated.</param>
    /// <param name="spotAtual">Current spot rate (BRL per unit of foreign currency).</param>
    /// <param name="mtmAtual">Current MTM value in BRL (informational; not used in alert logic directly).</param>
    /// <param name="notionalContratoSaldo">Outstanding notional of the underlying FINIMP contract in the same currency as <paramref name="hedge"/>.</param>
    /// <param name="hoje">Calendar date used to calculate days until expiry.</param>
    /// <param name="ptaxD1">PTAX D-1 rate, used to detect large intraday FX moves. Pass <see langword="null"/> if unavailable.</param>
    /// <param name="notionalContrato">Original contract notional for mismatch detection. Pass <see langword="null"/> to skip.</param>
    /// <param name="vencimentoContrato">Contract expiry for mismatch detection. Pass <see langword="null"/> to skip.</param>
    public static IReadOnlyList<string> GerarAlertas(
        InstrumentoHedge hedge,
        decimal spotAtual,
        decimal mtmAtual,
        decimal notionalContratoSaldo,
        LocalDate hoje,
        decimal? ptaxD1 = null,
        decimal? notionalContrato = null,
        LocalDate? vencimentoContrato = null)
    {
        List<string> alertas = new();

        GerarAlertaCobertura(hedge, notionalContratoSaldo, alertas);
        GerarAlertaStrikeProximo(hedge, spotAtual, alertas);
        GerarAlertaVariacaoCambial(spotAtual, ptaxD1, alertas);
        GerarAlertaVencimentoSemRolagem(hedge, hoje, alertas);
        GerarAlertaMismatch(hedge, notionalContrato, vencimentoContrato, alertas);

        return alertas.AsReadOnly();
    }

    private static void GerarAlertaCobertura(
        InstrumentoHedge hedge,
        decimal notionalContratoSaldo,
        List<string> alertas)
    {
        if (notionalContratoSaldo <= 0m)
        {
            return;
        }

        decimal cobertura = hedge.Notional.Valor / notionalContratoSaldo;
        if (cobertura < 0.80m)
        {
            alertas.Add(
                $"COBERTURA_INSUFICIENTE: NDF cobre {cobertura:P0} do saldo — abaixo de 80%.");
        }
    }

    private static void GerarAlertaStrikeProximo(
        InstrumentoHedge hedge,
        decimal spotAtual,
        List<string> alertas)
    {
        if (hedge.Tipo == TipoHedge.NdfForward && hedge.StrikeForward.HasValue)
        {
            decimal distancia = Math.Abs(spotAtual - hedge.StrikeForward.Value) / hedge.StrikeForward.Value;
            if (distancia <= 0.01m)
            {
                alertas.Add(
                    $"STRIKE_PROXIMO: cotação atual ({spotAtual:F4}) está a {distancia:P2} do strike ({hedge.StrikeForward.Value:F4}).");
            }

            return;
        }

        if (hedge.Tipo == TipoHedge.NdfCollar)
        {
            if (hedge.StrikePut.HasValue)
            {
                decimal distPut = Math.Abs(spotAtual - hedge.StrikePut.Value) / hedge.StrikePut.Value;
                if (distPut <= 0.01m)
                {
                    alertas.Add(
                        $"STRIKE_PUT_PROXIMO: cotação atual ({spotAtual:F4}) está a {distPut:P2} do strike put ({hedge.StrikePut.Value:F4}).");
                }
            }

            if (hedge.StrikeCall.HasValue)
            {
                decimal distCall = Math.Abs(spotAtual - hedge.StrikeCall.Value) / hedge.StrikeCall.Value;
                if (distCall <= 0.01m)
                {
                    alertas.Add(
                        $"STRIKE_CALL_PROXIMO: cotação atual ({spotAtual:F4}) está a {distCall:P2} do strike call ({hedge.StrikeCall.Value:F4}).");
                }
            }
        }
    }

    private static void GerarAlertaVariacaoCambial(
        decimal spotAtual,
        decimal? ptaxD1,
        List<string> alertas)
    {
        if (!ptaxD1.HasValue || ptaxD1.Value <= 0m)
        {
            return;
        }

        decimal variacao = Math.Abs(spotAtual - ptaxD1.Value) / ptaxD1.Value;
        if (variacao >= 0.02m)
        {
            string sentido = spotAtual > ptaxD1.Value ? "alta" : "baixa";
            alertas.Add(
                $"VARIACAO_CAMBIAL_ALTA: variação de {variacao:P2} ({sentido}) em relação à PTAX D-1 ({ptaxD1.Value:F4}).");
        }
    }

    private static void GerarAlertaVencimentoSemRolagem(
        InstrumentoHedge hedge,
        LocalDate hoje,
        List<string> alertas)
    {
        int diasParaVencimento = Period.Between(hoje, hedge.DataVencimento, PeriodUnits.Days).Days;
        if (diasParaVencimento is >= 0 and <= 15)
        {
            alertas.Add(
                $"VENCIMENTO_PROXIMO: NDF vence em {diasParaVencimento} dia(s) ({hedge.DataVencimento}) sem rolagem definida.");
        }
    }

    private static void GerarAlertaMismatch(
        InstrumentoHedge hedge,
        decimal? notionalContrato,
        LocalDate? vencimentoContrato,
        List<string> alertas)
    {
        if (notionalContrato.HasValue && hedge.Notional.Valor > notionalContrato.Value)
        {
            alertas.Add(
                $"MISMATCH_NOTIONAL: notional do NDF ({hedge.Notional.Valor:N2}) excede saldo do contrato ({notionalContrato.Value:N2}).");
        }

        if (vencimentoContrato.HasValue && hedge.DataVencimento > vencimentoContrato.Value)
        {
            alertas.Add(
                $"MISMATCH_VENCIMENTO: vencimento do NDF ({hedge.DataVencimento}) é posterior ao vencimento do contrato ({vencimentoContrato.Value}).");
        }
    }
}
