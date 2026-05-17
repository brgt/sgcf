using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Calcula o valor total de garantia exigida para um dado <c>valorAlvo</c>,
/// somando contribuições de cada <see cref="GarantiaExigidaLimite"/>.
///
/// Regras de composição:
/// <list type="bullet">
///   <item>Item com <c>PercentualSobreLimite</c>: contribui <c>percentual / 100 × valorAlvo</c>.</item>
///   <item>Item com <c>ValorFixoBrl</c>: contribui o valor fixo diretamente.</item>
///   <item>Item sem nenhum dos dois (ex: <c>Aval</c>): contribui zero.</item>
/// </list>
///
/// Pure static — sem estado, sem I/O, sem efeitos colaterais.
/// </summary>
public static class CalculadorValorGarantiaExigida
{
    /// <summary>
    /// Calcula o valor total de garantia exigida para o <paramref name="valorAlvo"/>.
    /// Retorna <c>Money(0, BRL)</c> quando a coleção for vazia.
    /// </summary>
    /// <param name="garantias">Coleção de garantias do <see cref="LimiteBanco"/>.</param>
    /// <param name="valorAlvo">Valor alvo da cotação em BRL, usado como base para os percentuais.</param>
    /// <exception cref="ArgumentException">Lançado se <paramref name="valorAlvo"/> não for em BRL.</exception>
    public static Money Calcular(IReadOnlyCollection<GarantiaExigidaLimite> garantias, Money valorAlvo)
    {
        ArgumentNullException.ThrowIfNull(garantias);

        if (valorAlvo.Moeda != Moeda.Brl)
        {
            throw new ArgumentException(
                "ValorAlvo deve ser em BRL para calcular garantias.",
                nameof(valorAlvo));
        }

        if (garantias.Count == 0)
        {
            return new Money(0m, Moeda.Brl);
        }

        decimal total = 0m;

        foreach (GarantiaExigidaLimite garantia in garantias)
        {
            if (garantia.PercentualSobreLimite.HasValue)
            {
                // Percentual humano (20 = 20%) — divide por 100 para obter fração
                total += garantia.PercentualSobreLimite.Value / 100m * valorAlvo.Valor;
                continue;
            }

            if (garantia.ValorFixoBrl.HasValue)
            {
                total += garantia.ValorFixoBrl.Value.Valor;
            }

            // Sem percentual nem valor fixo (ex: Aval) → contribui zero; nada a somar
        }

        // Money ctor aplica MidpointRounding.AwayFromZero automaticamente
        return new Money(total, Moeda.Brl);
    }
}
