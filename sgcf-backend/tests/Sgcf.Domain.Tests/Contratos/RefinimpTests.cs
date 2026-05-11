using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class RefinimpTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Contrato CriarContratoFinimp(IClock clock)
    {
        return Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    // ── Contrato.MarcarRefinanciadoParcial ──────────────────────────────────────

    [Fact]
    public void MarcarRefinanciadoParcial_AlteraStatusParaRefinanciadoParcial()
    {
        // Arrange
        Instant criacao = Instant.FromUtc(2026, 1, 15, 10, 0);
        Instant refinanciamento = Instant.FromUtc(2026, 6, 1, 9, 0);

        Contrato contrato = CriarContratoFinimp(CriarClock(criacao));

        // Act
        contrato.MarcarRefinanciadoParcial(CriarClock(refinanciamento));

        // Assert
        contrato.Status.Should().Be(StatusContrato.RefinanciadoParcial);
        contrato.UpdatedAt.Should().Be(refinanciamento);
    }

    // ── Contrato.MarcarRefinanciadoTotal ────────────────────────────────────────

    [Fact]
    public void MarcarRefinanciadoTotal_AlteraStatusParaRefinanciadoTotal()
    {
        // Arrange
        Instant criacao = Instant.FromUtc(2026, 1, 15, 10, 0);
        Instant refinanciamento = Instant.FromUtc(2026, 6, 1, 9, 0);

        Contrato contrato = CriarContratoFinimp(CriarClock(criacao));

        // Act
        contrato.MarcarRefinanciadoTotal(CriarClock(refinanciamento));

        // Assert
        contrato.Status.Should().Be(StatusContrato.RefinanciadoTotal);
        contrato.UpdatedAt.Should().Be(refinanciamento);
    }

    // ── RefinimpDetail.Criar ────────────────────────────────────────────────────

    [Fact]
    public void RefinimpDetail_Criar_DefinePercentualComoFracao()
    {
        // Arrange — 70% expressed as fraction 0.70
        IClock clock = CriarClock(Instant.FromUtc(2026, 6, 1, 9, 0));
        Guid contratoId = Guid.NewGuid();
        Guid contratoMaeId = Guid.NewGuid();
        Percentual percentual = Percentual.DeFracao(0.70m);
        Money valorQuitado = new(700_000m, Moeda.Usd);

        // Act
        RefinimpDetail detail = RefinimpDetail.Criar(
            contratoId,
            contratoMaeId,
            percentual,
            valorQuitado,
            clock);

        // Assert
        detail.ContratoId.Should().Be(contratoId);
        detail.ContratoMaeId.Should().Be(contratoMaeId);
        detail.PercentualRefinanciado.AsDecimal.Should().Be(0.70m);
        detail.PercentualRefinanciado.AsHumano.Should().Be(70m);
        detail.ValorQuitadoNoRefi.Valor.Should().Be(700_000m);
        detail.ValorQuitadoNoRefi.Moeda.Should().Be(Moeda.Usd);
        detail.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void RefinimpDetail_Criar_ContratoIdVazio_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 6, 1, 9, 0));

        // Act
        Action act = () => RefinimpDetail.Criar(
            Guid.Empty,
            Guid.NewGuid(),
            Percentual.DeFracao(0.50m),
            new Money(500_000m, Moeda.Usd),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contratoId");
    }

    [Fact]
    public void RefinimpDetail_Criar_ContratoMaeIdVazio_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 6, 1, 9, 0));

        // Act
        Action act = () => RefinimpDetail.Criar(
            Guid.NewGuid(),
            Guid.Empty,
            Percentual.DeFracao(0.50m),
            new Money(500_000m, Moeda.Usd),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contratoMaeId");
    }
}
