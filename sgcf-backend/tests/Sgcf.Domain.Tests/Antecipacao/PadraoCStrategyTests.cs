using FluentAssertions;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.Domain.Tests.Antecipacao;

[Trait("Category", "Domain")]
public sealed class PadraoCStrategyTests
{
    private static EntradaSimulacaoAntecipacao CriarEntrada(
        decimal principalValor = 500_000m,
        Moeda moeda = Moeda.Usd,
        decimal taxaAaPct = 16.24m,
        decimal? taxaMercadoAaPct = null,
        int prazoRemanescenteDias = 365,
        int baseCalculo = 365)
    {
        return new EntradaSimulacaoAntecipacao(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: null,
            PrazoTotalOriginalDias: 365,
            PrazoRemanescenteDias: prazoRemanescenteDias,
            PrazoRemanescenteMeses: 12,
            TaxaAa: Percentual.De(taxaAaPct),
            BaseCalculo: baseCalculo,
            TaxaMercadoAtualAa: taxaMercadoAaPct.HasValue ? Percentual.De(taxaMercadoAaPct.Value) : (Percentual?)null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);
    }

    // ── Teste 1: Taxa mercado > taxa contratada → total menor que saldo ────────

    [Fact]
    public void PadraoC_TaxaMercadoMaiorQueContratada_TotalMenorQueSaldo()
    {
        // Arrange — taxa_mercado=14% > taxa_contratada=16.24% → fator < 1 → TOTAL < saldo
        // Wait: fator = (1 + 0.1624)^(365/365) / (1 + 0.14)^(365/365) = 1.1624 / 1.14 > 1
        // Actually taxa_mercado > taxa_contratada → total LESS than saldo (buyer wins)
        // fator = (1+taxa)^exp / (1+tmkt)^exp
        // If tmkt > taxa: denominator > numerator → fator < 1 → TOTAL < saldo
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 500_000m,
            taxaAaPct: 16.24m,
            taxaMercadoAaPct: 20m,       // taxa mercado > taxa contratada
            prazoRemanescenteDias: 365,
            baseCalculo: 365);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoCStrategy.Calcular(entrada, exigeAnuenciaExpressa: false);

        // Assert
        resultado.TotalAQuitar.Valor.Should().BeLessThan(500_000m,
            because: "quando taxa de mercado > taxa contratada, antecipação é favorável ao devedor");
    }

    // ── Teste 2: Taxa mercado < taxa contratada → total maior que saldo ────────

    [Fact]
    public void PadraoC_TaxaMercadoMenorQueContratada_TotalMaiorQueSaldo()
    {
        // Arrange — taxa_mercado=12% < taxa_contratada=16.24% → fator > 1 → TOTAL > saldo
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 500_000m,
            taxaAaPct: 16.24m,
            taxaMercadoAaPct: 12m,       // taxa mercado < taxa contratada
            prazoRemanescenteDias: 365,
            baseCalculo: 365);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoCStrategy.Calcular(entrada, exigeAnuenciaExpressa: false);

        // Assert
        resultado.TotalAQuitar.Valor.Should().BeGreaterThan(500_000m,
            because: "quando taxa de mercado < taxa contratada, antecipação é desfavorável ao devedor");
    }

    // ── Teste 3: Sem taxa de mercado → lança InvalidOperationException ─────────

    [Fact]
    public void PadraoC_SemTaxaMercado_Throws()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            taxaMercadoAaPct: null);

        // Act
        Action act = () => PadraoCStrategy.Calcular(entrada, exigeAnuenciaExpressa: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TaxaMercadoAtualAa*");
    }
}
