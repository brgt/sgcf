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
public sealed class ListCenariosSimulacaoQueryHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_SemFiltros_RetornaTodosCenarios()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        List<CenarioSimulacao> cenarios =
        [
            CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "A", 2026),
            CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "B", 2027)
        ];
        repo.ListAsync(null, null, null, default).Returns(cenarios.AsReadOnly());

        ListCenariosSimulacaoQueryHandler handler = new(repo);
        IReadOnlyList<CenarioSimulacaoResumoDto> resultado = await handler.Handle(
            new ListCenariosSimulacaoQuery(null, null, null), default);

        resultado.Should().HaveCount(2);
        resultado[0].Nome.Should().Be("A");
        resultado[1].Nome.Should().Be("B");
    }

    [Fact]
    public async Task Handle_ComFiltroStatus_PassaFiltroAoRepositorio()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.ListAsync(StatusCenarioSimulacao.Ativo, null, null, default)
            .Returns(Array.Empty<CenarioSimulacao>());

        ListCenariosSimulacaoQueryHandler handler = new(repo);
        await handler.Handle(
            new ListCenariosSimulacaoQuery(StatusCenarioSimulacao.Ativo, null, null), default);

        await repo.Received(1).ListAsync(StatusCenarioSimulacao.Ativo, null, null, default);
    }

    [Fact]
    public async Task Handle_ListaVazia_RetornaListaVazia()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.ListAsync(null, null, null, default).Returns(Array.Empty<CenarioSimulacao>());

        ListCenariosSimulacaoQueryHandler handler = new(repo);
        IReadOnlyList<CenarioSimulacaoResumoDto> resultado = await handler.Handle(
            new ListCenariosSimulacaoQuery(null, null, null), default);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CenarioComSimulacoes_RetornaQtdeCorreta()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        SimulacaoContratacao sim = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);
        cenario.AdicionarSimulacao(sim, _clock);
        repo.ListAsync(null, null, null, default).Returns([cenario]);

        ListCenariosSimulacaoQueryHandler handler = new(repo);
        IReadOnlyList<CenarioSimulacaoResumoDto> resultado = await handler.Handle(
            new ListCenariosSimulacaoQuery(null, null, null), default);

        resultado[0].QtdeSimulacoes.Should().Be(1);
    }
}
