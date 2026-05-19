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
public sealed class AtualizarSimulacaoCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    private static AtualizarSimulacaoInput CriarInputValido() => new(
        Modalidade: "Nce",
        Moeda: "Brl",
        ValorPrincipal: 800_000m,
        DataContratacaoPrevista: new DateOnly(2026, 7, 1),
        DataPrimeiroVencimento: new DateOnly(2026, 8, 1),
        TipoTaxa: "CdiSpread",
        TaxaAa: null,
        SpreadAa: 3m,
        BaseCalculo: "Dias252",
        EstruturaAmortizacao: "Bullet",
        Periodicidade: "Mensal",
        QuantidadeParcelas: 24,
        AnchorDiaMes: "DiaContratacao");

    [Fact]
    public async Task Handle_SimulacaoExistente_AtualizaCamposEIncrementaVersion()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        SimulacaoContratacao sim = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);
        cenario.AdicionarSimulacao(sim, _clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtualizarSimulacaoCommandHandler handler = new(repo, _clock);
        AtualizarSimulacaoCommand cmd = new(cenario.Id, sim.Id, CriarInputValido());

        CenarioSimulacaoDto resultado = await handler.Handle(cmd, default);

        SimulacaoContratacaoDto simAtualizada = resultado.Simulacoes[0];
        simAtualizada.ValorPrincipal.Should().Be(800_000m);
        simAtualizada.QuantidadeParcelas.Should().Be(24);
        // Version deve ter sido incrementado de 1 para 2 (AD-3).
        simAtualizada.Version.Should().Be(2);
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        AtualizarSimulacaoCommandHandler handler = new(repo, _clock);
        AtualizarSimulacaoCommand cmd = new(Guid.NewGuid(), Guid.NewGuid(), CriarInputValido());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_CenarioArquivado_LancaInvalidOperationException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioArquivado(_clock);
        SimulacaoContratacao sim = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);
        // Simulação adicionada antes de Arquivar (para que o Id exista no cenário).
        // Recriamos o agregado com simulação antes do arquivamento.
        IClock clockInterno = CenarioSimulacaoTestFactory.CriarClock();
        CenarioSimulacao cenarioComSim = CenarioSimulacaoTestFactory.CriarCenarioRascunho(clockInterno);
        SimulacaoContratacao simInterna = CenarioSimulacaoTestFactory.CriarSimulacao(cenarioComSim.Id, clockInterno);
        cenarioComSim.AdicionarSimulacao(simInterna, clockInterno);
        cenarioComSim.Ativar(clockInterno);
        cenarioComSim.Arquivar(clockInterno);
        repo.GetByIdAsync(cenarioComSim.Id, default).Returns(cenarioComSim);

        AtualizarSimulacaoCommandHandler handler = new(repo, _clock);
        AtualizarSimulacaoCommand cmd = new(cenarioComSim.Id, simInterna.Id, CriarInputValido());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Arquivado*");
    }
}
