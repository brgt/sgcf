using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao.Strategies;

/// <summary>
/// Padrão A — Break Funding Fee (BFF).
/// Utilizado por bancos internacionais (Citi, HSBC, Santander, etc.) que têm
/// custo de desfazimento de posição no mercado interbancário.
/// Fórmula: TOTAL = C1 (principal) + C2 (juros pro rata) + C6 (BFF sobre base antecipada) + C7 (indenização banco).
/// </summary>
public static class PadraoAStrategy
{
    /// <summary>
    /// Calcula o custo total de antecipação pelo Padrão A.
    /// </summary>
    public static ResultadoSimulacaoAntecipacao Calcular(
        EntradaSimulacaoAntecipacao entrada,
        Percentual breakFundingFeePct,
        bool exigeAnuenciaExpressa)
    {
        Moeda moeda = entrada.PrincipalAQuitar.Moeda;

        decimal c1 = Math.Round(entrada.PrincipalAQuitar.Valor, 6, MidpointRounding.AwayFromZero);
        decimal c2 = entrada.JurosProRata.HasValue
            ? Math.Round(entrada.JurosProRata.Value.Valor, 6, MidpointRounding.AwayFromZero)
            : 0m;

        decimal baseAntecipado = Math.Round(c1 + c2, 6, MidpointRounding.AwayFromZero);
        decimal c6 = Math.Round(baseAntecipado * breakFundingFeePct.AsDecimal, 6, MidpointRounding.AwayFromZero);

        decimal c7 = entrada.IndenizacaoBanco.HasValue
            ? Math.Round(entrada.IndenizacaoBanco.Value.Valor, 6, MidpointRounding.AwayFromZero)
            : 0m;

        decimal total = Math.Round(c1 + c2 + c6 + c7, 6, MidpointRounding.AwayFromZero);

        List<ComponenteCusto> componentes = new(4)
        {
            new ComponenteCusto("C1", "Principal a quitar", new Money(c1, moeda), "+"),
        };

        if (c2 > 0m)
        {
            componentes.Add(new ComponenteCusto("C2", "Juros pro rata", new Money(c2, moeda), "+"));
        }

        componentes.Add(new ComponenteCusto("C6", "Break Funding Fee", new Money(c6, moeda), "+"));

        if (c7 > 0m)
        {
            componentes.Add(new ComponenteCusto("C7", "Indenização banco", new Money(c7, moeda), "+"));
        }

        return new ResultadoSimulacaoAntecipacao(
            Padrao: PadraoAntecipacao.A,
            Permitido: true,
            Alertas: Array.Empty<string>(),
            Componentes: componentes.AsReadOnly(),
            TotalAQuitar: new Money(total, moeda),
            ExigeAnuenciaExpressa: exigeAnuenciaExpressa);
    }
}
