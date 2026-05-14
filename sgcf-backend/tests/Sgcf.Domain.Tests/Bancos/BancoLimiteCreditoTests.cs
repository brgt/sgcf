using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Bancos;

[Trait("Category", "Domain")]
public sealed class BancoLimiteCreditoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Banco CriarBancoValido(Instant instant)
    {
        return Banco.Criar("001", "Banco do Brasil SA", "BB", PadraoAntecipacao.A, CriarClock(instant));
    }

    private static readonly Instant InstantCriacao = Instant.FromUtc(2026, 5, 1, 10, 0);
    private static readonly Instant InstantAtualizacao = Instant.FromUtc(2026, 5, 12, 14, 0);

    // ── AtualizarLimiteCredito: valor positivo ──────────────────────────────

    [Fact]
    public void AtualizarLimiteCredito_ComValorPositivo_SetaLimiteCreditoBrl()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        IClock clock = CriarClock(InstantAtualizacao);

        // Act
        banco.AtualizarLimiteCredito(5_000_000m, clock);

        // Assert
        banco.LimiteCreditoBrl.Should().NotBeNull();
        banco.LimiteCreditoBrl!.Value.Valor.Should().Be(5_000_000m);
    }

    // ── AtualizarLimiteCredito: valor nulo desabilita monitoramento ─────────

    [Fact]
    public void AtualizarLimiteCredito_ComNulo_LimiteCreditoBrlEhNull()
    {
        // Arrange — banco com limite já definido
        Banco banco = CriarBancoValido(InstantCriacao);
        banco.AtualizarLimiteCredito(1_000_000m, CriarClock(InstantCriacao));

        // Act
        banco.AtualizarLimiteCredito(null, CriarClock(InstantAtualizacao));

        // Assert
        banco.LimiteCreditoBrl.Should().BeNull();
    }

    // ── AtualizarLimiteCredito: atualiza UpdatedAt ──────────────────────────

    [Fact]
    public void AtualizarLimiteCredito_AtualizaUpdatedAt()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        IClock clock = CriarClock(InstantAtualizacao);

        // Act
        banco.AtualizarLimiteCredito(2_000_000m, clock);

        // Assert
        banco.UpdatedAt.Should().Be(InstantAtualizacao);
    }

    // ── AtualizarLimiteCredito: não altera CreatedAt ────────────────────────

    [Fact]
    public void AtualizarLimiteCredito_NaoAlteraCreatedAt()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        IClock clock = CriarClock(InstantAtualizacao);

        // Act
        banco.AtualizarLimiteCredito(2_000_000m, clock);

        // Assert
        banco.CreatedAt.Should().Be(InstantCriacao);
    }

    // ── LimiteCreditoBrl property retorna Money em BRL ──────────────────────

    [Fact]
    public void LimiteCreditoBrl_QuandoSetado_RetornaMoneyEmBrl()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        IClock clock = CriarClock(InstantAtualizacao);
        banco.AtualizarLimiteCredito(3_000_000m, clock);

        // Act
        Money? limite = banco.LimiteCreditoBrl;

        // Assert
        limite.Should().NotBeNull();
        limite!.Value.Moeda.Should().Be(Moeda.Brl);
        limite.Value.Valor.Should().Be(3_000_000m);
    }

    // ── Guard: valor não positivo lança exceção ─────────────────────────────

    [Fact]
    public void AtualizarLimiteCredito_ComValorZero_LancaArgumentException()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        IClock clock = CriarClock(InstantAtualizacao);

        // Act
        Action act = () => banco.AtualizarLimiteCredito(0m, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("limiteBrl");
    }

    [Fact]
    public void AtualizarLimiteCredito_ComValorNegativo_LancaArgumentException()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        IClock clock = CriarClock(InstantAtualizacao);

        // Act
        Action act = () => banco.AtualizarLimiteCredito(-100_000m, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("limiteBrl");
    }
}
