using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Calcula estimativa de IRRF (Imposto de Renda Retido na Fonte) para propostas Lei 4131.
/// <para>
/// Fórmula (SPEC §8.1):
/// <code>
///   JurosProjetadosMoedaOriginal = ValorOferecido × (TaxaAa + Spread) × (PrazoDias / 360)
///   JurosProjetadosBrl           = JurosProjetadosMoedaOriginal × PtaxUsadaUsdBrl
///   IrrfEstimadoBrl              = JurosProjetadosBrl × (AliquotaIrrfPercentual / 100)
/// </code>
/// </para>
/// <para>
/// Resultado arredondado a 2 casas decimais (MidpointRounding.AwayFromZero — padrão comercial brasileiro).
/// </para>
/// <para>
/// Função pura: sem I/O, sem efeitos colaterais, sem estado. Onda 4 — Lei 4131 SPEC §8.2.
/// </para>
/// </summary>
public static class CalculadoraIrrfEstimado
{
    /// <summary>
    /// Calcula o IRRF estimado em BRL para a proposta informada.
    /// </summary>
    /// <param name="proposta">Proposta Lei 4131 em moeda estrangeira.</param>
    /// <param name="ptaxUsadaUsdBrl">
    /// PTAX USD/BRL da cotação. Para moedas não-USD, deve ser o cross-rate efetivo
    /// (moeda→USD→BRL) já calculado pelo handler. SPEC §8.1.
    /// </param>
    /// <param name="aliquotaIrrfPercentual">
    /// Alíquota IRRF em percentual humano (ex: 15 para 15%). Quando null, zero ou negativo,
    /// retorna 0 — sem assumir alíquota default. SPEC §8.3.
    /// </param>
    /// <returns>IRRF estimado em BRL, arredondado a 2 casas decimais. Zero quando alíquota não informada ou não positiva.</returns>
    public static decimal Calcular(
        Proposta proposta,
        decimal ptaxUsadaUsdBrl,
        decimal? aliquotaIrrfPercentual)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        // Quando alíquota não informada ou não positiva, IRRF é zero (SPEC §8.3).
        // Não assumimos alíquota default — decisão explícita do operador.
        if (aliquotaIrrfPercentual is null || aliquotaIrrfPercentual.Value <= 0m)
        {
            return 0m;
        }

        decimal taxaTotal = (proposta.TaxaAaPercentual + proposta.SpreadAaPercentual) / 100m;

        // Juros projetados na moeda original (base 360 — MD travado SPEC §7.1).
        decimal jurosProjetadosMoedaOriginal =
            proposta.ValorOferecidoMoedaOriginal.Valor * taxaTotal * proposta.PrazoDias / 360m;

        // Converte para BRL usando PTAX efetiva.
        decimal jurosProjetadosBrl = jurosProjetadosMoedaOriginal * ptaxUsadaUsdBrl;

        // Aplica alíquota IRRF e arredonda ao centavo comercial.
        return Math.Round(
            jurosProjetadosBrl * aliquotaIrrfPercentual.Value / 100m,
            2,
            MidpointRounding.AwayFromZero);
    }
}
