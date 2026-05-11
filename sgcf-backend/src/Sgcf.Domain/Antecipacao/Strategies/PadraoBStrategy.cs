using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao.Strategies;

/// <summary>
/// Padrão B — Sicredi (juros do período total contratado).
/// O Sicredi cobra os juros sobre o prazo total original, tornando antecipação
/// financeiramente indiferente em termos de custo de juros. A antecipação só
/// faz sentido para liberar limite de crédito ou eliminar exposição cambial.
/// Fórmula: TOTAL = C1 (principal) + C3 (juros período total).
/// </summary>
public static class PadraoBStrategy
{
    private const string AlertaSicredi =
        "ALERTA CRÍTICO — Sicredi cobra juros do período total contratado. " +
        "Antecipação não economiza juros. Só faz sentido para liberar limite de crédito ou eliminar exposição cambial.";

    /// <summary>
    /// Calcula o custo total de antecipação pelo Padrão B.
    /// </summary>
    public static ResultadoSimulacaoAntecipacao Calcular(
        EntradaSimulacaoAntecipacao entrada,
        bool exigeAnuenciaExpressa)
    {
        Moeda moeda = entrada.PrincipalAQuitar.Moeda;

        decimal c1 = Math.Round(entrada.PrincipalAQuitar.Valor, 6, MidpointRounding.AwayFromZero);

        // C3 = principal × taxa_aa × prazo_total_original / base — juros sobre período total contratado
        decimal c3 = Math.Round(
            c1 * entrada.TaxaAa.AsDecimal * entrada.PrazoTotalOriginalDias / (decimal)entrada.BaseCalculo,
            6,
            MidpointRounding.AwayFromZero);

        decimal total = Math.Round(c1 + c3, 6, MidpointRounding.AwayFromZero);

        List<ComponenteCusto> componentes = new(2)
        {
            new ComponenteCusto("C1", "Principal a quitar", new Money(c1, moeda), "+"),
            new ComponenteCusto("C3", "Juros período total contratado", new Money(c3, moeda), "+"),
        };

        return new ResultadoSimulacaoAntecipacao(
            Padrao: PadraoAntecipacao.B,
            Permitido: true,
            Alertas: new[] { AlertaSicredi },
            Componentes: componentes.AsReadOnly(),
            TotalAQuitar: new Money(total, moeda),
            ExigeAnuenciaExpressa: exigeAnuenciaExpressa);
    }
}
