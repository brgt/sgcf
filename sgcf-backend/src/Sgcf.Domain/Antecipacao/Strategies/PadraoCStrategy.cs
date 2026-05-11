using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao.Strategies;

/// <summary>
/// Padrão C — Marcação a mercado (MTM).
/// O valor a quitar é ajustado pela relação entre a taxa contratada e a taxa de mercado atual.
/// Se taxa_mercado >= taxa_contratada, o devedor paga menos que o saldo em aberto (favorável).
/// Se taxa_mercado &lt; taxa_contratada, o devedor paga mais (desfavorável).
/// </summary>
public static class PadraoCStrategy
{
    /// <summary>
    /// Calcula o custo total de antecipação pelo Padrão C (MTM).
    /// </summary>
    /// <exception cref="InvalidOperationException">Lançada quando TaxaMercadoAtualAa não é informada.</exception>
    public static ResultadoSimulacaoAntecipacao Calcular(
        EntradaSimulacaoAntecipacao entrada,
        bool exigeAnuenciaExpressa)
    {
        if (!entrada.TaxaMercadoAtualAa.HasValue)
        {
            throw new InvalidOperationException("TaxaMercadoAtualAa é obrigatória para Padrão C");
        }

        Moeda moeda = entrada.PrincipalAQuitar.Moeda;
        decimal principal = entrada.PrincipalAQuitar.Valor;
        double exponent = (double)entrada.PrazoRemanescenteDias / (double)entrada.BaseCalculo;

        // Fator MTM: desconta pelo mercado e capitaliza pela taxa contratada
        double fator = Math.Pow(1.0 + (double)entrada.TaxaAa.AsDecimal, exponent)
                     / Math.Pow(1.0 + (double)entrada.TaxaMercadoAtualAa.Value.AsDecimal, exponent);

        decimal total = Math.Round(principal * (decimal)fator, 6, MidpointRounding.AwayFromZero);

        List<string> alertas = new(1);

        // Quando taxa de mercado >= taxa contratada, antecipação resulta em desconto
        if (entrada.TaxaMercadoAtualAa.Value.AsDecimal >= entrada.TaxaAa.AsDecimal)
        {
            alertas.Add("Antecipação favorável: taxa de mercado atual ≥ taxa contratada. " +
                        "O valor a quitar é menor que o saldo em aberto (MTM com desconto).");
        }
        else
        {
            alertas.Add("Antecipação desfavorável: taxa de mercado atual < taxa contratada. " +
                        "O valor a quitar é maior que o saldo em aberto (MTM com prêmio).");
        }

        List<ComponenteCusto> componentes = new(1)
        {
            new ComponenteCusto("VMTM", "Valor marcado a mercado", new Money(total, moeda), "+"),
        };

        return new ResultadoSimulacaoAntecipacao(
            Padrao: PadraoAntecipacao.C,
            Permitido: true,
            Alertas: alertas.AsReadOnly(),
            Componentes: componentes.AsReadOnly(),
            TotalAQuitar: new Money(total, moeda),
            ExigeAnuenciaExpressa: exigeAnuenciaExpressa);
    }
}
