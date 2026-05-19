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
public sealed class AdicionarSimulacaoCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    private static AdicionarSimulacaoInput CriarInputValido() => new(
        BancoId: Guid.NewGuid(),
        Modalidade: "Nce",
        Moeda: "Brl",
        ValorPrincipal: 500_000m,
        DataContratacaoPrevista: new DateOnly(2026, 7, 1),
        DataPrimeiroVencimento: new DateOnly(2026, 8, 1),
        TipoTaxa: "CdiSpread",
        TaxaAa: null,
        SpreadAa: 2m,
        BaseCalculo: "Dias252",
        EstruturaAmortizacao: "Bullet",
        Periodicidade: "Mensal",
        QuantidadeParcelas: 12,
        AnchorDiaMes: "DiaContratacao");

    [Fact]
    public async Task Handle_CenarioRascunho_AdicionaSimulacaoERetornaCenarioAtualizado()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AdicionarSimulacaoCommandHandler handler = new(repo, _clock);
        AdicionarSimulacaoCommand cmd = new(cenario.Id, CriarInputValido());

        CenarioSimulacaoDto resultado = await handler.Handle(cmd, default);

        resultado.Simulacoes.Should().HaveCount(1);
        resultado.Simulacoes[0].Version.Should().Be(1);
        resultado.Simulacoes[0].Moeda.Should().Be("Brl");
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        AdicionarSimulacaoCommandHandler handler = new(repo, _clock);
        AdicionarSimulacaoCommand cmd = new(Guid.NewGuid(), CriarInputValido());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_CenarioArquivado_LancaInvalidOperationException()
    {
        // Domínio bloqueia AdicionarSimulacao em Arquivado.
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioArquivado(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AdicionarSimulacaoCommandHandler handler = new(repo, _clock);
        AdicionarSimulacaoCommand cmd = new(cenario.Id, CriarInputValido());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Arquivado*");
    }
}
