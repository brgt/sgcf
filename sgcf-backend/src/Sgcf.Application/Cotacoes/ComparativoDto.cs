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
    string Status);
