using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Despacha o cálculo de simulação de antecipação para a estratégia correta com base no padrão
/// configurado no <see cref="LimiteBanco"/> (banco + modalidade).
/// </summary>
public static class AntecipacaoStrategyDispatcher
{
    /// <summary>
    /// Seleciona e executa a estratégia de antecipação correspondente ao padrão configurado
    /// no limite operacional da modalidade.
    /// Parâmetros institucionais que não variam por modalidade (ExigeAnuenciaExpressa,
    /// ExigeParcelaInteira) ainda são lidos de <paramref name="banco"/>.
    /// </summary>
    public static ResultadoSimulacaoAntecipacao Calcular(
        EntradaSimulacaoAntecipacao entrada,
        LimiteBanco limiteBanco,
        Banco banco)
    {
        PadraoAntecipacao padrao = limiteBanco.PadraoAntecipacao
            ?? throw new InvalidOperationException(
                $"LimiteBanco '{limiteBanco.Id}' não tem PadraoAntecipacao configurado. " +
                "Configure via ConfigurarAntecipacao antes de simular.");

        return padrao switch
        {
            PadraoAntecipacao.A => PadraoAStrategy.Calcular(
                entrada,
                limiteBanco.BreakFundingFeePct ?? throw new InvalidOperationException(
                    $"BreakFundingFeePct não configurado no LimiteBanco '{limiteBanco.Id}' para Padrão A."),
                banco.ExigeAnuenciaExpressa),

            PadraoAntecipacao.B => PadraoBStrategy.Calcular(entrada, banco.ExigeAnuenciaExpressa),

            PadraoAntecipacao.C => PadraoCStrategy.Calcular(entrada, banco.ExigeParcelaInteira),

            PadraoAntecipacao.D => PadraoDStrategy.Calcular(
                entrada,
                limiteBanco.TlaPctSobreSaldo ?? throw new InvalidOperationException(
                    $"TlaPctSobreSaldo não configurado no LimiteBanco '{limiteBanco.Id}' para Padrão D."),
                limiteBanco.TlaPctPorMesRemanescente ?? throw new InvalidOperationException(
                    $"TlaPctPorMesRemanescente não configurado no LimiteBanco '{limiteBanco.Id}' para Padrão D.")),

            PadraoAntecipacao.E => PadraoEStrategy.Calcular(entrada, null),

            _ => throw new InvalidOperationException($"Padrão de antecipação '{padrao}' não suportado.")
        };
    }
}
