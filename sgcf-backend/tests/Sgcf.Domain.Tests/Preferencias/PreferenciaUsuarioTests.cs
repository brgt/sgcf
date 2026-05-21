using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Preferencias;
using Xunit;

namespace Sgcf.Domain.Tests.Preferencias;

[Trait("Category", "Domain")]
public sealed class PreferenciaUsuarioTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 21, 10, 0);

    // ── Criar — happy path ──────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        PreferenciaUsuario p = PreferenciaUsuario.Criar(
            userId: "auth0|abc123",
            chave: "cockpit.layout",
            valor: """{"cols":3}""",
            agora: AgoraFixo);

        p.UserId.Should().Be("auth0|abc123");
        p.Chave.Should().Be("cockpit.layout");
        p.Valor.Should().Be("""{"cols":3}""");
        p.AtualizadoEm.Should().Be(AgoraFixo);
        p.Id.Should().NotBe(Guid.Empty);
    }

    // ── Validações de UserId ────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_UserIdVazioOuBranco_LancaArgumentException(string? userId)
    {
        Action act = () => PreferenciaUsuario.Criar(userId!, "theme", "dark", AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId*");
    }

    // ── Validações de Chave ─────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Criar_ChaveVaziaOuBranca_LancaArgumentException(string? chave)
    {
        Action act = () => PreferenciaUsuario.Criar("auth0|user1", chave!, "dark", AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Chave*");
    }

    [Fact]
    public void Criar_ChaveLongaDemais_LancaArgumentException()
    {
        string chaveLonga = new('x', 101);

        Action act = () => PreferenciaUsuario.Criar("auth0|user1", chaveLonga, "dark", AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Chave*");
    }

    [Fact]
    public void Criar_ChaveComExatamente100Chars_NaoLancaExcecao()
    {
        string chave100 = new('k', 100);

        Action act = () => PreferenciaUsuario.Criar("auth0|user1", chave100, "dark", AgoraFixo);

        act.Should().NotThrow();
    }

    // ── Validações de Valor ─────────────────────────────────────────────────

    [Fact]
    public void Criar_ValorLongoDemais_LancaArgumentException()
    {
        string valorLongo = new('v', 4001);

        Action act = () => PreferenciaUsuario.Criar("auth0|user1", "theme", valorLongo, AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Valor*");
    }

    [Fact]
    public void Criar_ValorComExatamente4000Chars_NaoLancaExcecao()
    {
        string valor4000 = new('v', 4000);

        Action act = () => PreferenciaUsuario.Criar("auth0|user1", "theme", valor4000, AgoraFixo);

        act.Should().NotThrow();
    }

    // ── AtualizarValor ──────────────────────────────────────────────────────

    [Fact]
    public void AtualizarValor_ComValorValido_AtualizaValorETimestamp()
    {
        Instant t1 = Instant.FromUtc(2026, 5, 22, 9, 0);

        PreferenciaUsuario p = PreferenciaUsuario.Criar(
            "auth0|user1", "theme", "light", AgoraFixo);

        p.AtualizarValor("dark", t1);

        p.Valor.Should().Be("dark");
        p.AtualizadoEm.Should().Be(t1);
    }

    [Fact]
    public void AtualizarValor_ValorLongoDemais_LancaArgumentException()
    {
        PreferenciaUsuario p = PreferenciaUsuario.Criar(
            "auth0|user1", "theme", "light", AgoraFixo);

        string valorLongo = new('v', 4001);

        Action act = () => p.AtualizarValor(valorLongo, AgoraFixo);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Valor*");
    }
}
