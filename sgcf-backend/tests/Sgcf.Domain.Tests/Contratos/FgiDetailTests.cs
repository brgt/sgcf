using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class FgiDetailTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    [Fact]
    public void Criar_ComTaxaFgiEPercentual_ArmazenaComoFracao()
    {
        // Arrange — taxa 1.5% a.a. (humano) → deve ser armazenada como 0.015 (fração)
        Instant agora = Instant.FromUtc(2026, 4, 15, 9, 0);
        IClock clock = CriarClock(agora);
        Guid contratoId = Guid.NewGuid();

        // Act
        FgiDetail detail = FgiDetail.Criar(
            contratoId,
            numeroOperacaoFgi: "FGI-2026-0042",
            taxaFgiAaPct: 1.5m,
            percentualCobertoPct: 80m,
            clock);

        // Assert
        detail.ContratoId.Should().Be(contratoId);
        detail.NumeroOperacaoFgi.Should().Be("FGI-2026-0042");
        detail.TaxaFgiAa.Should().NotBeNull();
        detail.TaxaFgiAa!.Value.AsDecimal.Should().Be(0.015m);
        detail.TaxaFgiAa.Value.AsHumano.Should().Be(1.5m);
        detail.PercentualCoberto.Should().NotBeNull();
        detail.PercentualCoberto!.Value.AsDecimal.Should().Be(0.80m);
        detail.PercentualCoberto.Value.AsHumano.Should().Be(80m);
        detail.CreatedAt.Should().Be(agora);
    }

    [Fact]
    public void Criar_ComCamposNulos_TaxaFgiAaEPercentualCobertoPermanecemNulos()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 1, 0, 0));
        Guid contratoId = Guid.NewGuid();

        // Act
        FgiDetail detail = FgiDetail.Criar(
            contratoId,
            numeroOperacaoFgi: null,
            taxaFgiAaPct: null,
            percentualCobertoPct: null,
            clock);

        // Assert
        detail.TaxaFgiAa.Should().BeNull();
        detail.PercentualCoberto.Should().BeNull();
        detail.NumeroOperacaoFgi.Should().BeNull();
    }

    [Fact]
    public void Criar_TaxaZeroPorCento_ArmazenaZeroFracao()
    {
        // Arrange — edge case: taxa FGI de 0%
        IClock clock = CriarClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        Guid contratoId = Guid.NewGuid();

        // Act
        FgiDetail detail = FgiDetail.Criar(
            contratoId,
            numeroOperacaoFgi: null,
            taxaFgiAaPct: 0m,
            percentualCobertoPct: 0m,
            clock);

        // Assert
        detail.TaxaFgiAa.Should().NotBeNull();
        detail.TaxaFgiAa!.Value.AsDecimal.Should().Be(0m);
        detail.PercentualCoberto.Should().NotBeNull();
        detail.PercentualCoberto!.Value.AsDecimal.Should().Be(0m);
    }
}
