using FluentAssertions;
using NodaTime;
using NSubstitute;

using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Commands;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Commands;

[Trait("Category", "Unit")]
public sealed class RemoverSimulacaoCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_SimulacaoExistente_RemoveERetornaCenarioAtualizadoEInvalidaCache()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        Sgcf.Application.Simulacao.Cache.ICronogramaSimulacaoCache cache =
            NSubstitute.Substitute.For<Sgcf.Application.Simulacao.Cache.ICronogramaSimulacaoCache>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        SimulacaoContratacao sim = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);
        cenario.AdicionarSimulacao(sim, _clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        RemoverSimulacaoCommandHandler handler = new(repo, _clock, cache);
        CenarioSimulacaoDto resultado = await handler.Handle(
            new RemoverSimulacaoCommand(cenario.Id, sim.Id), default);

        resultado.Simulacoes.Should().BeEmpty();
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
        // Fix 3: cache deve ser invalidado após remoção bem-sucedida
        await cache.Received(1).InvalidarPorSimulacaoAsync(
            cenario.Id,
            sim.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        RemoverSimulacaoCommandHandler handler = new(repo, _clock, NSubstitute.Substitute.For<Sgcf.Application.Simulacao.Cache.ICronogramaSimulacaoCache>());

        Func<Task> act = () => handler.Handle(
            new RemoverSimulacaoCommand(Guid.NewGuid(), Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_SimulacaoNaoExistenteNoCenario_LancaInvalidOperationException()
    {
        // Domínio lança InvalidOperationException quando simulacaoId não pertence ao cenário.
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        RemoverSimulacaoCommandHandler handler = new(repo, _clock, NSubstitute.Substitute.For<Sgcf.Application.Simulacao.Cache.ICronogramaSimulacaoCache>());

        Func<Task> act = () => handler.Handle(
            new RemoverSimulacaoCommand(cenario.Id, Guid.NewGuid()), default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }
}
