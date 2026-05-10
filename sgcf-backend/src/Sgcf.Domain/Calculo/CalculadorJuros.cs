using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Calculo;

/// <summary>
/// Funções puras de cálculo de juros. Entrada determinística → saída determinística.
/// Não dependem do relógio do sistema; toda data é parâmetro explícito.
/// </summary>
public static class CalculadorJuros
{
    /// <summary>
    /// Calcula juros pro rata diária — regime linear (simples).
    /// Fórmula: Principal × Taxa × Dias / Base
    /// </summary>
    /// <remarks>
    /// Base 360 = padrão internacional (FINIMP, 4131, Balcão Caixa).
    /// Base 365 = alguns contratos NCE/CCE.
    /// Base 252 = CDI (dias úteis — atenção: dias deve ser contado em d.u. pelo chamador).
    /// Arredondamento HalfUp em 6 decimais (SPEC §8.2).
    /// </remarks>
    public static Money CalcularJurosProRata(
        Money principal,
        Percentual taxaAnual,
        int diasDecorridos,
        BaseCalculo baseCalculo)
    {
        if (diasDecorridos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diasDecorridos),
                "Dias decorridos não pode ser negativo.");
        }

        if (diasDecorridos == 0)
        {
            return Money.Zero(principal.Moeda);
        }

        decimal fator = taxaAnual.AsDecimal * diasDecorridos / (decimal)baseCalculo;
        return principal.Multiplicar(fator);
    }

    /// <summary>
    /// Calcula gross-up de IRRF: empresa absorve o tributo do exterior.
    /// Fórmula: IRRF efetivo = Juros × aliq / (1 − aliq) — Anexo A §1.
    /// </summary>
    /// <remarks>
    /// O gross-up aplica-se quando o contrato prevê que a empresa paga o IRRF por conta própria
    /// (cláusula "gross-up" ou "tax gross-up"). O credor externo recebe juros cheios.
    /// Alíquotas típicas: 15% (país com tratado), 25% (paraíso fiscal per IN 1.037/2010).
    /// </remarks>
    public static Money CalcularIrrfGrossUp(Money jurosNominais, Percentual aliquotaIrrf)
    {
        if (aliquotaIrrf.AsDecimal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aliquotaIrrf),
                "Alíquota de IRRF deve ser positiva.");
        }

        if (aliquotaIrrf.AsDecimal >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(aliquotaIrrf),
                "Alíquota de IRRF não pode ser ≥ 100%.");
        }

        decimal fator = aliquotaIrrf.AsDecimal / (1m - aliquotaIrrf.AsDecimal);
        return jurosNominais.Multiplicar(fator);
    }

    /// <summary>
    /// Calcula juros totais entre duas datas (dias corridos, base configurável).
    /// </summary>
    public static Money CalcularJurosEntreDatas(
        Money principal,
        Percentual taxaAnual,
        LocalDate dataInicio,
        LocalDate dataFim,
        BaseCalculo baseCalculo)
    {
        int dias = Period.Between(dataInicio, dataFim, PeriodUnits.Days).Days;
        return CalcularJurosProRata(principal, taxaAnual, (int)dias, baseCalculo);
    }
}
