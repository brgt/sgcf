using FluentAssertions;
using NodaTime;
using NSubstitute;

using Sgcf.Application.Common;
using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Commands;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Commands;

[Trait("Category", "Unit")]
public sealed class DuplicarCenarioCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_CenarioComSimulacoes_CopiaEmRascunhoComNovoId()
    {
        // Arrange
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao origem = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Realista 2026");
        SimulacaoContratacao sim = CenarioSimulacaoTestFactory.CriarSimulacao(origem.Id, _clock);
        origem.AdicionarSimulacao(sim, _clock);
        repo.GetByIdAsync(origem.Id, default).Returns(origem);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.ActorSub.Returns("duplicador-99");

        DuplicarCenarioCommandHandler handler = new(repo, _clock, currentUser);

        // Act
        CenarioSimulacaoDto resultado = await handler.Handle(new DuplicarCenarioCommand(origem.Id), default);

        // Assert
        resultado.Id.Should().NotBe(origem.Id);
        resultado.Nome.Should().Be("Realista 2026 (cópia)");
        resultado.Status.Should().Be("Rascunho");
        resultado.CriadoPor.Should().Be("duplicador-99");
        resultado.Simulacoes.Should().HaveCount(1);
        resultado.Simulacoes[0].Id.Should().NotBe(sim.Id);
        resultado.Simulacoes[0].Version.Should().Be(1);
        repo.Received(1).Add(Arg.Any<CenarioSimulacao>());
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CenarioOrigemNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        DuplicarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new DuplicarCenarioCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Cenário origem*não encontrado*");
    }

    [Fact]
    public async Task Handle_CenarioArquivado_DuplicaComSucesso()
    {
        // Cenários Arquivados também podem ser duplicados (SPEC D-10).
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao arquivado = CenarioSimulacaoTestFactory.CriarCenarioArquivado(_clock);
        repo.GetByIdAsync(arquivado.Id, default).Returns(arquivado);

        DuplicarCenarioCommandHandler handler = new(repo, _clock);
        CenarioSimulacaoDto resultado = await handler.Handle(new DuplicarCenarioCommand(arquivado.Id), default);

        resultado.Status.Should().Be("Rascunho");
        resultado.Nome.Should().EndWith("(cópia)");
    }
}
