using FluentAssertions;
using MediatR;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Common;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class AceitarPropostaCommandHandlerTests
{
    [Fact]
    public async Task Handle_PropostaExistente_AceitaEPersiste()
    {
        // Arrange — AceitarProposta exige status Comparada (EmCaptacao → Comparada → Aceita)
        Cotacao cotacao = TestHelpers.CriarCotacaoComparada();
        Proposta proposta = TestHelpers.AdicionarPropostaPadrao(cotacao);

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        currentUser.ActorSub.Returns("user|abc123");

        AceitarPropostaCommandHandler handler = new(repo, currentUser, clock);
        AceitarPropostaCommand cmd = new(cotacao.Id, proposta.Id);

        // Act
        Unit resultado = await handler.Handle(cmd, default);

        // Assert
        cotacao.PropostaAceitaId.Should().Be(proposta.Id);
        cotacao.Status.Should().Be(StatusCotacao.Aceita);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UtilizaActorSubDoCurrentUser()
    {
        // Arrange — verifica que o actorSub vem do ICurrentUserService
        Cotacao cotacao = TestHelpers.CriarCotacaoComparada();
        Proposta proposta = TestHelpers.AdicionarPropostaPadrao(cotacao);

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        IClock clock = TestHelpers.CriarClock();

        const string ActorSubEsperado = "auth0|financeiro-responsavel";
        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        currentUser.ActorSub.Returns(ActorSubEsperado);

        AceitarPropostaCommandHandler handler = new(repo, currentUser, clock);
        AceitarPropostaCommand cmd = new(cotacao.Id, proposta.Id);

        await handler.Handle(cmd, default);

        // ICurrentUserService.ActorSub foi acessado e propagado para a cotação
        _ = currentUser.Received(1).ActorSub;
        cotacao.AceitaPor.Should().Be(ActorSubEsperado);
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        IClock clock = TestHelpers.CriarClock();

        repo.GetByIdWithPropostasAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        AceitarPropostaCommandHandler handler = new(repo, currentUser, clock);
        AceitarPropostaCommand cmd = new(Guid.NewGuid(), Guid.NewGuid());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
