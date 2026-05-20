using FluentAssertions;
using NodaTime;
using NSubstitute;

using Sgcf.Application.Common;
using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Commands;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Commands;

[Trait("Category", "Unit")]
public sealed class CriarCenarioSimulacaoCommandHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    [Fact]
    public async Task Handle_ComDadosValidos_CriaCenarioEmRascunho()
    {
        // Arrange
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.ActorSub.Returns("user-123");

        CriarCenarioSimulacaoCommandHandler handler = new(repo, _clock, currentUser);
        CriarCenarioSimulacaoCommand cmd = new("Realista 2026", 2026, "Cenário base para o ano");

        // Act
        CenarioSimulacaoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.Nome.Should().Be("Realista 2026");
        resultado.Status.Should().Be("Rascunho");
        resultado.AnoBase.Should().Be(2026);
        resultado.CriadoPor.Should().Be("user-123");
        resultado.Descricao.Should().Be("Cenário base para o ano");
        resultado.Simulacoes.Should().BeEmpty();
        repo.Received(1).Add(Arg.Any<CenarioSimulacao>());
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_SemUsuarioAutenticado_UsaUsuarioSistema()
    {
        // Arrange — sem ICurrentUserService injetado (null)
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CriarCenarioSimulacaoCommandHandler handler = new(repo, _clock, currentUser: null);
        CriarCenarioSimulacaoCommand cmd = new("Otimista 2027", 2027);

        // Act
        CenarioSimulacaoDto resultado = await handler.Handle(cmd, default);

        // Assert — fallback para AuditConstants.SystemActor ("SYSTEM") em contexto sem HTTP
        resultado.CriadoPor.Should().Be(AuditConstants.SystemActor);
    }

    [Fact]
    public async Task Handle_SemDescricao_CriaCenarioComDescricaoNula()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CriarCenarioSimulacaoCommandHandler handler = new(repo, _clock);
        CriarCenarioSimulacaoCommand cmd = new("Pessimista 2026", 2026);

        CenarioSimulacaoDto resultado = await handler.Handle(cmd, default);

        resultado.Descricao.Should().BeNull();
    }
}
