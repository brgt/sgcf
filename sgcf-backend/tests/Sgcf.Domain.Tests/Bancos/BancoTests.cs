using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Bancos;

[Trait("Category", "Domain")]
public sealed class BancoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    // ── Criar — happy path ──────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 5, 11, 10, 0);
        IClock clock = CriarClock(agora);

        // Act
        Banco banco = Banco.Criar("001", "Banco do Brasil SA", "BB", clock);

        // Assert
        banco.CodigoCompe.Should().Be("001");
        banco.RazaoSocial.Should().Be("Banco do Brasil SA");
        banco.Apelido.Should().Be("BB");
        banco.CreatedAt.Should().Be(agora);
        banco.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Criar_CodigoCompeEmMinusculas_ArmazenaEmMaiusculas()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Banco banco = Banco.Criar("abc", "Razao Social", "Apelido", clock);

        // Assert
        banco.CodigoCompe.Should().Be("ABC");
    }

    [Fact]
    public void Criar_IdNaoEhVazio()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Banco banco = Banco.Criar("001", "Banco do Brasil SA", "BB", clock);

        // Assert
        banco.Id.Should().NotBeEmpty();
    }

    // ── Criar — guard: codigoCompe deve ter exatamente 3 chars ─────────────

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("1234")]
    [InlineData("ABCD")]
    public void Criar_CodigoCompeForaDe3Chars_LancaArgumentException(string codigoInvalido)
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Banco.Criar(codigoInvalido, "Razao Social", "Apelido", clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("codigoCompe");
    }

    // ── Criar — guard: codigoCompe vazio / whitespace ───────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_CodigoCompeVazioOuWhitespace_LancaArgumentException(string codigoVazio)
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Banco.Criar(codigoVazio, "Razao Social", "Apelido", clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("codigoCompe");
    }

    // ── Criar — guard: razaoSocial vazio ────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_RazaoSocialVazia_LancaArgumentException(string razaoVazia)
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Banco.Criar("001", razaoVazia, "Apelido", clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("razaoSocial");
    }

    // ── Criar — guard: apelido vazio ─────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ApelidoVazio_LancaArgumentException(string apelidoVazio)
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Banco.Criar("001", "Razao Social", apelidoVazio, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("apelido");
    }

    // ── AtualizarConfigAntecipacao — atualiza campos institucionais ──────────

    [Fact]
    public void AtualizarConfigAntecipacao_ComTodosOsCampos_AtualizaPropriedades()
    {
        // Arrange
        Instant criacaoInstant = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant atualizacaoInstant = Instant.FromUtc(2026, 5, 11, 11, 0);

        IClock clockCriacao = CriarClock(criacaoInstant);
        IClock clockAtualizacao = CriarClock(atualizacaoInstant);

        Banco banco = Banco.Criar("001", "Banco do Brasil SA", "BB", clockCriacao);

        // Act
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: false,
            exigeAnuenciaExpressa: true,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 5,
            clock: clockAtualizacao);

        // Assert
        banco.AceitaLiquidacaoTotal.Should().BeTrue();
        banco.AceitaLiquidacaoParcial.Should().BeFalse();
        banco.ExigeAnuenciaExpressa.Should().BeTrue();
        banco.ExigeParcelaInteira.Should().BeFalse();
        banco.AvisoPrevioMinDiasUteis.Should().Be(5);
        banco.UpdatedAt.Should().Be(atualizacaoInstant);
        banco.CreatedAt.Should().Be(criacaoInstant);
    }

    [Fact]
    public void AtualizarConfigAntecipacao_UpdatedAtAvanca_CreatedAtPermanece()
    {
        // Arrange
        Instant criacaoInstant = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant atualizacaoInstant = Instant.FromUtc(2026, 5, 12, 8, 0);

        Banco banco = Banco.Criar("001", "Razao Social", "Apelido", CriarClock(criacaoInstant));

        // Act
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: false,
            aceitaLiquidacaoParcial: false,
            exigeAnuenciaExpressa: false,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 0,
            clock: CriarClock(atualizacaoInstant));

        // Assert
        banco.CreatedAt.Should().Be(criacaoInstant);
        banco.UpdatedAt.Should().Be(atualizacaoInstant);
        banco.UpdatedAt.Should().BeGreaterThan(banco.CreatedAt);
    }

    // ── AtualizarConfigAntecipacao — campos booleanos são armazenados ────────

    [Fact]
    public void AtualizarConfigAntecipacao_ExigeAnuenciaExpressa_EhArmazenado()
    {
        // Arrange
        Banco banco = Banco.Criar("001", "Razao Social", "Apelido",
            CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0)));

        // Act
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: true,
            exigeAnuenciaExpressa: true,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 3,
            clock: CriarClock(Instant.FromUtc(2026, 5, 11, 11, 0)));

        // Assert
        banco.ExigeAnuenciaExpressa.Should().BeTrue();
        banco.AvisoPrevioMinDiasUteis.Should().Be(3);
    }

    // ── Nota: PadraoAntecipacao, BreakFundingFeePct, TLA, ValorMinimoParcialPct
    //    e ObservacoesAntecipacao foram movidos para LimiteBanco (S32).
    //    Consulte os testes em Sgcf.Domain.Tests.Cotacoes.LimiteBancoTests
    //    para cobertura desses campos.
}
