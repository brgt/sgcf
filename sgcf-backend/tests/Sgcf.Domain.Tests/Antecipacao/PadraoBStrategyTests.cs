using FluentAssertions;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.Domain.Tests.Antecipacao;

[Trait("Category", "Domain")]
public sealed class PadraoBStrategyTests
{
    private static EntradaSimulacaoAntecipacao CriarEntrada(
        decimal principalValor = 210_279.50m,
        Moeda moeda = Moeda.Usd,
        decimal taxaAaPct = 8.5m,
        int prazoTotalOriginalDias = 180,
        int baseCalculo = 360)
    {
        return new EntradaSimulacaoAntecipacao(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: null,
            PrazoTotalOriginalDias: prazoTotalOriginalDias,
            PrazoRemanescenteDias: 120,
            PrazoRemanescenteMeses: 4,
            TaxaAa: Percentual.De(taxaAaPct),
            BaseCalculo: baseCalculo,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);
    }

    // ── Teste 1: Cálculo golden — Annexo C §7.2 ───────────────────────────────

    [Fact]
    public void PadraoB_CalculaJurosPeriodoTotal()
    {
        // Arrange — golden dataset Sicredi §7.2
        // C3 = 210279.50 × 0.085 × 180 / 360 = 8936.879...  → rounded to 6dp = 8936.879750
        // TOTAL = 210279.50 + 8936.879750 = 219216.379750
        // Money rounds to 6dp AwayFromZero → 219216.379750
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 210_279.50m,
            taxaAaPct: 8.5m,
            prazoTotalOriginalDias: 180,
            baseCalculo: 360);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoBStrategy.Calcular(entrada, exigeAnuenciaExpressa: false);

        // Assert — golden values per Annexo C §7.2
        decimal c3Esperado = Math.Round(210_279.50m * 0.085m * 180m / 360m, 6, MidpointRounding.AwayFromZero);
        resultado.Componentes.First(c => c.Codigo == "C3").Valor.Valor
            .Should().Be(c3Esperado, because: "C3 deve ser principal × taxa × prazo_total / base");

        decimal totalEsperado = Math.Round(210_279.50m + c3Esperado, 6, MidpointRounding.AwayFromZero);
        resultado.TotalAQuitar.Valor.Should().Be(totalEsperado);
    }

    // ── Teste 2: Alerta crítico Sicredi ───────────────────────────────────────

    [Fact]
    public void PadraoB_EmiteAlertaCritico()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntrada();

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoBStrategy.Calcular(entrada, exigeAnuenciaExpressa: false);

        // Assert — alerta deve mencionar o problema de período total do Sicredi
        resultado.Alertas.Should().NotBeEmpty();
        string alertaUnido = string.Concat(resultado.Alertas);
        alertaUnido.Should().ContainAny("período total", "Sicredi");
    }

    // ── Teste 3: ExigeAnuenciaExpressa propagado ao resultado ─────────────────

    [Fact]
    public void PadraoB_ExigeAnuenciaExpressa_SetaFlag()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntrada();

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoBStrategy.Calcular(entrada, exigeAnuenciaExpressa: true);

        // Assert
        resultado.ExigeAnuenciaExpressa.Should().BeTrue();
    }
}
