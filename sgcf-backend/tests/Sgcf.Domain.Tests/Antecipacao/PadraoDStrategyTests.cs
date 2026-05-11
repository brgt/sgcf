using FluentAssertions;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.Domain.Tests.Antecipacao;

[Trait("Category", "Domain")]
public sealed class PadraoDStrategyTests
{
    // ── Golden dataset — Annexo C §7.4 ────────────────────────────────────────
    // saldo=3500000, juros=21000, prazo_remanescente=18 meses
    // tlaSaldo=2%, tlaMes=0.1%
    // VTD = 3500000 + 21000 = 3521000
    // TLA_A = 3521000 × 0.02 = 70420
    // TLA_B = 3521000 × 0.001 × 18 = 63378
    // TLA = max(70420, 63378) = 70420
    // TOTAL = 3521000 + 70420 = 3591420

    private static EntradaSimulacaoAntecipacao CriarEntradaGolden(bool refinanciamentoInterno = false)
    {
        return new EntradaSimulacaoAntecipacao(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(3_500_000m, Moeda.Brl),
            JurosProRata: new Money(21_000m, Moeda.Brl),
            PrazoTotalOriginalDias: 720,
            PrazoRemanescenteDias: 540,
            PrazoRemanescenteMeses: 18,
            TaxaAa: Percentual.De(12m),
            BaseCalculo: 360,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: refinanciamentoInterno);
    }

    // ── Teste 1: VTD e TLA corretos ───────────────────────────────────────────

    [Fact]
    public void PadraoD_CalculaVtdEtla()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntradaGolden();
        Percentual tlaSaldo = Percentual.DeFracao(0.02m);
        Percentual tlaMes = Percentual.DeFracao(0.001m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoDStrategy.Calcular(entrada, tlaSaldo, tlaMes);

        // Assert — golden values
        resultado.TotalAQuitar.Valor.Should().Be(3_591_420m);
    }

    // ── Teste 2: max(TLA_A, TLA_B) aplicado ──────────────────────────────────

    [Fact]
    public void PadraoD_UsaMaxTla()
    {
        // Arrange — com prazo curto, TLA_A seria maior que TLA_B
        // VTD = 3521000, TLA_A = 70420, TLA_B = 63378 → max = 70420
        EntradaSimulacaoAntecipacao entrada = CriarEntradaGolden();
        Percentual tlaSaldo = Percentual.DeFracao(0.02m);
        Percentual tlaMes = Percentual.DeFracao(0.001m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoDStrategy.Calcular(entrada, tlaSaldo, tlaMes);

        // Assert — TLA no componente deve ser max(70420, 63378) = 70420
        ComponenteCusto tlaComponente = resultado.Componentes.First(c => c.Codigo == "TLA");
        tlaComponente.Valor.Valor.Should().Be(70_420m);
    }

    // ── Teste 3: Refinanciamento interno — TLA isenta ─────────────────────────

    [Fact]
    public void PadraoD_RefinanciamentoInterno_TlaIsento()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntradaGolden(refinanciamentoInterno: true);
        Percentual tlaSaldo = Percentual.DeFracao(0.02m);
        Percentual tlaMes = Percentual.DeFracao(0.001m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoDStrategy.Calcular(entrada, tlaSaldo, tlaMes);

        // Assert — TLA = 0, TOTAL = VTD
        decimal vtdEsperado = 3_521_000m;
        resultado.TotalAQuitar.Valor.Should().Be(vtdEsperado,
            because: "refinanciamento interno é isento de TLA (Res. BACEN 3401/06 §3º)");

        ComponenteCusto tlaComponente = resultado.Componentes.First(c => c.Codigo == "TLA");
        tlaComponente.Valor.Valor.Should().Be(0m);
    }

    // ── Teste 4: Alerta de isenção presente no refinanciamento interno ─────────

    [Fact]
    public void PadraoD_RefinanciamentoInterno_AlertaIsencao()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntradaGolden(refinanciamentoInterno: true);
        Percentual tlaSaldo = Percentual.DeFracao(0.02m);
        Percentual tlaMes = Percentual.DeFracao(0.001m);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoDStrategy.Calcular(entrada, tlaSaldo, tlaMes);

        // Assert — alerta deve mencionar isenção e refinanciamento
        resultado.Alertas.Should().NotBeEmpty();
        string alertaUnido = string.Concat(resultado.Alertas).ToLowerInvariant();
        alertaUnido.Should().ContainAny("isenção", "refinanciamento");
    }
}
