using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;

using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Commands;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Application.Sistema.Commands;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Sistema;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Audit;

/// <summary>
/// Garante que entidades do módulo Simulações emitem entradas em audit_log.
///
/// O audit é automático via <c>AuditInterceptor</c> (EF Core SaveChangesInterceptor):
/// qualquer entidade que implementa <see cref="IAuditable"/> e está em estado
/// Added/Modified/Deleted no ChangeTracker gera uma linha em audit_log.
///
/// Security finding HIGH: mutações em CenarioSimulacao, SimulacaoContratacao e
/// ParametroSistema não tinham rastro forense. Este conjunto de testes garante que
/// cada entidade relevante implementa IAuditable.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuditLoggingTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    // ── CenarioSimulacao — já implementa IAuditable ───────────────────────────

    [Fact]
    public void CenarioSimulacao_ImplementaIAuditable()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);

        // Assert — AuditInterceptor requer IAuditable para gerar linha em audit_log
        cenario.Should().BeAssignableTo<IAuditable>(
            "CenarioSimulacao é Admin-only e afeta todo o portfólio — toda mutação deve ser rastreável");
    }

    [Fact]
    public async Task CriarCenario_RepositoryAddChamado_ComEntidadeAuditavel()
    {
        // Arrange
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CriarCenarioSimulacaoCommandHandler handler = new(repo, _clock);
        CriarCenarioSimulacaoCommand cmd = new("Realista 2026", 2026);

        // Act — handler chama repo.Add(cenario) que passa pelo AuditInterceptor no SaveChanges
        CenarioSimulacaoDto _ = await handler.Handle(cmd, default);

        // Assert — repo.Add foi chamado com uma entidade auditável
        repo.Received(1).Add(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    [Fact]
    public async Task AtualizarCenario_RepositoryUpdateChamado_ComEntidadeAuditavel()
    {
        // Arrange
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtualizarCenarioCommandHandler handler = new(repo, _clock);
        AtualizarCenarioCommand cmd = new(cenario.Id, "Novo Nome", null, null);

        // Act
        await handler.Handle(cmd, default);

        // Assert — repo.Update foi chamado com uma entidade auditável
        repo.Received(1).Update(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    [Fact]
    public async Task AtivarCenario_RepositoryUpdateChamado_ComEntidadeAuditavel()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        AtivarCenarioCommandHandler handler = new(repo, _clock, NullLogger<AtivarCenarioCommandHandler>.Instance);
        await handler.Handle(new AtivarCenarioCommand(cenario.Id), default);

        repo.Received(1).Update(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    [Fact]
    public async Task ArquivarCenario_RepositoryUpdateChamado_ComEntidadeAuditavel()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioAtivo(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        ArquivarCenarioCommandHandler handler = new(repo, _clock, NullLogger<ArquivarCenarioCommandHandler>.Instance);
        await handler.Handle(new ArquivarCenarioCommand(cenario.Id), default);

        repo.Received(1).Update(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    [Fact]
    public async Task DeletarCenario_RepositoryUpdateChamado_ComEntidadeAuditavel()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        DeletarCenarioCommandHandler handler = new(repo, _clock, NullLogger<DeletarCenarioCommandHandler>.Instance);
        await handler.Handle(new DeletarCenarioCommand(cenario.Id), default);

        repo.Received(1).Update(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    [Fact]
    public async Task DuplicarCenario_RepositoryAddChamado_ComEntidadeAuditavel()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        DuplicarCenarioCommandHandler handler = new(repo, _clock);
        await handler.Handle(new DuplicarCenarioCommand(cenario.Id), default);

        // A cópia é adicionada — deve ser auditável
        repo.Received(1).Add(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    [Fact]
    public async Task RemoverSimulacao_RepositoryUpdateChamado_ComCenarioAuditavel()
    {
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        SimulacaoContratacao simulacao = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);
        cenario.AdicionarSimulacao(simulacao, _clock);
        repo.GetByIdAsync(cenario.Id, default).Returns(cenario);

        RemoverSimulacaoCommandHandler handler = new(repo, _clock, NSubstitute.Substitute.For<Sgcf.Application.Simulacao.Cache.ICronogramaSimulacaoCache>());
        await handler.Handle(new RemoverSimulacaoCommand(cenario.Id, simulacao.Id), default);

        repo.Received(1).Update(Arg.Is<CenarioSimulacao>(c => c is IAuditable));
    }

    // ── SimulacaoContratacao — RED: deve implementar IAuditable ──────────────

    /// <summary>
    /// RED test: SimulacaoContratacao não implementa IAuditable ainda.
    /// Fica verde após a correção no Domain (security fix).
    /// Security finding HIGH: adições/remoções/atualizações de simulações filhas
    /// afetam o portfólio de captação e precisam de rastro forense individual.
    /// </summary>
    [Fact]
    public void SimulacaoContratacao_ImplementaIAuditable()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock);
        SimulacaoContratacao simulacao = CenarioSimulacaoTestFactory.CriarSimulacao(cenario.Id, _clock);

        // Assert — falha até que SimulacaoContratacao implemente IAuditable
        simulacao.Should().BeAssignableTo<IAuditable>(
            "SimulacaoContratacao deve ser auditável individualmente: " +
            "cada captação hipotética é um dado financeiro sensível que precisa de rastro forense");
    }

    // ── ParametroSistema — RED: deve implementar IAuditable ──────────────────

    /// <summary>
    /// RED test: ParametroSistema não implementa IAuditable ainda.
    /// Fica verde após a correção no Domain (security fix).
    /// D-11: o tetão mensal afeta validações de toda a empresa — toda alteração
    /// deve ser rastreável (quem mudou, quando, de qual valor para qual).
    /// </summary>
    [Fact]
    public void ParametroSistema_ImplementaIAuditable()
    {
        // Arrange
        Guid tenantId = Guid.Parse("00000000-0000-7000-8000-000000000099");
        ParametroSistema parametros = ParametroSistema.CriarDefault(tenantId, _clock);

        // Assert — falha até que ParametroSistema implemente IAuditable
        parametros.Should().BeAssignableTo<IAuditable>(
            "ParametroSistema (tetão) é Admin-only e afeta toda a empresa — " +
            "toda alteração deve ter rastro forense para compliance (D-11)");
    }
}
