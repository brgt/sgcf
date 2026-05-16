using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Serviço de domínio puro para cálculo da economia obtida na negociação.
/// Sem I/O, sem estado, sem IClock — função matemática pura.
/// SPEC §5.2.
/// </summary>
public static class CalculadoraEconomia
{
    /// <summary>
    /// Calcula a economia bruta e a economia ajustada por CDI entre uma proposta aceita e o contrato fechado.
    ///
    /// Cenário mesmo prazo (SPEC §5.2 caso simples):
    ///   Economia bruta = (CET_proposta - CET_contrato) × ValorPrincipal × (Prazo/360)
    ///
    /// Cenário prazos diferentes (SPEC §5.2 VPL via CDI):
    ///   1. Calcular VPL do fluxo da proposta usando CDI como taxa de desconto.
    ///   2. Calcular VPL do fluxo do contrato fechado usando CDI como taxa de desconto.
    ///   3. Economia ajustada = VPL_proposta - VPL_contrato.
    ///
    /// TODO (Onda 2/3): Refinamento quando prazo diferem e fluxos têm distribuições distintas
    /// (Price vs Bullet). Atualmente usa fórmula linear para o caso diferente-prazo.
    /// </summary>
    /// <param name="cetPropostaAaPercentual">CET da proposta em % a.a. (ex: 7.5).</param>
    /// <param name="cetContratoAaPercentual">CET do contrato fechado em % a.a. (ex: 7.2).</param>
    /// <param name="valorPrincipalBrl">Principal da operação em BRL.</param>
    /// <param name="prazoProposta">Prazo em dias da proposta.</param>
    /// <param name="prazoContrato">Prazo em dias do contrato fechado.</param>
    /// <param name="taxaCdiAaPercentual">Taxa CDI atual em % a.a. para equalização (ex: 10.75).</param>
    /// <param name="dataReferenciaCdi">Data de referência do CDI usado.</param>
    /// <returns>Tupla com economia bruta BRL, economia ajustada CDI BRL e data referência CDI.</returns>
    public static (Money EconomiaBruta, Money EconomiaAjustadaCdi, LocalDate DataReferenciaCdi) Calcular(
        decimal cetPropostaAaPercentual,
        decimal cetContratoAaPercentual,
        Money valorPrincipalBrl,
        int prazoProposta,
        int prazoContrato,
        decimal taxaCdiAaPercentual,
        LocalDate dataReferenciaCdi)
    {
        if (valorPrincipalBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorPrincipalBrl deve ser em BRL.", nameof(valorPrincipalBrl));
        }

        if (prazoProposta < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(prazoProposta), "PrazoProposta deve ser >= 1.");
        }

        if (prazoContrato < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(prazoContrato), "PrazoContrato deve ser >= 1.");
        }

        if (taxaCdiAaPercentual <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taxaCdiAaPercentual), "TaxaCdiAaPercentual deve ser positiva.");
        }

        decimal principal = valorPrincipalBrl.Valor;

        // ── Economia bruta (fórmula linear SPEC §5.2) ────────────────────────
        // Usa o prazo da proposta como base de referência para a diferença de custo.
        decimal difCet = (cetPropostaAaPercentual - cetContratoAaPercentual) / 100m;
        decimal economiaBrutaValor = Math.Round(
            difCet * principal * prazoProposta / 360m,
            6,
            MidpointRounding.AwayFromZero);

        Money economiaBruta = new(economiaBrutaValor, Moeda.Brl);

        // ── Economia ajustada por CDI (SPEC §5.2 VPL) ───────────────────────
        // VPL do custo da proposta e do contrato, descontados pela taxa CDI.
        // Para MVP, usa fórmula linear com desconto CDI para equalizar prazos.
        // Quando prazos são iguais, VPL e economia bruta convergem.
        Money economiaAjustada = CalcularEconomiaAjustadaCdi(
            cetPropostaAaPercentual,
            cetContratoAaPercentual,
            principal,
            prazoProposta,
            prazoContrato,
            taxaCdiAaPercentual);

        return (economiaBruta, economiaAjustada, dataReferenciaCdi);
    }

    // ─── Helpers privados ───────────────────────────────────────────────────

    private static Money CalcularEconomiaAjustadaCdi(
        decimal cetPropostaAaPercentual,
        decimal cetContratoAaPercentual,
        decimal principal,
        int prazoProposta,
        int prazoContrato,
        decimal taxaCdiAaPercentual)
    {
        decimal cdiDecimal = taxaCdiAaPercentual / 100m;

        // Custo total estimado da proposta e do contrato (fluxo único — simplificação bullet)
        // TODO (Onda 2/3): distribuição real por parcelas (Price/SAC) com VPL rigoroso
        decimal custoTotalProposta = principal * cetPropostaAaPercentual / 100m * prazoProposta / 360m;
        decimal custoTotalContrato = principal * cetContratoAaPercentual / 100m * prazoContrato / 360m;

        // Desconta pelo CDI para equalizar prazos diferentes
        decimal fatorDescontoProposta = CalcularFatorDesconto(cdiDecimal, prazoProposta);
        decimal fatorDescontoContrato = CalcularFatorDesconto(cdiDecimal, prazoContrato);

        decimal vplProposta = Math.Round(custoTotalProposta * fatorDescontoProposta, 6, MidpointRounding.AwayFromZero);
        decimal vplContrato = Math.Round(custoTotalContrato * fatorDescontoContrato, 6, MidpointRounding.AwayFromZero);

        decimal economiaAjustadaValor = Math.Round(vplProposta - vplContrato, 6, MidpointRounding.AwayFromZero);

        return new Money(economiaAjustadaValor, Moeda.Brl);
    }

    /// <summary>Fator de desconto = 1 / (1 + CDI_aa * prazo/360).</summary>
    private static decimal CalcularFatorDesconto(decimal cdiAaDecimal, int prazoDias)
    {
        decimal denominador = 1m + cdiAaDecimal * prazoDias / 360m;
        return Math.Round(1m / denominador, 8, MidpointRounding.AwayFromZero);
    }
}
