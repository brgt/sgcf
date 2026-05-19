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
public sealed class AtivarArquivarCenarioCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    // ── AtivarCenario ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AtivarHandler_CenarioRascunho_TransicionaParaAtivo()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtivarCenarioCommandHandler handler = new(repo, _clock);
        CenarioSimulacaoDto resultado = await handler.Handle(new AtivarCenarioCommand(cenario.Id), default);

        resultado.Status.Should().Be("Ativo");
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task AtivarHandler_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        AtivarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new AtivarCenarioCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AtivarHandler_CenarioJaAtivo_LancaInvalidOperationException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioAtivo(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtivarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new AtivarCenarioCommand(cenario.Id), default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já está Ativo*");
    }

    // ── ArquivarCenario ───────────────────────────────────────────────────────

    [Fact]
    public async Task ArquivarHandler_CenarioAtivo_TransicionaParaArquivado()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioAtivo(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        ArquivarCenarioCommandHandler handler = new(repo, _clock);
        CenarioSimulacaoDto resultado = await handler.Handle(new ArquivarCenarioCommand(cenario.Id), default);

        resultado.Status.Should().Be("Arquivado");
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task ArquivarHandler_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        ArquivarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new ArquivarCenarioCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ArquivarHandler_CenarioRascunho_LancaInvalidOperationException()
    {
        // Rascunho → Arquivado é transição inválida (Arquivar exige Ativo).
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        ArquivarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new ArquivarCenarioCommand(cenario.Id), default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Somente cenários Ativos*");
    }
}
