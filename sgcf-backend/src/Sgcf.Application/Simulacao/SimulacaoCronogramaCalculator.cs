using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao;

/// <summary>
/// Calcula o cronograma hipotético de uma <see cref="SimulacaoContratacao"/>
/// reutilizando o motor de cronograma existente sem qualquer I/O ou efeito colateral.
///
/// <para>
/// <b>Garantia AD-4:</b> o cronograma produzido aqui é bit-a-bit idêntico
/// ao que <c>CronogramaStrategyFactory.Criar(estrutura).Gerar(input)</c>
/// produziria para os mesmos parâmetros. Isso elimina divergência entre
/// simulação e contrato real após conversão.
/// </para>
///
/// <para>
/// <b>Decisão CDI+Spread (SPEC §6.5):</b> a taxa efetiva é calculada
/// por composição — <c>(1+CDI) × (1+spread) - 1</c> — não por adição simples.
/// Essa é a convenção de mercado brasileiro para operações NCE/Capital de Giro
/// indexadas ao CDI. O CDI é "congelado" no momento do cálculo (snapshot),
/// não atualizado periodicamente. CDI indexado em runtime é escopo futuro.
/// </para>
///
/// <para>
/// <b>BulletComJurosPeriodicos:</b> a periodicidade do cupom de juros é mapeada
/// da mesma <c>Periodicidade</c> da simulação. Essa é a única estrutura onde
/// os pagamentos de juros ocorrem em datas diferentes do principal.
/// </para>
/// </summary>
public static class SimulacaoCronogramaCalculator
{
    /// <summary>
    /// Gera o cronograma hipotético da simulação.
    /// </summary>
    /// <param name="simulacao">
    /// Simulação de contratação com todos os parâmetros de estrutura.
    /// </param>
    /// <param name="cdiReferenciaAaPercentual">
    /// CDI de referência em percentual ao ano (ex: 10.50 para 10,50% a.a.).
    /// Obrigatório quando <see cref="SimulacaoContratacao.TipoTaxa"/> é
    /// <see cref="TipoTaxa.CdiSpread"/>; ignorado para taxa fixa.
    /// </param>
    /// <returns>
    /// Lista imutável de eventos gerados, idêntica à produzida pelo motor de cronograma.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Lançado quando <see cref="TipoTaxa.CdiSpread"/> e
    /// <paramref name="cdiReferenciaAaPercentual"/> não for informado.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Lançado quando <see cref="TipoTaxa.Fixa"/> e
    /// <see cref="SimulacaoContratacao.TaxaAa"/> for nulo
    /// (invariante I-6 deveria impedir esse estado, mas o calculador se protege).
    /// </exception>
    public static IReadOnlyList<EventoCronogramaGerado> Calcular(
        SimulacaoContratacao simulacao,
        decimal? cdiReferenciaAaPercentual = null)
    {
        ArgumentNullException.ThrowIfNull(simulacao);

        Percentual taxaEfetiva = ResolverTaxaEfetiva(simulacao, cdiReferenciaAaPercentual);

        // BulletComJurosPeriodicos exige PeriodicidadeJuros.
        // Mapeamos da Periodicidade da simulação — o cupom semestral tem a mesma
        // periodicidade da amortização nessa estrutura.
        Periodicidade? periodicidadeJuros = simulacao.EstruturaAmortizacao ==
            EstruturaAmortizacao.BulletComJurosPeriodicos
                ? simulacao.Periodicidade
                : null;

        GerarCronogramaInput input = new(
            ValorPrincipal: simulacao.ValorPrincipal,
            TaxaAa: taxaEfetiva,
            BaseCalculo: simulacao.BaseCalculo,
            DataDesembolso: simulacao.DataContratacaoPrevista,
            DataPrimeiroVencimento: simulacao.DataPrimeiroVencimento,
            QuantidadeParcelas: simulacao.QuantidadeParcelas,
            Periodicidade: simulacao.Periodicidade,
            AnchorDiaMes: simulacao.AnchorDiaMes,
            AnchorDiaFixo: simulacao.AnchorDiaFixo,
            PeriodicidadeJuros: periodicidadeJuros,
            ConvencaoDataNaoUtil: ConvencaoDataNaoUtil.Following);

        ICronogramaStrategy strategy = CronogramaStrategyFactory.Criar(simulacao.EstruturaAmortizacao);
        return strategy.Gerar(input);
    }

    /// <summary>
    /// Resolve a taxa efetiva anual a partir dos campos da simulação.
    /// Para taxa fixa, retorna <see cref="SimulacaoContratacao.TaxaAa"/> diretamente.
    /// Para CDI+spread, aplica composição: (1+CDI) × (1+spread) - 1.
    /// </summary>
    private static Percentual ResolverTaxaEfetiva(
        SimulacaoContratacao simulacao,
        decimal? cdiReferenciaAaPercentual)
    {
        if (simulacao.TipoTaxa == TipoTaxa.Fixa)
        {
            return simulacao.TaxaAa
                ?? throw new InvalidOperationException(
                    "TaxaAa é obrigatória quando TipoTaxa = Fixa. " +
                    "O invariante I-6 de SimulacaoContratacao deveria impedir esse estado.");
        }

        // TipoTaxa.CdiSpread: CDI de referência obrigatório
        if (!cdiReferenciaAaPercentual.HasValue)
        {
            throw new ArgumentException(
                "cdiReferenciaAaPercentual é obrigatório quando SimulacaoContratacao.TipoTaxa = CdiSpread. " +
                "Passe o CDI vigente do snapshot de mercado.",
                nameof(cdiReferenciaAaPercentual));
        }

        // Fórmula composta (convenção de mercado brasileiro):
        //   taxa_efetiva = (1 + CDI) × (1 + spread) - 1
        // Onde CDI e spread são frações decimais (0.105 para 10.5%).
        decimal cdi = cdiReferenciaAaPercentual.Value / 100m;
        decimal spread = simulacao.SpreadAa!.Value.AsDecimal;
        decimal efetivaDecimal = (1m + cdi) * (1m + spread) - 1m;

        return Percentual.DeFracao(efetivaDecimal);
    }
}
