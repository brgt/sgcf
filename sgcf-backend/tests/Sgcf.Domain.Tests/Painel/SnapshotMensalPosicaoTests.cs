using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Painel;

using Xunit;

namespace Sgcf.Domain.Tests.Painel;

[Trait("Category", "Domain")]
public sealed class SnapshotMensalPosicaoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 31, 23, 59);

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        SnapshotMensalPosicao snapshot = SnapshotMensalPosicao.Criar(
            ano: 2026,
            mes: 5,
            totalContratosAtivos: 12,
            saldoPrincipalBrl: 5_000_000m,
            totalParcelasAbertasBrl: 1_200_000m,
            clock: clock);

        // Assert
        snapshot.Ano.Should().Be(2026);
        snapshot.Mes.Should().Be(5);
        snapshot.TotalContratosAtivos.Should().Be(12);
        snapshot.SaldoPrincipalBrlDecimal.Should().Be(5_000_000m);
        snapshot.TotalParcelasAbertasBrlDecimal.Should().Be(1_200_000m);
        snapshot.CriadoEm.Should().Be(AgoraFixo);
    }

    // ── Guard: mês fora de faixa ────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Criar_MesForaDeFaixa_LancaArgumentOutOfRangeException(int mesInvalido)
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => SnapshotMensalPosicao.Criar(2026, mesInvalido, 0, 0m, 0m, clock);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("mes");
    }

    // ── Guard: ano fora de faixa ────────────────────────────────────────────

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Criar_AnoForaDeFaixa_LancaArgumentOutOfRangeException(int anoInvalido)
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => SnapshotMensalPosicao.Criar(anoInvalido, 5, 0, 0m, 0m, clock);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ano");
    }

    // ── Guard: totalContratosAtivos negativo ────────────────────────────────

    [Fact]
    public void Criar_TotalContratosNegativo_LancaArgumentOutOfRangeException()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => SnapshotMensalPosicao.Criar(2026, 5, -1, 0m, 0m, clock);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("totalContratosAtivos");
    }

    // ── Arredondamento a 6 casas decimais ───────────────────────────────────

    [Fact]
    public void Criar_ValoresArredondados6CasasDecimais()
    {
        // Arrange — valores com mais de 6 casas devem ser arredondados HalfUp
        IClock clock = CriarClock(AgoraFixo);

        // Act
        SnapshotMensalPosicao snapshot = SnapshotMensalPosicao.Criar(
            ano: 2026,
            mes: 1,
            totalContratosAtivos: 1,
            saldoPrincipalBrl: 1.1234567m,       // rounds to 1.123457
            totalParcelasAbertasBrl: 2.9999999m,  // rounds to 3.0
            clock: clock);

        // Assert
        snapshot.SaldoPrincipalBrlDecimal.Should().Be(1.123457m);
        snapshot.TotalParcelasAbertasBrlDecimal.Should().Be(3m);
    }

    // ── Properties Money retornam Moeda.Brl ────────────────────────────────

    [Fact]
    public void SaldoPrincipalBrl_RetornaMoneyEmBrl()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);
        SnapshotMensalPosicao snapshot = SnapshotMensalPosicao.Criar(
            2026, 4, 5, 3_000_000m, 500_000m, clock);

        // Act
        Money saldo = snapshot.SaldoPrincipalBrl;

        // Assert
        saldo.Moeda.Should().Be(Moeda.Brl);
        saldo.Valor.Should().Be(3_000_000m);
    }

    [Fact]
    public void TotalParcelasAbertasBrl_RetornaMoneyEmBrl()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);
        SnapshotMensalPosicao snapshot = SnapshotMensalPosicao.Criar(
            2026, 4, 5, 3_000_000m, 500_000m, clock);

        // Act
        Money parcelas = snapshot.TotalParcelasAbertasBrl;

        // Assert
        parcelas.Moeda.Should().Be(Moeda.Brl);
        parcelas.Valor.Should().Be(500_000m);
    }
}
