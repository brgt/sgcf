using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Calcula o valor total de garantia exigida para um dado <c>valorAlvo</c>,
/// somando contribuições de cada <see cref="GarantiaExigidaItem"/>.
///
/// Regras de composição:
/// <list type="bullet">
///   <item>Item com <c>PercentualSobreLimite</c>: contribui <c>percentual / 100 × valorAlvo</c>.</item>
///   <item>Item com <c>ValorFixoBrl</c>: contribui o valor fixo diretamente.</item>
///   <item>Item sem nenhum dos dois (ex: <c>Aval</c>): contribui zero.</item>
/// </list>
///
/// Grupos de alternativas "OU" (RV-GA):
/// <list type="bullet">
///   <item>
///     Itens com o mesmo <c>GrupoAlternativaId</c> não-nulo formam um grupo mutuamente
///     substitutível. O grupo contribui com o <b>mínimo</b> entre as contribuições
///     individuais de seus membros — o piso mais barato capaz de satisfazer o grupo.
///   </item>
///   <item>
///     Itens sem <c>GrupoAlternativaId</c> (independentes) somam normalmente, sem alteração
///     de comportamento em relação à versão anterior.
///   </item>
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
    public static Money Calcular(IReadOnlyCollection<GarantiaExigidaItem> garantias, Money valorAlvo)
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

        // Itens independentes: somam individualmente (comportamento legado preservado)
        foreach (GarantiaExigidaItem garantia in garantias.Where(g => g.GrupoAlternativaId is null))
        {
            total += ContribuicaoItem(garantia, valorAlvo);
        }

        // Grupos de alternativas "OU": cada grupo contribui com o mínimo entre seus membros.
        // RV-GA: o exigido do grupo é o piso — basta a alternativa mais barata cobrir o grupo.
        var grupos = garantias
            .Where(g => g.GrupoAlternativaId is not null)
            .GroupBy(g => g.GrupoAlternativaId!.Value);

        foreach (var grupo in grupos)
        {
            decimal minContribuicao = grupo.Min(g => ContribuicaoItem(g, valorAlvo));
            total += minContribuicao;
        }

        // Money ctor aplica MidpointRounding.AwayFromZero automaticamente
        return new Money(total, Moeda.Brl);
    }

    /// <summary>
    /// Calcula a contribuição individual de um item em reais (sem arredondamento adicional
    /// além do que o percentual já impõe). O arredondamento final é feito pelo construtor
    /// de <see cref="Money"/>.
    /// </summary>
    private static decimal ContribuicaoItem(GarantiaExigidaItem garantia, Money valorAlvo)
    {
        if (garantia.PercentualSobreLimite.HasValue)
        {
            // Percentual humano (20 = 20%) — divide por 100 para obter fração
            return Math.Round(
                garantia.PercentualSobreLimite.Value / 100m * valorAlvo.Valor,
                2,
                MidpointRounding.AwayFromZero);
        }

        if (garantia.ValorFixoBrl.HasValue)
        {
            return garantia.ValorFixoBrl.Value.Valor;
        }

        // Sem percentual nem valor fixo (ex: Aval) → contribui zero
        return 0m;
    }
}
