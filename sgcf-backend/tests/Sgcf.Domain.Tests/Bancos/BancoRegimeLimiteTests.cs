using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Bancos;

using Xunit;

namespace Sgcf.Domain.Tests.Bancos;

[Trait("Category", "Domain")]
public sealed class BancoRegimeLimiteTests
{
    private static readonly Instant InstantCriacao = Instant.FromUtc(2026, 5, 1, 10, 0);
    private static readonly Instant InstantAtualizacao = Instant.FromUtc(2026, 5, 12, 14, 0);

    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Banco CriarBancoValido(Instant instant)
    {
        return Banco.Criar("001", "Banco do Brasil SA", "BB", CriarClock(instant));
    }

    // ── Default: banco nasce em regime per-modalidade (SPEC §3.2) ───────────

    [Fact]
    public void Criar_BancoNasce_ComRegimePerModalidade()
    {
        // Arrange + Act
        Banco banco = CriarBancoValido(InstantCriacao);

        // Assert
        banco.RegimeLimite.Should().Be(RegimeLimiteBanco.PerModalidade);
    }

    // ── DefinirRegimeLimite altera o regime ─────────────────────────────────

    [Fact]
    public void DefinirRegimeLimite_AlteraRegime()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);

        // Act
        banco.DefinirRegimeLimite(RegimeLimiteBanco.GlobalPuro, CriarClock(InstantAtualizacao));

        // Assert
        banco.RegimeLimite.Should().Be(RegimeLimiteBanco.GlobalPuro);
    }

    // ── DefinirRegimeLimite atualiza UpdatedAt ──────────────────────────────

    [Fact]
    public void DefinirRegimeLimite_AtualizaUpdatedAt()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);

        // Act
        banco.DefinirRegimeLimite(RegimeLimiteBanco.GlobalPuro, CriarClock(InstantAtualizacao));

        // Assert
        banco.UpdatedAt.Should().Be(InstantAtualizacao);
    }

    // ── DefinirRegimeLimite não altera CreatedAt ────────────────────────────

    [Fact]
    public void DefinirRegimeLimite_NaoAlteraCreatedAt()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);

        // Act
        banco.DefinirRegimeLimite(RegimeLimiteBanco.GlobalPuro, CriarClock(InstantAtualizacao));

        // Assert
        banco.CreatedAt.Should().Be(InstantCriacao);
    }

    // ── Voltar para per-modalidade é permitido (REG-04) ─────────────────────

    [Fact]
    public void DefinirRegimeLimite_VoltarParaPerModalidade_Permitido()
    {
        // Arrange
        Banco banco = CriarBancoValido(InstantCriacao);
        banco.DefinirRegimeLimite(RegimeLimiteBanco.GlobalPuro, CriarClock(InstantAtualizacao));

        // Act
        banco.DefinirRegimeLimite(RegimeLimiteBanco.PerModalidade, CriarClock(InstantAtualizacao));

        // Assert
        banco.RegimeLimite.Should().Be(RegimeLimiteBanco.PerModalidade);
    }
}
