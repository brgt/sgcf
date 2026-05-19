using FluentAssertions;
using NodaTime;
using NSubstitute;

using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Commands;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Commands;

[Trait("Category", "Unit")]
public sealed class DeletarCenarioCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_CenarioExistente_MarcaDeletedAt()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        DeletarCenarioCommandHandler handler = new(repo, _clock);
        await handler.Handle(new DeletarCenarioCommand(cenario.Id), default);

        // DeletedAt deve ter sido preenchido pelo domínio.
        cenario.DeletedAt.Should().NotBeNull();
        repo.Received(1).Update(cenario);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_CenarioNaoEncontrado_LancaKeyNotFoundException()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((CenarioSimulacao?)null);

        DeletarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new DeletarCenarioCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_CenarioArquivado_DeletedAtPreenchidoSemExcecao()
    {
        // Soft delete é permitido em qualquer status, incluindo Arquivado.
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioArquivado(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        DeletarCenarioCommandHandler handler = new(repo, _clock);

        Func<Task> act = () => handler.Handle(new DeletarCenarioCommand(cenario.Id), default);
        await act.Should().NotThrowAsync();
        cenario.DeletedAt.Should().NotBeNull();
    }
}
