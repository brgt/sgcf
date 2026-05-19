using FluentAssertions;
using NodaTime;
using NSubstitute;

using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Queries;

[Trait("Category", "Unit")]
public sealed class GetCenarioSimulacaoByIdQueryHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_CenarioExistente_RetornaDto()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Realista 2026", 2026);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        GetCenarioSimulacaoByIdQueryHandler handler = new(repo);
        CenarioSimulacaoDto resultado = await handler.Handle(
            new GetCenarioSimulacaoByIdQuery(cenario.Id), default);

        resultado.Id.Should().Be(cenario.Id);
        resultado.Nome.Should().Be("Realista 2026");
        resultado.Status.Should().Be("Rascunho");
        resultado.Simulacoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CenarioComSimulacoes_RetornaDtoComFilhas()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        SimulacaoContratacao sim = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);
        cenario.AdicionarSimulacao(sim, _clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        GetCenarioSimulacaoByIdQueryHandler handler = new(repo);
        CenarioSimulacaoDto resultado = await handler.Handle(
            new GetCenarioSimulacaoByIdQuery(cenario.Id), default);

        resultado.Simulacoes.Should().HaveCount(1);
        resultado.Simulacoes[0].Id.Should().Be(sim.Id);
    }

    [Fact]
    public async Task Handle_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        GetCenarioSimulacaoByIdQueryHandler handler = new(repo);

        Func<Task> act = () => handler.Handle(
            new GetCenarioSimulacaoByIdQuery(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*não encontrado*");
    }
}
