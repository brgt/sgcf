using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao.Strategies;

/// <summary>
/// Padrão E — Abatimento de juros futuros embutidos (Price/SAC pré-fixado).
/// O devedor quita o principal com desconto dos juros embutidos ainda não vencidos.
/// Aplicável em contratos com tabela Price ou SAC pré-fixados onde os juros futuros
/// já estão refletidos no saldo.
/// Fórmula: TOTAL = C1 (principal) − abatimento (clamped a zero).
/// </summary>
public static class PadraoEStrategy
{
    /// <summary>
    /// Calcula o custo total de antecipação pelo Padrão E.
    /// </summary>
    /// <param name="entrada">Dados da simulação. JurosProRata é interpretado como abatimento de juros embutidos.</param>
    /// <param name="jurosEmbebidosFuturos">Juros futuros embutidos a abater. Se null, usa JurosProRata da entrada.</param>
    public static ResultadoSimulacaoAntecipacao Calcular(
        EntradaSimulacaoAntecipacao entrada,
        Money? jurosEmbebidosFuturos)
    {
        Moeda moeda = entrada.PrincipalAQuitar.Moeda;

        decimal c1 = Math.Round(entrada.PrincipalAQuitar.Valor, 6, MidpointRounding.AwayFromZero);

        // Prioridade: parâmetro explícito > JurosProRata da entrada (reaproveitado como abatimento)
        decimal abatimento = jurosEmbebidosFuturos.HasValue
            ? Math.Round(jurosEmbebidosFuturos.Value.Valor, 6, MidpointRounding.AwayFromZero)
            : entrada.JurosProRata.HasValue
                ? Math.Round(entrada.JurosProRata.Value.Valor, 6, MidpointRounding.AwayFromZero)
                : 0m;

        // Total nunca pode ser negativo — clamp at zero
        decimal total = Math.Max(0m, Math.Round(c1 - abatimento, 6, MidpointRounding.AwayFromZero));

        List<ComponenteCusto> componentes = new(2)
        {
            new ComponenteCusto("C1", "Principal a quitar", new Money(c1, moeda), "+"),
        };

        if (abatimento > 0m)
        {
            componentes.Add(new ComponenteCusto("ABAT", "Abatimento de juros embutidos futuros", new Money(abatimento, moeda), "-"));
        }

        return new ResultadoSimulacaoAntecipacao(
            Padrao: PadraoAntecipacao.E,
            Permitido: true,
            Alertas: Array.Empty<string>(),
            Componentes: componentes.AsReadOnly(),
            TotalAQuitar: new Money(total, moeda),
            ExigeAnuenciaExpressa: false);
    }
}
