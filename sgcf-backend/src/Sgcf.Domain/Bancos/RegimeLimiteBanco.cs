namespace Sgcf.Domain.Bancos;

/// <summary>
/// Regime de controle de limite de crédito de um banco.
/// SPEC_REGIME_LIMITE_EXPLICITO §3.1 (emenda à SPEC_LIMITE_GLOBAL §4.3).
/// </summary>
public enum RegimeLimiteBanco
{
    /// <summary>
    /// Regime per-modalidade (Cenário B da SPEC_LIMITE_GLOBAL).
    /// Cada modalidade tem seu próprio <c>LimiteBanco</c>; o <c>LimiteGlobalBanco</c> (se existir)
    /// atua como teto agregado. É o regime padrão e o comportamento histórico do sistema.
    /// </summary>
    PerModalidade = 0,

    /// <summary>
    /// Regime de limite global puro (Cenário A da SPEC_LIMITE_GLOBAL).
    /// O banco não possui <c>LimiteBanco</c> por modalidade; qualquer operação consome o
    /// <c>LimiteGlobalBanco</c> vigente.
    /// </summary>
    GlobalPuro = 1,
}
