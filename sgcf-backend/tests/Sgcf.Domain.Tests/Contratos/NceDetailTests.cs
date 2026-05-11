using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class NceDetailTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    [Fact]
    public void Criar_ComTodosOsCampos_PreenchePropriedadesCorretamente()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 3, 10, 8, 0);
        IClock clock = CriarClock(agora);
        Guid contratoId = Guid.NewGuid();
        LocalDate dataEmissao = new(2026, 3, 10);

        // Act
        NceDetail detail = NceDetail.Criar(
            contratoId,
            nceNumero: "NCE-2026-0001",
            dataEmissao: dataEmissao,
            bancoMandatario: "Banco do Brasil S.A.",
            clock);

        // Assert
        detail.ContratoId.Should().Be(contratoId);
        detail.NceNumero.Should().Be("NCE-2026-0001");
        detail.DataEmissao.Should().Be(dataEmissao);
        detail.BancoMandatario.Should().Be("Banco do Brasil S.A.");
        detail.CreatedAt.Should().Be(agora);
        detail.UpdatedAt.Should().Be(agora);
        detail.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Criar_ComCamposOpcionaisNulos_PreencheContratoIdETimestamps()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 5, 1, 10, 0);
        IClock clock = CriarClock(agora);
        Guid contratoId = Guid.NewGuid();

        // Act
        NceDetail detail = NceDetail.Criar(
            contratoId,
            nceNumero: null,
            dataEmissao: null,
            bancoMandatario: null,
            clock);

        // Assert
        detail.ContratoId.Should().Be(contratoId);
        detail.NceNumero.Should().BeNull();
        detail.DataEmissao.Should().BeNull();
        detail.BancoMandatario.Should().BeNull();
        detail.CreatedAt.Should().Be(agora);
    }

    [Fact]
    public void Criar_DoisContratos_GeramIdsDistintos()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 1, 1, 0, 0));

        // Act
        NceDetail detail1 = NceDetail.Criar(Guid.NewGuid(), null, null, null, clock);
        NceDetail detail2 = NceDetail.Criar(Guid.NewGuid(), null, null, null, clock);

        // Assert
        detail1.Id.Should().NotBe(detail2.Id);
    }
}
