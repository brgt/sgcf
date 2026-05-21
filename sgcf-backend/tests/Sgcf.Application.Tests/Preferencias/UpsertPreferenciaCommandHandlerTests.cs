using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Preferencias;
using Sgcf.Application.Preferencias.Commands;
using Sgcf.Domain.Preferencias;
using Xunit;

namespace Sgcf.Application.Tests.Preferencias;

[Trait("Category", "Domain")]
public sealed class UpsertPreferenciaCommandHandlerTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 21, 10, 0);

    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    // ── Caminho CREATE ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PreferenciaInexistente_CriaEPersiste()
    {
        // Arrange
        IPreferenciaUsuarioRepository repo = Substitute.For<IPreferenciaUsuarioRepository>();
        repo.GetAsync("auth0|user1", "theme", Arg.Any<CancellationToken>())
            .Returns((PreferenciaUsuario?)null);

        IClock clock = CriarClock(AgoraFixo);
        UpsertPreferenciaCommandHandler handler = new(repo, clock);

        // Act
        PreferenciaUsuarioDto resultado = await handler.Handle(
            new UpsertPreferenciaCommand("auth0|user1", "theme", "dark"),
            CancellationToken.None);

        // Assert
        resultado.Chave.Should().Be("theme");
        resultado.Valor.Should().Be("dark");
        resultado.AtualizadoEm.Should().Be(AgoraFixo.ToString());

        repo.Received(1).Add(Arg.Is<PreferenciaUsuario>(p =>
            p.UserId == "auth0|user1" &&
            p.Chave == "theme" &&
            p.Valor == "dark"));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Caminho UPDATE ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PreferenciaExistente_AtualizaValorSemAdicionarNova()
    {
        // Arrange
        Instant t0 = Instant.FromUtc(2026, 5, 20, 8, 0);
        Instant t1 = AgoraFixo;

        PreferenciaUsuario existente = PreferenciaUsuario.Criar("auth0|user1", "theme", "light", t0);

        IPreferenciaUsuarioRepository repo = Substitute.For<IPreferenciaUsuarioRepository>();
        repo.GetAsync("auth0|user1", "theme", Arg.Any<CancellationToken>())
            .Returns(existente);

        IClock clock = CriarClock(t1);
        UpsertPreferenciaCommandHandler handler = new(repo, clock);

        // Act
        PreferenciaUsuarioDto resultado = await handler.Handle(
            new UpsertPreferenciaCommand("auth0|user1", "theme", "dark"),
            CancellationToken.None);

        // Assert — valor atualizado, timestamp avançado, Add NÃO chamado
        resultado.Valor.Should().Be("dark");
        resultado.AtualizadoEm.Should().Be(t1.ToString());
        existente.AtualizadoEm.Should().Be(t1);

        repo.DidNotReceive().Add(Arg.Any<PreferenciaUsuario>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
