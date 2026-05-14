using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Alertas;
using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Alertas;

[Trait("Category", "Domain")]
public sealed class AlertaVencimentoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 12, 8, 0);

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Guid contratoId = Guid.NewGuid();
        LocalDate dataVencimento = new(2026, 6, 15);
        LocalDate dataAlerta = new(2026, 6, 8);
        Money valor = new(1_000m, Moeda.Brl);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        AlertaVencimento alerta = AlertaVencimento.Criar(
            contratoId, "D_MENOS_7", dataVencimento, dataAlerta, valor, clock);

        // Assert
        alerta.ContratoId.Should().Be(contratoId);
        alerta.TipoAlerta.Should().Be("D_MENOS_7");
        alerta.DataVencimento.Should().Be(dataVencimento);
        alerta.DataAlerta.Should().Be(dataAlerta);
        alerta.Valor.Valor.Should().Be(1_000m);
        alerta.Valor.Moeda.Should().Be(Moeda.Brl);
        alerta.CriadoEm.Should().Be(AgoraFixo);
    }

    // ── Guard: contratoId vazio ─────────────────────────────────────────────

    [Fact]
    public void Criar_ContratoIdVazio_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaVencimento.Criar(
            Guid.Empty, "D_MENOS_7",
            new LocalDate(2026, 6, 15),
            new LocalDate(2026, 6, 8),
            new Money(100m, Moeda.Brl),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contratoId");
    }

    // ── Guard: tipoAlerta vazio / whitespace ────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_TipoAlertaVazio_LancaArgumentException(string tipoInvalido)
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaVencimento.Criar(
            Guid.NewGuid(), tipoInvalido,
            new LocalDate(2026, 6, 15),
            new LocalDate(2026, 6, 8),
            new Money(100m, Moeda.Brl),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("tipoAlerta");
    }

    // ── Arredondamento a 6 casas decimais ───────────────────────────────────

    [Fact]
    public void Criar_ValorArredondado6CasasDecimais()
    {
        // Arrange — 100.1234567890 rounds to 100.123457 (HalfUp at 7th decimal)
        Money valor = new(100.123456789m, Moeda.Brl);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        AlertaVencimento alerta = AlertaVencimento.Criar(
            Guid.NewGuid(), "D_MENOS_3",
            new LocalDate(2026, 7, 1),
            new LocalDate(2026, 6, 28),
            valor, clock);

        // Assert
        alerta.ValorDecimal.Should().Be(100.123457m);
    }

    // ── CriadoEm reflete o clock ────────────────────────────────────────────

    [Fact]
    public void Criar_CriadoEmRefleteClock()
    {
        // Arrange
        Instant esperado = Instant.FromUtc(2026, 3, 10, 14, 30);
        IClock clock = CriarClock(esperado);

        // Act
        AlertaVencimento alerta = AlertaVencimento.Criar(
            Guid.NewGuid(), "D_MENOS_0",
            new LocalDate(2026, 3, 10),
            new LocalDate(2026, 3, 10),
            new Money(500m, Moeda.Usd),
            clock);

        // Assert
        alerta.CriadoEm.Should().Be(esperado);
    }

    // ── Money preserva a moeda ──────────────────────────────────────────────

    [Fact]
    public void Criar_ValorMoneyPreservaMoeda()
    {
        // Arrange — valor em USD deve permanecer USD
        Money valor = new(250_000m, Moeda.Usd);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        AlertaVencimento alerta = AlertaVencimento.Criar(
            Guid.NewGuid(), "D_MENOS_7",
            new LocalDate(2026, 8, 1),
            new LocalDate(2026, 7, 25),
            valor, clock);

        // Assert
        alerta.Valor.Moeda.Should().Be(Moeda.Usd);
        alerta.Moeda.Should().Be(Moeda.Usd);
    }
}
