using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class CriarLimiteGlobalBancoHandlerTests
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

    private static Banco CriarBanco()
    {
        return Banco.Criar("341", "Banco Teste S.A.", "TesteBank", CriarClock());
    }

    private static CriarLimiteGlobalBancoCommandHandler CriarHandler(
        ILimiteGlobalBancoRepository? repo = null,
        IBancoRepository? bancoRepo = null,
        IConsultaSaldoBanco? saldo = null,
        ITenantContext? tenantContext = null,
        IClock? clock = null)
    {
        return new CriarLimiteGlobalBancoCommandHandler(
            repo ?? Substitute.For<ILimiteGlobalBancoRepository>(),
            bancoRepo ?? Substitute.For<IBancoRepository>(),
            saldo ?? Substitute.For<IConsultaSaldoBanco>(),
            tenantContext ?? CriarTenantContext(),
            clock ?? CriarClock());
    }

    [Fact]
    public async Task Handle_ComDadosValidos_RegimeA_CriaDtoCorretamente()
    {
        // Arrange
        Banco banco = CriarBanco();
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var bancoRepo = Substitute.For<IBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();
        var clock = CriarClock();

        bancoRepo.GetByIdAsync(BancoId, default).Returns(banco);
        repo.FindOverlappingAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, default)
            .Returns((LimiteGlobalBanco?)null);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(false);

        var handler = CriarHandler(repo, bancoRepo, saldo, tenantContext, clock);
        var cmd = new CriarLimiteGlobalBancoCommand(
            BancoId,
            ValorLimiteBrl: 1_000_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1),
            DataVigenciaFim: null,
            Observacoes: "Limite inicial");

        // Act
        LimiteGlobalBancoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.BancoId.Should().Be(BancoId);
        resultado.ValorLimiteBrl.Should().Be(1_000_000m);
        resultado.DataVigenciaInicio.Should().Be(new DateOnly(2026, 1, 1));
        resultado.DataVigenciaFim.Should().BeNull();
        resultado.Observacoes.Should().Be("Limite inicial");
        resultado.Historico.Should().HaveCount(1, "criação registra entrada inicial no histórico");
        resultado.Historico[0].ValorAnteriorBrl.Should().BeNull();
        resultado.Historico[0].ValorNovoBrl.Should().Be(1_000_000m);

        repo.Received(1).Add(Arg.Any<LimiteGlobalBanco>());
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_QuandoFindOverlappingRetornaConflito_LancaInvalidOperationException()
    {
        // Arrange — LG-05: sobreposição de vigência detectada
        Banco banco = CriarBanco();
        LimiteGlobalBanco existente = LimiteGlobalBanco.Criar(
            BancoId,
            new Money(500_000m, Moeda.Brl),
            new LocalDate(2026, 1, 1),
            CriarClock());

        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var bancoRepo = Substitute.For<IBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        bancoRepo.GetByIdAsync(BancoId, default).Returns(banco);
        repo.FindOverlappingAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, default)
            .Returns(existente);

        var handler = CriarHandler(repo, bancoRepo, saldo);
        var cmd = new CriarLimiteGlobalBancoCommand(
            BancoId,
            ValorLimiteBrl: 800_000m,
            DataVigenciaInicio: new DateOnly(2026, 6, 1),
            DataVigenciaFim: null,
            Observacoes: null);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*vigência sobreposta*");
    }

    [Fact]
    public async Task Handle_RegimeB_SomaModalidadesExcedeNovoGlobal_LancaInvalidOperationException()
    {
        // Arrange — LG-13: soma de limites por modalidade > novo limite global
        Banco banco = CriarBanco();
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var bancoRepo = Substitute.For<IBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();

        bancoRepo.GetByIdAsync(BancoId, default).Returns(banco);
        repo.FindOverlappingAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, default)
            .Returns((LimiteGlobalBanco?)null);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(true);
        saldo.CalcularSomaLimitesModalidadesAsync(BancoId, TenantId, null, default)
            .Returns(new Money(900_000m, Moeda.Brl));

        var handler = CriarHandler(repo, bancoRepo, saldo, tenantContext);
        var cmd = new CriarLimiteGlobalBancoCommand(
            BancoId,
            ValorLimiteBrl: 800_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1),
            DataVigenciaFim: null,
            Observacoes: null);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Soma dos limites por modalidade*");
    }

    [Fact]
    public async Task Handle_RegimeA_SemLimitesModalidades_NaoLancaExcecaoDeOverflow()
    {
        // Arrange — LG-13: regime A não verifica soma de modalidades
        Banco banco = CriarBanco();
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var bancoRepo = Substitute.For<IBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = CriarTenantContext();

        bancoRepo.GetByIdAsync(BancoId, default).Returns(banco);
        repo.FindOverlappingAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, default)
            .Returns((LimiteGlobalBanco?)null);
        saldo.BancoEmRegimePerModalityAsync(BancoId, TenantId, default).Returns(false);

        var handler = CriarHandler(repo, bancoRepo, saldo, tenantContext);
        var cmd = new CriarLimiteGlobalBancoCommand(
            BancoId,
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1),
            DataVigenciaFim: null,
            Observacoes: null);

        // Act — deve completar sem exceção
        LimiteGlobalBancoDto resultado = await handler.Handle(cmd, default);

        resultado.ValorLimiteBrl.Should().Be(500_000m);
        await saldo.DidNotReceive()
            .CalcularSomaLimitesModalidadesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BancoNaoEncontrado_LancaKeyNotFoundException()
    {
        var repo = Substitute.For<ILimiteGlobalBancoRepository>();
        var bancoRepo = Substitute.For<IBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();

        bancoRepo.GetByIdAsync(BancoId, default).Returns((Banco?)null);

        var handler = CriarHandler(repo, bancoRepo, saldo);
        var cmd = new CriarLimiteGlobalBancoCommand(
            BancoId,
            ValorLimiteBrl: 1_000_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1),
            DataVigenciaFim: null,
            Observacoes: null);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 1_000_000, false)]
    [InlineData("A1B2C3D4-E5F6-7890-ABCD-EF1234567890", 0, false)]
    [InlineData("A1B2C3D4-E5F6-7890-ABCD-EF1234567890", -100, false)]
    public void Validator_ComCamposInvalidos_FailsValidation(string bancoIdStr, decimal valorLimite, bool esperaValido)
    {
        var validator = new CriarLimiteGlobalBancoCommandValidator();
        var cmd = new CriarLimiteGlobalBancoCommand(
            Guid.Parse(bancoIdStr),
            ValorLimiteBrl: valorLimite,
            DataVigenciaInicio: new DateOnly(2026, 1, 1),
            DataVigenciaFim: null,
            Observacoes: null);

        var resultado = validator.Validate(cmd);

        resultado.IsValid.Should().Be(esperaValido);
    }

    [Fact]
    public void Validator_ComDadosValidos_PassaValidation()
    {
        var validator = new CriarLimiteGlobalBancoCommandValidator();
        var cmd = new CriarLimiteGlobalBancoCommand(
            BancoId,
            ValorLimiteBrl: 1_000_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1),
            DataVigenciaFim: null,
            Observacoes: null);

        var resultado = validator.Validate(cmd);

        resultado.IsValid.Should().BeTrue();
    }
}
