using FluentAssertions;
using MediatR;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class EnviarCotacaoCommandHandlerTests
{
    [Fact]
    public async Task Handle_CotacaoEmRascunho_TransicionaParaEmCaptacao()
    {
        // Arrange
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);

        EnviarCotacaoCommandHandler handler = new(repo, clock);
        EnviarCotacaoCommand cmd = new(cotacao.Id);

        // Act
        Unit resultado = await handler.Handle(cmd, default);

        // Assert
        cotacao.Status.Should().Be(StatusCotacao.EmCaptacao);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        // Arrange
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        EnviarCotacaoCommandHandler handler = new(repo, clock);
        EnviarCotacaoCommand cmd = new(Guid.NewGuid());

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_CotacaoJaEmCaptacao_LancaInvalidOperationException()
    {
        // Arrange — cotação já transitou para EmCaptacao
        Cotacao cotacao = TestHelpers.CriarCotacaoEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);

        EnviarCotacaoCommandHandler handler = new(repo, clock);
        EnviarCotacaoCommand cmd = new(cotacao.Id);

        // Act & Assert — FSM rejeita transição inválida
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
