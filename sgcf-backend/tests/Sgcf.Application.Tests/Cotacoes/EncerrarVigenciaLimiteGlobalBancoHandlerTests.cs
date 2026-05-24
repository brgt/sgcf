using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class EncerrarVigenciaLimiteGlobalBancoHandlerTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 23, 10, 0);
    private static readonly Guid BancoId = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static LimiteGlobalBanco CriarLimiteGlobal(LocalDate inicio)
    {
        return LimiteGlobalBanco.Criar(
            BancoId,
            new Money(1_000_000m, Moeda.Brl),
            inicio,
            CriarClock());
    }

    private static EncerrarVigenciaLimiteGlobalBancoCommandHandler CriarHandler(
        ILimiteGlobalBancoRepository? repo = null,
        IClock? clock = null)
    {
        return new EncerrarVigenciaLimiteGlobalBancoCommandHandler(
            repo ?? Substitute.For<ILimiteGlobalBancoRepository>(),
            clock ?? CriarClock());
    }

    [Fact]
    public async Task Handle_ComDataFimValida_EncerraVigenciaESalva()
    {
        // Arrange
        LimiteGlobalBanco limite = CriarLimiteGlobal(new LocalDate(2026, 1, 1));
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);

        var handler = CriarHandler(repo, clock);
        var cmd = new EncerrarVigenciaLimiteGlobalBancoCommand(
            limite.Id,
            DataFim: new DateOnly(2026, 12, 31));

        // Act
        LimiteGlobalBancoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.DataVigenciaFim.Should().Be(new DateOnly(2026, 12, 31));
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_LimiteNaoEncontrado_LancaInvalidOperationException()
    {
        // EncerrarVigencia usa InvalidOperationException (não KeyNotFoundException) — ver handler
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.GetByIdTrackingAsync(Arg.Any<Guid>(), default).Returns((LimiteGlobalBanco?)null);

        var handler = CriarHandler(repo);
        var cmd = new EncerrarVigenciaLimiteGlobalBancoCommand(
            Guid.NewGuid(),
            DataFim: new DateOnly(2026, 12, 31));

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task Handle_DataFimAnteriorAoInicio_LancaArgumentException()
    {
        // Arrange — LG-08: DataFim < DataVigenciaInicio → domínio rejeita
        LimiteGlobalBanco limite = CriarLimiteGlobal(new LocalDate(2026, 6, 1));
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);

        var handler = CriarHandler(repo);
        var cmd = new EncerrarVigenciaLimiteGlobalBancoCommand(
            limite.Id,
            DataFim: new DateOnly(2026, 5, 1));

        // Act & Assert — domínio lança ArgumentException por LG-08
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*DataFim*anterior*DataVigenciaInicio*");
    }

    [Fact]
    public async Task Handle_VigenciaJaEncerrada_DominioPropagaInvalidOperationException()
    {
        // Arrange — LG-08: tentar encerrar vigência já encerrada
        LimiteGlobalBanco limite = CriarLimiteGlobal(new LocalDate(2026, 1, 1));
        limite.EncerrarVigencia(new LocalDate(2026, 6, 30), CriarClock());

        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);

        var handler = CriarHandler(repo);
        var cmd = new EncerrarVigenciaLimiteGlobalBancoCommand(
            limite.Id,
            DataFim: new DateOnly(2026, 12, 31));

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Vigência já encerrada*");
    }
}
