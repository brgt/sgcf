using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao.Strategies;

/// <summary>
/// Padrão D — Taxa de Liquidação Antecipada (TLA) — Caixa Econômica Federal.
/// A TLA é o maior valor entre dois critérios calculados sobre o VTD (valor total do débito):
///   TLA_A = VTD × tlaPctSobreSaldo
///   TLA_B = VTD × tlaPctPorMesRemanescente × prazoRemanescenteMeses
/// Refinanciamentos internos são isentos de TLA (Res. BACEN 3401/06 §3º).
/// </summary>
public static class PadraoDStrategy
{
    private const string AlertaIsencaoRefinanciamento =
        "Isenção de TLA: refinanciamento interno (Res. BACEN 3401/06 §3º)";

    /// <summary>
    /// Calcula o custo total de antecipação pelo Padrão D.
    /// </summary>
    public static ResultadoSimulacaoAntecipacao Calcular(
        EntradaSimulacaoAntecipacao entrada,
        Percentual tlaPctSobreSaldo,
        Percentual tlaPctPorMesRemanescente)
    {
        Moeda moeda = entrada.PrincipalAQuitar.Moeda;

        decimal c1 = Math.Round(entrada.PrincipalAQuitar.Valor, 6, MidpointRounding.AwayFromZero);
        decimal c2 = entrada.JurosProRata.HasValue
            ? Math.Round(entrada.JurosProRata.Value.Valor, 6, MidpointRounding.AwayFromZero)
            : 0m;

        decimal vtd = Math.Round(c1 + c2, 6, MidpointRounding.AwayFromZero);

        decimal tlaEfetiva;
        List<string> alertas = new(1);

        if (entrada.OrigemRefinanciamentoInterno)
        {
            tlaEfetiva = 0m;
            alertas.Add(AlertaIsencaoRefinanciamento);
        }
        else
        {
            decimal tlaA = Math.Round(vtd * tlaPctSobreSaldo.AsDecimal, 6, MidpointRounding.AwayFromZero);
            decimal tlaB = Math.Round(
                vtd * tlaPctPorMesRemanescente.AsDecimal * entrada.PrazoRemanescenteMeses,
                6,
                MidpointRounding.AwayFromZero);

            tlaEfetiva = Math.Max(tlaA, tlaB);
        }

        decimal total = Math.Round(vtd + tlaEfetiva, 6, MidpointRounding.AwayFromZero);

        List<ComponenteCusto> componentes = new(4)
        {
            new ComponenteCusto("C1", "Principal a quitar", new Money(c1, moeda), "+"),
        };

        if (c2 > 0m)
        {
            componentes.Add(new ComponenteCusto("C2", "Juros pro rata", new Money(c2, moeda), "+"));
        }

        componentes.Add(new ComponenteCusto("TLA", "Taxa de Liquidação Antecipada", new Money(tlaEfetiva, moeda), "+"));

        return new ResultadoSimulacaoAntecipacao(
            Padrao: PadraoAntecipacao.D,
            Permitido: true,
            Alertas: alertas.AsReadOnly(),
            Componentes: componentes.AsReadOnly(),
            TotalAQuitar: new Money(total, moeda),
            ExigeAnuenciaExpressa: false);
    }
}
