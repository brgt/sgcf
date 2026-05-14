using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contabilidade;

using Xunit;

namespace Sgcf.Domain.Tests.Contabilidade;

[Trait("Category", "Domain")]
public sealed class LancamentoContabilTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 12, 8, 0);
    private static readonly LocalDate DataFixa = new(2026, 5, 12);

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Guid contratoId = Guid.NewGuid();
        Money valor = new(1_500.50m, Moeda.Brl);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        LancamentoContabil lancamento = LancamentoContabil.Criar(
            contratoId, DataFixa, "PROVISAO_JUROS", valor, "Provisão de juros diária", clock);

        // Assert
        lancamento.ContratoId.Should().Be(contratoId);
        lancamento.Data.Should().Be(DataFixa);
        lancamento.Origem.Should().Be("PROVISAO_JUROS");
        lancamento.Valor.Valor.Should().Be(1_500.50m);
        lancamento.Valor.Moeda.Should().Be(Moeda.Brl);
        lancamento.Descricao.Should().Be("Provisão de juros diária");
        lancamento.CriadoEm.Should().Be(AgoraFixo);
    }

    // ── Guard: contratoId vazio ─────────────────────────────────────────────

    [Fact]
    public void Criar_ContratoIdVazio_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => LancamentoContabil.Criar(
            Guid.Empty, DataFixa, "PROVISAO_JUROS",
            new Money(100m, Moeda.Brl), "Descrição válida", clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contratoId");
    }

    // ── Guard: origem vazia / whitespace ────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_OrigemVazia_LancaArgumentException(string origemInvalida)
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => LancamentoContabil.Criar(
            Guid.NewGuid(), DataFixa, origemInvalida,
            new Money(100m, Moeda.Brl), "Descrição válida", clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("origem");
    }

    // ── Guard: descrição vazia / whitespace ─────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_DescricaoVazia_LancaArgumentException(string descricaoInvalida)
    {
        // Arrange
        IClock clock = CriarClock(AgoraFixo);

        // Act
        Action act = () => LancamentoContabil.Criar(
            Guid.NewGuid(), DataFixa, "PROVISAO_JUROS",
            new Money(100m, Moeda.Brl), descricaoInvalida, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("descricao");
    }

    // ── Valor preserva moeda do contrato ────────────────────────────────────

    [Fact]
    public void Criar_ValorPreservaMoedaContrato()
    {
        // Arrange — contrato em USD deve manter USD no lançamento
        Money valor = new(12_000m, Moeda.Usd);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        LancamentoContabil lancamento = LancamentoContabil.Criar(
            Guid.NewGuid(), DataFixa, "PROVISAO_JUROS",
            valor, "Provisão USD", clock);

        // Assert
        lancamento.Valor.Moeda.Should().Be(Moeda.Usd);
        lancamento.MoedaContrato.Should().Be(Moeda.Usd);
    }

    // ── Arredondamento a 6 casas decimais ───────────────────────────────────

    [Fact]
    public void Criar_ValorArredondado6CasasDecimais()
    {
        // Arrange — 100.1234567 rounds to 100.123457 (HalfUp)
        Money valor = new(100.1234567m, Moeda.Brl);
        IClock clock = CriarClock(AgoraFixo);

        // Act
        LancamentoContabil lancamento = LancamentoContabil.Criar(
            Guid.NewGuid(), DataFixa, "PROVISAO_JUROS",
            valor, "Descrição", clock);

        // Assert
        lancamento.ValorDecimal.Should().Be(100.123457m);
    }
}
