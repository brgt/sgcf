using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Sistema;
using Xunit;

namespace Sgcf.Domain.Tests.Sistema;

/// <summary>
/// Testes unitários para <see cref="ParametroSistema"/>.
/// Fase 3 Task 3.4 — tetão mensal configurável (D-11).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ParametroSistemaTests
{
    private static IClock CriarClock(Instant instante)
    {
        IClock c = Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(instante);
        return c;
    }

    private static IClock CriarClockFixo()
        => CriarClock(Instant.FromUtc(2026, 5, 19, 12, 0));

    // ── Teste 1: Criar sem tetão → TetaoMensalCapacidadeBrl é null ───────────

    [Fact]
    public void Criar_comTetaoNull_funciona()
    {
        // Arrange
        IClock clock = CriarClockFixo();

        // Act
        ParametroSistema parametro = ParametroSistema.Criar(clock);

        // Assert
        parametro.Should().NotBeNull();
        parametro.TetaoMensalCapacidadeBrl.Should().BeNull(
            "parâmetro novo não tem tetão configurado");
        parametro.Id.Should().NotBeEmpty();
    }

    // ── Teste 2: AtualizarTetao → campo atualiza e timestamp avança ──────────

    [Fact]
    public void AtualizarTetao_atualizaValor()
    {
        // Arrange
        Instant t1 = Instant.FromUtc(2026, 5, 19, 12, 0);
        Instant t2 = Instant.FromUtc(2026, 5, 19, 13, 0);

        ParametroSistema parametro = ParametroSistema.Criar(CriarClock(t1));
        Money novoTetao = new(5_000_000m, Moeda.Brl);

        // Act
        parametro.AtualizarTetaoMensal(novoTetao, CriarClock(t2));

        // Assert
        parametro.TetaoMensalCapacidadeBrl.Should().NotBeNull();
        parametro.TetaoMensalCapacidadeBrl!.Value.Valor.Should().Be(5_000_000m);
        parametro.TetaoMensalCapacidadeBrl.Value.Moeda.Should().Be(Moeda.Brl);
        parametro.UpdatedAt.Should().Be(t2, "timestamp deve refletir o horário da atualização");
    }

    // ── Teste 3: AtualizarTetao com null → limpa o tetão ─────────────────────

    [Fact]
    public void AtualizarTetao_comNull_limpaTetao()
    {
        // Arrange
        ParametroSistema parametro = ParametroSistema.Criar(CriarClockFixo());
        parametro.AtualizarTetaoMensal(new Money(1_000_000m, Moeda.Brl), CriarClockFixo());
        parametro.TetaoMensalCapacidadeBrl.Should().NotBeNull("precondição: tetão estava configurado");

        // Act
        parametro.AtualizarTetaoMensal(null, CriarClockFixo());

        // Assert
        parametro.TetaoMensalCapacidadeBrl.Should().BeNull(
            "passar null deve limpar o tetão configurado");
    }

    // ── Teste 4: AtualizarTetao com moeda não-BRL → lança ArgumentException ──

    [Fact]
    public void AtualizarTetao_comMoedaNaoBrl_lancaArgumentException()
    {
        // Arrange
        ParametroSistema parametro = ParametroSistema.Criar(CriarClockFixo());
        Money emUsd = new(1_000_000m, Moeda.Usd);

        // Act & Assert
        parametro
            .Invoking(p => p.AtualizarTetaoMensal(emUsd, CriarClockFixo()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ── Teste 5: AtualizarTetao com valor negativo → lança ArgumentOutOfRangeException ──

    [Fact]
    public void AtualizarTetao_comValorNegativo_lancaArgumentOutOfRangeException()
    {
        // Arrange
        ParametroSistema parametro = ParametroSistema.Criar(CriarClockFixo());
        Money negativo = new(-1m, Moeda.Brl);

        // Act & Assert
        parametro
            .Invoking(p => p.AtualizarTetaoMensal(negativo, CriarClockFixo()))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
