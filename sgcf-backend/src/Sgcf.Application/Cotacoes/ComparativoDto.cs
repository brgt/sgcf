namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Linha do comparativo de propostas — três métricas por proposta (SPEC §5.3).
/// Permite ao operador comparar propostas com prazos diferentes de forma justa.
/// </summary>
public sealed record ComparativoDto(
    Guid PropostaId,
    Guid BancoId,
    string MoedaOriginal,
    int PrazoDias,
    /// <summary>Coluna 1: taxa nominal (taxa_aa + spread_aa). O que o banco oferece "de cara".</summary>
    decimal TaxaNominalAaPercentual,
    /// <summary>Coluna 2: CET calculado — métrica padrão regulada, comparável entre propostas de mesmo prazo.</summary>
    decimal CetAaPercentual,
    /// <summary>
    /// Coluna 3: custo total em BRL equalizado para o prazo da cotação via CDI.
    /// A única coluna que permite ranking matemático puro entre prazos diferentes.
    /// </summary>
    decimal CustoTotalEquivalenteBrl,
    bool ExigeNdf,
    string GarantiaExigida,
    decimal ValorGarantiaExigidaBrl,
    string Status,
    /// <summary>
    /// IRRF estimado em BRL — campo informativo exclusivo de Lei 4131 (SPEC §8.1).
    /// Calculado on-demand pelo <see cref="CompararPropostasQueryHandler"/> quando
    /// <c>aliquotaIrrfPercentual</c> é fornecida na query. Zero para outras modalidades
    /// e quando a alíquota não é informada. Não é persistido na Proposta (decisão MD-5/AD-3).
    /// </summary>
    decimal IrrfEstimadoBrl = 0m,
    /// <summary>
    /// Taxa indicativa de mercado no momento da proposta, em % a.a. como fração (0.065 = 6,5% a.a.).
    /// Nula quando não capturada. GAP-CKP-19.
    /// </summary>
    decimal? TaxaIndicativaAaPercentual = null,
    /// <summary>
    /// Spread entre a taxa indicativa de mercado e a taxa efetiva da proposta, em bps.
    /// Fórmula: (TaxaAaPercentual + SpreadAaPercentual - TaxaIndicativaAa) × 10 000.
    /// Nulo quando <see cref="TaxaIndicativaAaPercentual"/> não está disponível. GAP-CKP-19.
    /// </summary>
    decimal? SpreadIndicativaPropostaBps = null);
