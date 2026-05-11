using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Despacha o cálculo de simulação de antecipação para a estratégia correta com base no padrão do banco.
/// </summary>
public static class AntecipacaoStrategyDispatcher
{
    /// <summary>
    /// Seleciona e executa a estratégia de antecipação correspondente ao padrão configurado no banco.
    /// </summary>
    public static ResultadoSimulacaoAntecipacao Calcular(
        PadraoAntecipacao padrao,
        EntradaSimulacaoAntecipacao entrada,
        Banco banco)
    {
        return padrao switch
        {
            PadraoAntecipacao.A => PadraoAStrategy.Calcular(
                entrada,
                banco.BreakFundingFeePct ?? throw new InvalidOperationException("BreakFundingFeePct não configurado para Padrão A"),
                banco.ExigeAnuenciaExpressa),

            PadraoAntecipacao.B => PadraoBStrategy.Calcular(entrada, banco.ExigeAnuenciaExpressa),

            PadraoAntecipacao.C => PadraoCStrategy.Calcular(entrada, banco.ExigeParcelaInteira),

            PadraoAntecipacao.D => PadraoDStrategy.Calcular(
                entrada,
                banco.TlaPctSobreSaldo ?? throw new InvalidOperationException("TlaPctSobreSaldo não configurado para Padrão D"),
                banco.TlaPctPorMesRemanescente ?? throw new InvalidOperationException("TlaPctPorMesRemanescente não configurado para Padrão D")),

            PadraoAntecipacao.E => PadraoEStrategy.Calcular(entrada, null),

            _ => throw new ArgumentOutOfRangeException(nameof(padrao), padrao, "Padrão de antecipação não suportado.")
        };
    }
}
