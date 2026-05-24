using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Queries;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class LimiteGlobalBancoQueryHandlerTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 23, 10, 0);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BancoId = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static ITenantContext CriarTenantContext()
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.TenantId.Returns(TenantId);
        return ctx;
    }

    private static LimiteGlobalBanco CriarLimiteGlobal(decimal valorBrl = 1_000_000m, Guid? bancoId = null)
    {
        return LimiteGlobalBanco.Criar(
            bancoId ?? BancoId,
            new Money(valorBrl, Moeda.Brl),
            new LocalDate(2026, 1, 1),
            CriarClock());
    }

    // ─── ListarLimitesGlobaisBancoQuery ──────────────────────────────────────

    [Fact]
    public async Task ListarLimitesGlobaisBanco_QuandoNenhumRegistro_RetornaListaVazia()
    {
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.ListAsync(null, null, default).Returns(Array.Empty<LimiteGlobalBanco>());

        var handler = new ListarLimitesGlobaisBancoQueryHandler(repo);
        var query = new ListarLimitesGlobaisBancoQuery();

        IReadOnlyList<LimiteGlobalBancoDto> resultado = await handler.Handle(query, default);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarLimitesGlobaisBanco_ComDoisRegistros_MapeiaParaDtos()
    {
        var limite1 = CriarLimiteGlobal(500_000m);
        var limite2 = CriarLimiteGlobal(800_000m);

        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.ListAsync(null, null, default)
            .Returns(new[] { limite1, limite2 });

        var handler = new ListarLimitesGlobaisBancoQueryHandler(repo);
        var query = new ListarLimitesGlobaisBancoQuery();

        IReadOnlyList<LimiteGlobalBancoDto> resultado = await handler.Handle(query, default);

        resultado.Should().HaveCount(2);
        resultado.Should().Contain(d => d.ValorLimiteBrl == 500_000m);
        resultado.Should().Contain(d => d.ValorLimiteBrl == 800_000m);
    }

    [Fact]
    public async Task ListarLimitesGlobaisBanco_ComFiltrosBancoIdEVigentesEm_PassaFiltrosParaRepo()
    {
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.ListAsync(BancoId, new LocalDate(2026, 6, 1), default)
            .Returns(Array.Empty<LimiteGlobalBanco>());

        var handler = new ListarLimitesGlobaisBancoQueryHandler(repo);
        var query = new ListarLimitesGlobaisBancoQuery(BancoId, VigentesEm: new DateOnly(2026, 6, 1));

        await handler.Handle(query, default);

        await repo.Received(1).ListAsync(BancoId, new LocalDate(2026, 6, 1), default);
    }

    // ─── GetLimiteGlobalBancoQuery ────────────────────────────────────────────

    [Fact]
    public async Task GetLimiteGlobalBanco_QuandoEncontrado_RetornaDto()
    {
        var limite = CriarLimiteGlobal(1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.GetByIdAsync(limite.Id, default).Returns(limite);

        var handler = new GetLimiteGlobalBancoQueryHandler(repo);
        var query = new GetLimiteGlobalBancoQuery(limite.Id);

        LimiteGlobalBancoDto resultado = await handler.Handle(query, default);

        resultado.Id.Should().Be(limite.Id);
        resultado.ValorLimiteBrl.Should().Be(1_000_000m);
        resultado.BancoId.Should().Be(BancoId);
    }

    [Fact]
    public async Task GetLimiteGlobalBanco_QuandoNaoEncontrado_LancaKeyNotFoundException()
    {
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((LimiteGlobalBanco?)null);

        var handler = new GetLimiteGlobalBancoQueryHandler(repo);
        var query = new GetLimiteGlobalBancoQuery(Guid.NewGuid());

        Func<Task> act = () => handler.Handle(query, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ─── GetLimiteGlobalVigenteBancoQuery ─────────────────────────────────────

    [Fact]
    public async Task GetLimiteGlobalVigenteBanco_RegimeA_CalculaSaldoDevedorESetaRegimeGlobalPuro()
    {
        // Arrange — regime A: sem limites por modalidade
        var limite = CriarLimiteGlobal(1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();

        repo.GetVigenteByBancoAsync(BancoId, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(false);
        saldo.CalcularSaldoDevedorBancoAsync(BancoId, TenantId, default)
            .Returns(new Money(300_000m, Moeda.Brl));

        var handler = new GetLimiteGlobalVigenteBancoQueryHandler(repo, saldo, tenantContext);
        var query = new GetLimiteGlobalVigenteBancoQuery(BancoId);

        // Act
        LimiteGlobalBancoVigenteDto resultado = await handler.Handle(query, default);

        // Assert
        resultado.Regime.Should().Be("GlobalPuro");
        resultado.ValorUtilizadoBrl.Should().Be(300_000m);
        resultado.ValorDisponivelBrl.Should().Be(700_000m);
        resultado.ValorLimiteBrl.Should().Be(1_000_000m);

        await saldo.Received(1).CalcularSaldoDevedorBancoAsync(BancoId, TenantId, default);
        await saldo.DidNotReceive()
            .CalcularUtilizadoAgregadoModalidadesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLimiteGlobalVigenteBanco_RegimeB_CalculaUtilizadoAgregadoESetaRegimePerModalidade()
    {
        // Arrange — regime B: banco possui limites por modalidade
        var limite = CriarLimiteGlobal(1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();

        repo.GetVigenteByBancoAsync(BancoId, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(true);
        saldo.CalcularUtilizadoAgregadoModalidadesAsync(BancoId, TenantId, default)
            .Returns(new Money(450_000m, Moeda.Brl));

        var handler = new GetLimiteGlobalVigenteBancoQueryHandler(repo, saldo, tenantContext);
        var query = new GetLimiteGlobalVigenteBancoQuery(BancoId);

        // Act
        LimiteGlobalBancoVigenteDto resultado = await handler.Handle(query, default);

        // Assert
        resultado.Regime.Should().Be("PerModalidade");
        resultado.ValorUtilizadoBrl.Should().Be(450_000m);
        resultado.ValorDisponivelBrl.Should().Be(550_000m);

        await saldo.Received(1).CalcularUtilizadoAgregadoModalidadesAsync(BancoId, TenantId, default);
        await saldo.DidNotReceive()
            .CalcularSaldoDevedorBancoAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLimiteGlobalVigenteBanco_UtilizadoMaiorQueLimite_DisponiveIsForcadoAZero()
    {
        // Arrange — utilizado > limite: ValorDisponivel deve ser 0, não negativo
        var limite = CriarLimiteGlobal(1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();

        repo.GetVigenteByBancoAsync(BancoId, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(false);
        saldo.CalcularSaldoDevedorBancoAsync(BancoId, TenantId, default)
            .Returns(new Money(1_200_000m, Moeda.Brl));

        var handler = new GetLimiteGlobalVigenteBancoQueryHandler(repo, saldo, tenantContext);
        var query = new GetLimiteGlobalVigenteBancoQuery(BancoId);

        LimiteGlobalBancoVigenteDto resultado = await handler.Handle(query, default);

        resultado.ValorDisponivelBrl.Should().Be(0m, "disponível não pode ser negativo");
    }

    [Fact]
    public async Task GetLimiteGlobalVigenteBanco_SemLimiteVigente_LancaKeyNotFoundException()
    {
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();

        repo.GetVigenteByBancoAsync(BancoId, default).Returns((LimiteGlobalBanco?)null);

        var handler = new GetLimiteGlobalVigenteBancoQueryHandler(repo, saldo, tenantContext);
        var query = new GetLimiteGlobalVigenteBancoQuery(BancoId);

        Func<Task> act = () => handler.Handle(query, default);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*vigente*");
    }
}
