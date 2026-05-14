using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Alertas;
using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Alertas;

[Trait("Category", "Domain")]
public sealed class AlertaExposicaoBancoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 12, 8, 0);
    private static readonly LocalDate DataAlertaFixa = new(2026, 5, 12);

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange — exposição 80k / limite 100k = 80% ocupação
        Guid bancoId = Guid.NewGuid();
        Money exposicao = new(800_000m, Moeda.Brl);
        Money limite = new(1_000_000m, Moeda.Brl);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        AlertaExposicaoBanco alerta = AlertaExposicaoBanco.Criar(
            bancoId, DataAlertaFixa, exposicao, limite, clock);

        // Assert
        alerta.BancoId.Should().Be(bancoId);
        alerta.DataAlerta.Should().Be(DataAlertaFixa);
        alerta.ExposicaoBrl.Valor.Should().Be(800_000m);
        alerta.ExposicaoBrl.Moeda.Should().Be(Moeda.Brl);
        alerta.LimiteBrl.Valor.Should().Be(1_000_000m);
        alerta.LimiteBrl.Moeda.Should().Be(Moeda.Brl);
        alerta.PercentualOcupacao.Should().Be(0.8m);
        alerta.CriadoEm.Should().Be(AgoraFixo);
    }

    // ── Guard: bancoId vazio ────────────────────────────────────────────────

    [Fact]
    public void Criar_BancoIdVazio_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaExposicaoBanco.Criar(
            Guid.Empty, DataAlertaFixa,
            new Money(500_000m, Moeda.Brl),
            new Money(1_000_000m, Moeda.Brl),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("bancoId");
    }

    // ── Guard: exposição em moeda errada ────────────────────────────────────

    [Fact]
    public void Criar_ExposicaoEmMoedaErrada_LancaArgumentException()
    {
        // Arrange — USD em vez de BRL
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaExposicaoBanco.Criar(
            Guid.NewGuid(), DataAlertaFixa,
            new Money(500_000m, Moeda.Usd),   // deve ser BRL
            new Money(1_000_000m, Moeda.Brl),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("exposicaoBrl");
    }

    // ── Guard: limite em moeda errada ───────────────────────────────────────

    [Fact]
    public void Criar_LimiteEmMoedaErrada_LancaArgumentException()
    {
        // Arrange — EUR em vez de BRL
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaExposicaoBanco.Criar(
            Guid.NewGuid(), DataAlertaFixa,
            new Money(500_000m, Moeda.Brl),
            new Money(1_000_000m, Moeda.Eur),  // deve ser BRL
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("limiteBrl");
    }

    // ── Guard: limite zero ──────────────────────────────────────────────────

    [Fact]
    public void Criar_LimiteZero_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaExposicaoBanco.Criar(
            Guid.NewGuid(), DataAlertaFixa,
            new Money(500_000m, Moeda.Brl),
            new Money(0m, Moeda.Brl),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("limiteBrl");
    }

    // ── Guard: limite negativo ──────────────────────────────────────────────

    [Fact]
    public void Criar_LimiteNegativo_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => AlertaExposicaoBanco.Criar(
            Guid.NewGuid(), DataAlertaFixa,
            new Money(500_000m, Moeda.Brl),
            new Money(-100_000m, Moeda.Brl),
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("limiteBrl");
    }

    // ── Cálculo percentual ──────────────────────────────────────────────────

    [Fact]
    public void Criar_PercentualOcupacaoCalculadoCorretamente()
    {
        // Arrange — 800k / 1M = 0.8
        IClock clock = CriarClock(AgoraFixo);

        // Act
        AlertaExposicaoBanco alerta = AlertaExposicaoBanco.Criar(
            Guid.NewGuid(), DataAlertaFixa,
            new Money(800_000m, Moeda.Brl),
            new Money(1_000_000m, Moeda.Brl),
            clock);

        // Assert
        alerta.PercentualOcupacao.Should().Be(0.8m);
    }

    [Fact]
    public void Criar_ExposicaoAcimaDoLimite_PercentualAcimaDe1()
    {
        // Arrange — 1.2M / 1M = 1.2 (exposição acima do limite é válida para registrar o alerta)
        IClock clock = CriarClock(AgoraFixo);

        // Act
        AlertaExposicaoBanco alerta = AlertaExposicaoBanco.Criar(
            Guid.NewGuid(), DataAlertaFixa,
            new Money(1_200_000m, Moeda.Brl),
            new Money(1_000_000m, Moeda.Brl),
            clock);

        // Assert
        alerta.PercentualOcupacao.Should().Be(1.2m);
    }
}
