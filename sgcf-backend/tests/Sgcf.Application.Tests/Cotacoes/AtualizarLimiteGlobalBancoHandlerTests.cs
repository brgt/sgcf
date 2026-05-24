using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class AtualizarLimiteGlobalBancoHandlerTests
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

    private static LimiteGlobalBanco CriarLimiteGlobal(decimal valorBrl = 1_000_000m)
    {
        return LimiteGlobalBanco.Criar(
            BancoId,
            new Money(valorBrl, Moeda.Brl),
            new LocalDate(2026, 1, 1),
            CriarClock());
    }

    private static AtualizarLimiteGlobalBancoCommandHandler CriarHandler(
        ILimiteGlobalBancoRepository? repo = null,
        IConsultaSaldoBanco? saldo = null,
        ITenantContext? tenantContext = null,
        IClock? clock = null)
    {
        return new AtualizarLimiteGlobalBancoCommandHandler(
            repo ?? Substitute.For<ILimiteGlobalBancoRepository>(),
            saldo ?? Substitute.For<IConsultaSaldoBanco>(),
            tenantContext ?? CriarTenantContext(),
            clock ?? CriarClock());
    }

    [Fact]
    public async Task Handle_RegimeA_AtualizaValorRetornaDto()
    {
        // Arrange
        LimiteGlobalBanco limite = CriarLimiteGlobal(valorBrl: 1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();
        var clock = CriarClock();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(false);
        saldo.CalcularSaldoDevedorBancoAsync(BancoId, TenantId, default)
            .Returns(new Money(300_000m, Moeda.Brl));

        var handler = CriarHandler(repo, saldo, tenantContext, clock);
        var cmd = new AtualizarLimiteGlobalBancoCommand(
            limite.Id,
            ValorLimiteBrl: 1_200_000m,
            DataVigenciaInicio: null,
            DataVigenciaFim: null,
            Observacoes: null);

        // Act
        LimiteGlobalBancoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.ValorLimiteBrl.Should().Be(1_200_000m);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_RegimeB_ReducaoAcimaSomaModalidades_AtualizaComSucesso()
    {
        // Arrange — regime B, novo valor ainda ≥ soma dos limites por modalidade
        LimiteGlobalBanco limite = CriarLimiteGlobal(valorBrl: 1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();
        var clock = CriarClock();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(true);
        saldo.CalcularUtilizadoAgregadoModalidadesAsync(BancoId, TenantId, default)
            .Returns(new Money(400_000m, Moeda.Brl));
        saldo.CalcularSomaLimitesModalidadesAsync(BancoId, TenantId, null, default)
            .Returns(new Money(600_000m, Moeda.Brl));

        var handler = CriarHandler(repo, saldo, tenantContext, clock);
        var cmd = new AtualizarLimiteGlobalBancoCommand(
            limite.Id,
            ValorLimiteBrl: 700_000m,
            DataVigenciaInicio: null,
            DataVigenciaFim: null,
            Observacoes: null);

        // Act — 700k ≥ 600k (soma modalidades) → deve passar
        LimiteGlobalBancoDto resultado = await handler.Handle(cmd, default);

        resultado.ValorLimiteBrl.Should().Be(700_000m);
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_RegimeA_NovoValorMenorQueSaldoDevedor_LancaInvalidOperationException()
    {
        // Arrange — LG-06: novo limite < saldo devedor
        LimiteGlobalBanco limite = CriarLimiteGlobal(valorBrl: 1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();
        var clock = CriarClock();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(false);
        saldo.CalcularSaldoDevedorBancoAsync(BancoId, TenantId, default)
            .Returns(new Money(800_000m, Moeda.Brl));

        var handler = CriarHandler(repo, saldo, tenantContext, clock);
        var cmd = new AtualizarLimiteGlobalBancoCommand(
            limite.Id,
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: null,
            DataVigenciaFim: null,
            Observacoes: null);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*menor que o saldo devedor*");
    }

    [Fact]
    public async Task Handle_RegimeB_SomaModalidadesExcedeNovoGlobal_LancaInvalidOperationException()
    {
        // Arrange — LG-09: soma de limites por modalidade > novo valor global
        LimiteGlobalBanco limite = CriarLimiteGlobal(valorBrl: 1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();
        var clock = CriarClock();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(true);
        saldo.CalcularUtilizadoAgregadoModalidadesAsync(BancoId, TenantId, default)
            .Returns(new Money(400_000m, Moeda.Brl));
        saldo.CalcularSomaLimitesModalidadesAsync(BancoId, TenantId, null, default)
            .Returns(new Money(800_000m, Moeda.Brl));

        var handler = CriarHandler(repo, saldo, tenantContext, clock);
        var cmd = new AtualizarLimiteGlobalBancoCommand(
            limite.Id,
            ValorLimiteBrl: 600_000m,
            DataVigenciaInicio: null,
            DataVigenciaFim: null,
            Observacoes: null);

        // Act & Assert — 600k < 800k (soma modalidades)
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Soma dos limites por modalidade*");
    }

    [Fact]
    public async Task Handle_LimiteNaoEncontrado_LancaKeyNotFoundException()
    {
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        repo.GetByIdTrackingAsync(Arg.Any<Guid>(), default).Returns((LimiteGlobalBanco?)null);

        var handler = CriarHandler(repo, saldo);
        var cmd = new AtualizarLimiteGlobalBancoCommand(
            Guid.NewGuid(),
            ValorLimiteBrl: 1_000_000m,
            DataVigenciaInicio: null,
            DataVigenciaFim: null,
            Observacoes: null);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_SemAlteracaoDeValor_NaoConsultaSaldo()
    {
        // Arrange — cmd sem ValorLimiteBrl → saldo não deve ser consultado
        LimiteGlobalBanco limite = CriarLimiteGlobal(valorBrl: 1_000_000m);
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var clock = CriarClock();

        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);

        var handler = CriarHandler(repo, saldo, clock: clock);
        var cmd = new AtualizarLimiteGlobalBancoCommand(
            limite.Id,
            ValorLimiteBrl: null,
            DataVigenciaInicio: null,
            DataVigenciaFim: null,
            Observacoes: "Observação atualizada");

        // Act
        LimiteGlobalBancoDto resultado = await handler.Handle(cmd, default);

        // Assert — saldo devedor não deve ter sido consultado
        resultado.Observacoes.Should().Be("Observação atualizada");
        await saldo.DidNotReceive()
            .BancoEmRegimePerModalityAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
