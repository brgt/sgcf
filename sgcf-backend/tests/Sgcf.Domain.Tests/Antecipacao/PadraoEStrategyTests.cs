using FluentAssertions;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.Domain.Tests.Antecipacao;

[Trait("Category", "Domain")]
public sealed class PadraoEStrategyTests
{
    private static EntradaSimulacaoAntecipacao CriarEntrada(
        decimal principalValor = 1_000_000m,
        decimal? jurosProRataValor = null,
        Moeda moeda = Moeda.Brl)
    {
        return new EntradaSimulacaoAntecipacao(
            Tipo: TipoAntecipacao.LiquidacaoParcialReducaoPrazo,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: jurosProRataValor.HasValue ? new Money(jurosProRataValor.Value, moeda) : (Money?)null,
            PrazoTotalOriginalDias: 720,
            PrazoRemanescenteDias: 360,
            PrazoRemanescenteMeses: 12,
            TaxaAa: Percentual.De(10m),
            BaseCalculo: 360,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);
    }

    // ── Teste 1: Com abatimento — reduz total ─────────────────────────────────

    [Fact]
    public void PadraoE_ComAbatimento_ReducTotal()
    {
        // Arrange — saldo=1000000, abatimento=50000 → TOTAL = 950000
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 1_000_000m);

        Money abatimento = new(50_000m, Moeda.Brl);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoEStrategy.Calcular(entrada, abatimento);

        // Assert
        resultado.TotalAQuitar.Valor.Should().Be(950_000m);
        resultado.Componentes.Should().Contain(c => c.Codigo == "ABAT" && c.Sinal == "-");
    }

    // ── Teste 2: Sem abatimento — total = principal ───────────────────────────

    [Fact]
    public void PadraoE_SemAbatimento_TotalEhPrincipal()
    {
        // Arrange
        EntradaSimulacaoAntecipacao entrada = CriarEntrada(
            principalValor: 1_000_000m,
            jurosProRataValor: null);

        // Act — jurosEmbebidosFuturos = null, JurosProRata = null → sem abatimento
        ResultadoSimulacaoAntecipacao resultado = PadraoEStrategy.Calcular(entrada, null);

        // Assert
        resultado.TotalAQuitar.Valor.Should().Be(1_000_000m);
        resultado.Componentes.Should().NotContain(c => c.Codigo == "ABAT");
    }
}
