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
public sealed class AtualizarCenarioCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_CenarioRascunho_AtualizaNomeEDescricao()
    {
        // Arrange
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtualizarCenarioCommandHandler handler = new(repo, _clock);
        AtualizarCenarioCommand cmd = new(cenario.Id, "Nome Atualizado", "Nova descrição", null);

        // Act
        CenarioSimulacaoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.Nome.Should().Be("Nome Atualizado");
        resultado.Descricao.Should().Be("Nova descrição");
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        // Arrange
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        AtualizarCenarioCommandHandler handler = new(repo, _clock);
        AtualizarCenarioCommand cmd = new(Guid.NewGuid(), "Nome", null, null);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task Handle_CenarioArquivado_LancaInvalidOperationException()
    {
        // Arrange — domínio bloqueia mutação em Arquivado
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioArquivado(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtualizarCenarioCommandHandler handler = new(repo, _clock);
        AtualizarCenarioCommand cmd = new(cenario.Id, "Novo Nome", null, null);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Arquivado*");
    }
}
