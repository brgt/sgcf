using FluentAssertions;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.Domain.Tests.Antecipacao;

[Trait("Category", "Domain")]
public sealed class PadraoAStrategyTests
{
    private static EntradaSimulacaoAntecipacao CriarEntrada(
        decimal principalValor = 200_000m,
        Moeda moeda = Moeda.Usd,
        decimal? jurosProRataValor = 4_900m,
        decimal? indenizacaoValor = null)
    {
        return new EntradaSimulacaoAntecipacao(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: jurosProRataValor.HasValue ? new Money(jurosProRataValor.Value, moeda) : (Money?)null,
            PrazoTotalOriginalDias: 360,
            PrazoRemanescenteDias: 180,
            PrazoRemanescenteMeses: 6,
            TaxaAa: Percentual.De(8m),
            BaseCalculo: 360,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: indenizacaoValor.HasValue ? new Money(indenizacaoValor.Value, moeda) : (Money?)null,
            OrigemRefinanciamentoInterno: false);
    }

    // ── Teste 1: Cálculo padrão sem indenização ────────────────────────────────

    [Fact]
    public void PadraoA_CalculaTotalCorreto()
    {
        // Arrange
        // principal=200000 USD, juros=4900 USD, break=1%
        // base_antecipado = 200000 + 4900 = 204900
        // C6 = 204900 × 0.01 = 2049
        // TOTAL = 200000 + 4900 + 2049 = 206949
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 200_000m,
            jurosProRataValor: 4_900m,
            indenizacaoValor: null);

        Percentual breakFundingFee = Percentual.DeFracao(0.01m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoAStrategy.Calcular(entrada, breakFundingFee, exigeAnuenciaExpressa: false);

        // Assert
        resultado.TotalAQuitar.Valor.Should().Be(206_949m);
        resultado.TotalAQuitar.Moeda.Should().Be(Moeda.Usd);
        resultado.Padrao.Should().Be(PadraoAntecipacao.A);
        resultado.Permitido.Should().BeTrue();
    }

    // ── Teste 2: Com indenização inclui C7 ────────────────────────────────────

    [Fact]
    public void PadraoA_ComIndenizacao_IncluiC7()
    {
        // Arrange
        // TOTAL = 206949 + 800 = 207749
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 200_000m,
            jurosProRataValor: 4_900m,
            indenizacaoValor: 800m);

        Percentual breakFundingFee = Percentual.DeFracao(0.01m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoAStrategy.Calcular(entrada, breakFundingFee, exigeAnuenciaExpressa: false);

        // Assert
        resultado.TotalAQuitar.Valor.Should().Be(207_749m);
        resultado.Componentes.Should().Contain(c => c.Codigo == "C7");
    }

    // ── Teste 3: Sem indenização — C7 não aparece ────────────────────────────

    [Fact]
    public void PadraoA_SemIndenizacao_ExcluiC7()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 200_000m,
            jurosProRataValor: 4_900m,
            indenizacaoValor: null);

        Percentual breakFundingFee = Percentual.DeFracao(0.01m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoAStrategy.Calcular(entrada, breakFundingFee, exigeAnuenciaExpressa: false);

        // Assert
        resultado.TotalAQuitar.Valor.Should().Be(206_949m);
        resultado.Componentes.Should().NotContain(c => c.Codigo == "C7");
    }
}
