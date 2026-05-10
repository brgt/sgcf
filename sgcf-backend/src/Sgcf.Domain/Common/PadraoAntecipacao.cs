namespace Sgcf.Domain.Common;

/// <summary>
/// Padrão de cálculo de antecipação de pagamento — configurado por banco/modalidade no BANCO_CONFIG.
/// Derivado dos contratos reais da Proxys (Anexo C).
/// </summary>
public enum PadraoAntecipacao
{
    /// <summary>Pro rata + break funding fee fixo + indenização. Padrão BB FINIMP.</summary>
    A = 1,

    /// <summary>
    /// Cobra juros do período TOTAL contratado — sem desconto de juros futuros.
    /// ALERTA CRÍTICO: antecipar pelo Padrão B (Sicredi) não economiza juros.
    /// </summary>
    B = 2,

    /// <summary>Desconto a taxa de mercado (MTM). Padrão FGI BV (PEAC).</summary>
    C = 3,

    /// <summary>Fórmula TLA BACEN — Resoluções 3401/06 e 3516/07. Padrão Caixa Balcão.</summary>
    D = 4,

    /// <summary>Pagamento ordinário com abatimento proporcional de juros futuros. Padrão Caixa prefixado.</summary>
    E = 5
}
