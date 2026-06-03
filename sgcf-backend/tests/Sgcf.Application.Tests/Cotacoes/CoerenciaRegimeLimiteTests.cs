using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Bancos.Commands;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Coerência de regime de limite (SPEC_REGIME_LIMITE_EXPLICITO §4.2):
/// REG-01 — banco em regime GlobalPuro não admite LimiteBanco por modalidade
/// (criação e atualização bloqueadas).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CoerenciaRegimeLimiteTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 23, 10, 0);
    private static readonly Guid BancoId = Guid.NewGuid();
    private static readonly LocalDate DataInicio = new(2026, 1, 1);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static Banco CriarBanco(RegimeLimiteBanco regime)
    {
        Banco banco = Banco.Criar("341", "Itaú Unibanco S.A.", "Itaú", CriarClock());
        if (regime != RegimeLimiteBanco.PerModalidade)
        {
            banco.DefinirRegimeLimite(regime, CriarClock());
        }

        return banco;
    }

    private static IBancoRepository CriarBancoRepo(Banco banco)
    {
        var bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(banco.Id, Arg.Any<CancellationToken>()).Returns(banco);
        return bancoRepo;
    }

    // ─── REG-01: criar LimiteBanco em banco GlobalPuro é bloqueado ──────────────

    [Fact]
    public async Task CriarLimiteBanco_EmBancoGlobalPuro_Bloqueia()
    {
        // Arrange
        Banco banco = CriarBanco(RegimeLimiteBanco.GlobalPuro);
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();

        var handler = new CreateLimiteBancoCommandHandler(
            repo, limiteGlobalRepo, CriarBancoRepo(banco), CriarClock());

        var cmd = new CreateLimiteBancoCommand(
            banco.Id,
            Modalidade: ModalidadeContrato.Finimp.ToString(),
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1));

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[REG-01]*");

        repo.DidNotReceive().Add(Arg.Any<LimiteBanco>());
    }

    [Fact]
    public async Task CriarLimiteBanco_EmBancoPerModalidade_Permite()
    {
        // Arrange — regime per-modalidade: sem bloqueio de coerência.
        Banco banco = CriarBanco(RegimeLimiteBanco.PerModalidade);
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();

        LimiteBanco? semConflito = null;
        repo.FindOverlappingAsync(banco.Id, Arg.Any<ModalidadeContrato>(), Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, Arg.Any<CancellationToken>())
            .Returns(semConflito);
        LimiteGlobalBanco? semGlobal = null;
        limiteGlobalRepo.GetVigenteByBancoAsync(banco.Id, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(semGlobal);

        var handler = new CreateLimiteBancoCommandHandler(
            repo, limiteGlobalRepo, CriarBancoRepo(banco), CriarClock());

        var cmd = new CreateLimiteBancoCommand(
            banco.Id,
            Modalidade: ModalidadeContrato.Finimp.ToString(),
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1));

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        repo.Received(1).Add(Arg.Any<LimiteBanco>());
    }

    // ─── REG-01: atualizar LimiteBanco de banco GlobalPuro é bloqueado ──────────

    [Fact]
    public async Task AtualizarLimiteBanco_EmBancoGlobalPuro_Bloqueia()
    {
        // Arrange
        Banco banco = CriarBanco(RegimeLimiteBanco.GlobalPuro);
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();

        LimiteBanco existente = LimiteBanco.Criar(
            bancoId: banco.Id,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(300_000m, Moeda.Brl),
            dataVigenciaInicio: DataInicio,
            clock: CriarClock());
        repo.GetByIdTrackingAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var handler = new UpdateLimiteBancoCommandHandler(
            repo, limiteGlobalRepo, CriarBancoRepo(banco), CriarClock());

        var cmd = new UpdateLimiteBancoCommand(
            LimiteId: existente.Id,
            NovoValorLimiteBrl: 400_000m);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[REG-01]*");

        repo.DidNotReceive().Update(Arg.Any<LimiteBanco>());
    }

    // ─── REG-02 / REG-04: troca de regime via DefinirRegimeLimiteBancoCommand ───

    private static readonly Guid TenantId = Guid.NewGuid();

    private static ITenantContext CriarTenantContext()
    {
        var tc = Substitute.For<ITenantContext>();
        tc.TenantId.Returns(TenantId);
        return tc;
    }

    private static DefinirRegimeLimiteBancoCommandHandler CriarHandlerDefinirRegime(
        Banco banco, IConsultaSaldoBanco saldo, IBancoRepository? bancoRepo = null)
    {
        return new DefinirRegimeLimiteBancoCommandHandler(
            bancoRepo ?? CriarBancoRepo(banco),
            saldo,
            CriarTenantContext(),
            CriarClock());
    }

    [Fact]
    public async Task MudarParaGlobalPuro_ComLimiteBancoAtivo_Bloqueia()
    {
        // Arrange — banco per-modalidade com soma de limites por modalidade > 0.
        Banco banco = CriarBanco(RegimeLimiteBanco.PerModalidade);
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        saldo.CalcularSomaLimitesModalidadesAsync(banco.Id, TenantId, null, Arg.Any<CancellationToken>())
            .Returns(new Money(300_000m, Moeda.Brl));

        var bancoRepo = CriarBancoRepo(banco);
        var handler = CriarHandlerDefinirRegime(banco, saldo, bancoRepo);
        var cmd = new DefinirRegimeLimiteBancoCommand(banco.Id, RegimeLimiteBanco.GlobalPuro.ToString());

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[REG-02]*");

        banco.RegimeLimite.Should().Be(RegimeLimiteBanco.PerModalidade, "a troca foi bloqueada");
        await bancoRepo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MudarParaGlobalPuro_SemLimiteBanco_Permite()
    {
        // Arrange — soma de limites por modalidade == 0.
        Banco banco = CriarBanco(RegimeLimiteBanco.PerModalidade);
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        saldo.CalcularSomaLimitesModalidadesAsync(banco.Id, TenantId, null, Arg.Any<CancellationToken>())
            .Returns(new Money(0m, Moeda.Brl));

        var handler = CriarHandlerDefinirRegime(banco, saldo);
        var cmd = new DefinirRegimeLimiteBancoCommand(banco.Id, RegimeLimiteBanco.GlobalPuro.ToString());

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        banco.RegimeLimite.Should().Be(RegimeLimiteBanco.GlobalPuro);
    }

    [Fact]
    public async Task MudarParaPerModalidade_ComLimiteBancoAtivo_Permite()
    {
        // Arrange — REG-04: voltar para per-modalidade é sempre permitido, sem checar saldo.
        Banco banco = CriarBanco(RegimeLimiteBanco.GlobalPuro);
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        var handler = CriarHandlerDefinirRegime(banco, saldo);
        var cmd = new DefinirRegimeLimiteBancoCommand(banco.Id, RegimeLimiteBanco.PerModalidade.ToString());

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        banco.RegimeLimite.Should().Be(RegimeLimiteBanco.PerModalidade);

        // REG-04 não consulta a soma de limites por modalidade.
        await saldo.DidNotReceive().CalcularSomaLimitesModalidadesAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // ─── Criação de banco com regime explícito ──────────────────────────────────

    [Fact]
    public async Task CriarBanco_ComRegimeGlobalPuro_DefineRegime()
    {
        // Arrange
        var repo = Substitute.For<IBancoRepository>();
        var handler = new CreateBancoCommandHandler(repo, CriarClock());
        var cmd = new CreateBancoCommand("341", "Itaú Unibanco S.A.", "Itaú")
        {
            RegimeLimite = RegimeLimiteBanco.GlobalPuro.ToString(),
        };

        // Act
        BancoDto dto = await handler.Handle(cmd, default);

        // Assert
        dto.RegimeLimite.Should().Be(RegimeLimiteBanco.GlobalPuro.ToString());
        repo.Received(1).Add(Arg.Is<Banco>(b => b.RegimeLimite == RegimeLimiteBanco.GlobalPuro));
    }

    [Fact]
    public async Task CriarBanco_SemRegime_NascePerModalidade()
    {
        // Arrange
        var repo = Substitute.For<IBancoRepository>();
        var handler = new CreateBancoCommandHandler(repo, CriarClock());
        var cmd = new CreateBancoCommand("033", "Banco Santander S.A.", "Santander");

        // Act
        BancoDto dto = await handler.Handle(cmd, default);

        // Assert
        dto.RegimeLimite.Should().Be(RegimeLimiteBanco.PerModalidade.ToString());
    }
}
