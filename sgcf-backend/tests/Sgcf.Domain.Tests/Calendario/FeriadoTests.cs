using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Calendario;

using Xunit;

namespace Sgcf.Domain.Tests.Calendario;

[Trait("Category", "Domain")]
public sealed class FeriadoTests
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
        Instant agora = Instant.FromUtc(2026, 5, 14, 12, 0);
        IClock clock = CriarClock(agora);

        Feriado feriado = Feriado.Criar(
            data: new LocalDate(2026, 12, 25),
            tipo: TipoFeriado.Nacional,
            escopo: EscopoFeriado.Brasil,
            descricao: "Natal",
            fonte: FonteFeriado.Anbima,
            anoReferencia: 2026,
            clock: clock);

        feriado.Data.Should().Be(new LocalDate(2026, 12, 25));
        feriado.Tipo.Should().Be(TipoFeriado.Nacional);
        feriado.Escopo.Should().Be(EscopoFeriado.Brasil);
        feriado.Descricao.Should().Be("Natal");
        feriado.Fonte.Should().Be(FonteFeriado.Anbima);
        feriado.AnoReferencia.Should().Be(2026);
        feriado.CreatedAt.Should().Be(agora);
        feriado.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Criar_IdNaoEhVazio()
    {
        Feriado feriado = Feriado.Criar(
            new LocalDate(2026, 1, 1),
            TipoFeriado.Nacional,
            EscopoFeriado.Brasil,
            "Confraternização Universal",
            FonteFeriado.Anbima,
            2026,
            CriarClock(Instant.FromUtc(2026, 5, 14, 12, 0)));

        feriado.Id.Should().NotBe(Guid.Empty);
    }

    // ── Validações ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Criar_DescricaoVaziaOuBranca_LancaArgumentException(string? descricao)
    {
        Action act = () => Feriado.Criar(
            new LocalDate(2026, 12, 25),
            TipoFeriado.Nacional,
            EscopoFeriado.Brasil,
            descricao!,
            FonteFeriado.Anbima,
            2026,
            CriarClock(Instant.FromUtc(2026, 5, 14, 12, 0)));

        act.Should().Throw<ArgumentException>().WithMessage("*Descricao*");
    }

    [Fact]
    public void Criar_AnoReferenciaDivergeDoAnoDaData_LancaArgumentException()
    {
        Action act = () => Feriado.Criar(
            new LocalDate(2026, 12, 25),
            TipoFeriado.Nacional,
            EscopoFeriado.Brasil,
            "Natal",
            FonteFeriado.Anbima,
            anoReferencia: 2027,
            clock: CriarClock(Instant.FromUtc(2026, 5, 14, 12, 0)));

        act.Should().Throw<ArgumentException>().WithMessage("*AnoReferencia*");
    }

    [Fact]
    public void Criar_DescricaoLongaDemais_LancaArgumentException()
    {
        string descricaoLonga = new('A', 121);

        Action act = () => Feriado.Criar(
            new LocalDate(2026, 12, 25),
            TipoFeriado.Nacional,
            EscopoFeriado.Brasil,
            descricaoLonga,
            FonteFeriado.Anbima,
            2026,
            CriarClock(Instant.FromUtc(2026, 5, 14, 12, 0)));

        act.Should().Throw<ArgumentException>().WithMessage("*Descricao*");
    }

    // ── Atualizar descrição ─────────────────────────────────────────────────

    [Fact]
    public void AtualizarDescricao_ComValorValido_PersisteEAtualizaTimestamp()
    {
        Instant t0 = Instant.FromUtc(2026, 5, 14, 12, 0);
        Instant t1 = Instant.FromUtc(2026, 5, 15, 12, 0);
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(t0, t1);

        Feriado feriado = Feriado.Criar(
            new LocalDate(2026, 12, 25),
            TipoFeriado.Nacional,
            EscopoFeriado.Brasil,
            "Natal",
            FonteFeriado.Anbima,
            2026,
            clock);

        feriado.AtualizarDescricao("Natal (descricao revisada)", clock);

        feriado.Descricao.Should().Be("Natal (descricao revisada)");
        feriado.UpdatedAt.Should().Be(t1);
        feriado.CreatedAt.Should().Be(t0);
    }
}
