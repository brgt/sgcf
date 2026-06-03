using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes cruzados que verificam o invariante LG-09 entre os handlers
/// <see cref="CreateLimiteBancoCommandHandler"/> / <see cref="UpdateLimiteBancoCommandHandler"/>
/// e <see cref="LimiteGlobalBanco"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LimiteGlobalBancoInvariantesCruzadasTests
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

    private static LimiteGlobalBanco CriarLimiteGlobal(decimal valorBrl)
    {
        return LimiteGlobalBanco.Criar(
            BancoId,
            new Money(valorBrl, Moeda.Brl),
            DataInicio,
            CriarClock());
    }

    private static LimiteBanco CriarLimiteBanco(Guid? bancoId = null, decimal valorBrl = 100_000m)
    {
        return LimiteBanco.Criar(
            bancoId: bancoId ?? BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(valorBrl, Moeda.Brl),
            dataVigenciaInicio: DataInicio,
            clock: CriarClock());
    }

    private static CreateLimiteBancoCommandHandler CriarHandlerCreate(
        ILimiteBancoRepository? repo = null,
        ILimiteGlobalBancoRepository? limiteGlobalRepo = null,
        IClock? clock = null)
    {
        return new CreateLimiteBancoCommandHandler(
            repo ?? Substitute.For<ILimiteBancoRepository>(),
            limiteGlobalRepo ?? Substitute.For<ILimiteGlobalBancoRepository>(),
            Substitute.For<Sgcf.Application.Bancos.IBancoRepository>(),
            clock ?? CriarClock());
    }

    private static UpdateLimiteBancoCommandHandler CriarHandlerUpdate(
        ILimiteBancoRepository? repo = null,
        ILimiteGlobalBancoRepository? limiteGlobalRepo = null,
        IClock? clock = null)
    {
        return new UpdateLimiteBancoCommandHandler(
            repo ?? Substitute.For<ILimiteBancoRepository>(),
            limiteGlobalRepo ?? Substitute.For<ILimiteGlobalBancoRepository>(),
            Substitute.For<Sgcf.Application.Bancos.IBancoRepository>(),
            clock ?? CriarClock());
    }

    // ─── Grupo A: CreateLimiteBancoCommandHandler + LG-09 ───────────────────────

    [Fact]
    public async Task Criar_LimiteBanco_quando_nao_existe_limite_global_deve_permitir()
    {
        // Arrange — GetVigenteByBancoAsync retorna null: nenhum limite global cadastrado.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();

        LimiteBanco? semConflito = null;
        LimiteGlobalBanco? semGlobal = null;
        repo.FindOverlappingAsync(BancoId, Arg.Any<ModalidadeContrato>(), Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, Arg.Any<CancellationToken>())
            .Returns(semConflito);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(semGlobal);

        var handler = CriarHandlerCreate(repo, limiteGlobalRepo, clock);
        var cmd = new CreateLimiteBancoCommand(
            BancoId,
            Modalidade: ModalidadeContrato.Finimp.ToString(),
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1));

        // Act — não deve lançar exceção.
        Func<Task> act = () => handler.Handle(cmd, default);

        // Assert
        await act.Should().NotThrowAsync();
        repo.Received(1).Add(Arg.Any<LimiteBanco>());
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Criar_LimiteBanco_dentro_do_limite_global_deve_permitir()
    {
        // Arrange — global = 1.000.000, novo = 500.000 → OK.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(1_000_000m);

        LimiteBanco? semConflito = null;
        repo.FindOverlappingAsync(BancoId, Arg.Any<ModalidadeContrato>(), Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, Arg.Any<CancellationToken>())
            .Returns(semConflito);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        var handler = CriarHandlerCreate(repo, limiteGlobalRepo, clock);
        var cmd = new CreateLimiteBancoCommand(
            BancoId,
            Modalidade: ModalidadeContrato.Finimp.ToString(),
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1));

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        repo.Received(1).Add(Arg.Any<LimiteBanco>());
    }

    [Fact]
    public async Task Criar_LimiteBanco_igual_ao_limite_global_deve_permitir()
    {
        // Arrange — global = 500.000, novo = 500.000 → OK (LG-09 usa >, não ≥).
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(500_000m);

        LimiteBanco? semConflito = null;
        repo.FindOverlappingAsync(BancoId, Arg.Any<ModalidadeContrato>(), Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, Arg.Any<CancellationToken>())
            .Returns(semConflito);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        var handler = CriarHandlerCreate(repo, limiteGlobalRepo, clock);
        var cmd = new CreateLimiteBancoCommand(
            BancoId,
            Modalidade: ModalidadeContrato.Finimp.ToString(),
            ValorLimiteBrl: 500_000m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1));

        // Act & Assert — valor igual ao teto deve ser aceito.
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        repo.Received(1).Add(Arg.Any<LimiteBanco>());
    }

    [Fact]
    public async Task Criar_LimiteBanco_acima_do_limite_global_deve_rejeitar()
    {
        // Arrange — global = 500.000, novo = 500.001 → viola LG-09.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(500_000m);

        LimiteBanco? semConflito = null;
        repo.FindOverlappingAsync(BancoId, Arg.Any<ModalidadeContrato>(), Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), null, Arg.Any<CancellationToken>())
            .Returns(semConflito);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        var handler = CriarHandlerCreate(repo, limiteGlobalRepo, clock);
        var cmd = new CreateLimiteBancoCommand(
            BancoId,
            Modalidade: ModalidadeContrato.Finimp.ToString(),
            ValorLimiteBrl: 500_001m,
            DataVigenciaInicio: new DateOnly(2026, 1, 1));

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[LG-09]*");

        repo.DidNotReceive().Add(Arg.Any<LimiteBanco>());
    }

    // ─── Grupo B: UpdateLimiteBancoCommandHandler + LG-09 ───────────────────────

    [Fact]
    public async Task Atualizar_LimiteBanco_sem_mudar_valor_nao_verifica_limite_global()
    {
        // Arrange — NovoValorLimiteBrl = null → handler não deve consultar o limite global.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();

        LimiteBanco existente = CriarLimiteBanco(BancoId, 300_000m);
        repo.GetByIdTrackingAsync(existente.Id, Arg.Any<CancellationToken>())
            .Returns(existente);

        var handler = CriarHandlerUpdate(repo, limiteGlobalRepo, clock);
        var cmd = new UpdateLimiteBancoCommand(
            LimiteId: existente.Id,
            NovoValorLimiteBrl: null);

        // Act
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();

        // Assert — repositório global não deve ter sido consultado.
        await limiteGlobalRepo.DidNotReceive()
            .GetVigenteByBancoAsync(Arg.Any<Guid>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Atualizar_LimiteBanco_para_valor_dentro_do_global_deve_permitir()
    {
        // Arrange — global = 800.000, novo = 600.000 → OK.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(800_000m);

        LimiteBanco existente = CriarLimiteBanco(BancoId, 300_000m);
        repo.GetByIdTrackingAsync(existente.Id, Arg.Any<CancellationToken>())
            .Returns(existente);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        var handler = CriarHandlerUpdate(repo, limiteGlobalRepo, clock);
        var cmd = new UpdateLimiteBancoCommand(
            LimiteId: existente.Id,
            NovoValorLimiteBrl: 600_000m);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        repo.Received(1).Update(existente);
    }

    [Fact]
    public async Task Atualizar_LimiteBanco_para_valor_acima_do_global_deve_rejeitar()
    {
        // Arrange — global = 500.000, novo = 600.000 → viola LG-09.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(500_000m);

        LimiteBanco existente = CriarLimiteBanco(BancoId, 300_000m);
        repo.GetByIdTrackingAsync(existente.Id, Arg.Any<CancellationToken>())
            .Returns(existente);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        var handler = CriarHandlerUpdate(repo, limiteGlobalRepo, clock);
        var cmd = new UpdateLimiteBancoCommand(
            LimiteId: existente.Id,
            NovoValorLimiteBrl: 600_000m);

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[LG-09]*");

        repo.DidNotReceive().Update(Arg.Any<LimiteBanco>());
    }

    [Fact]
    public async Task Atualizar_LimiteBanco_quando_nao_existe_limite_global_deve_permitir()
    {
        // Arrange — GetVigenteByBancoAsync retorna null → sem teto → OK.
        var repo = Substitute.For<ILimiteBancoRepository>();
        var limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        var clock = CriarClock();
        LimiteGlobalBanco? semGlobal = null;

        LimiteBanco existente = CriarLimiteBanco(BancoId, 300_000m);
        repo.GetByIdTrackingAsync(existente.Id, Arg.Any<CancellationToken>())
            .Returns(existente);
        limiteGlobalRepo.GetVigenteByBancoAsync(BancoId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(semGlobal);

        var handler = CriarHandlerUpdate(repo, limiteGlobalRepo, clock);
        var cmd = new UpdateLimiteBancoCommand(
            LimiteId: existente.Id,
            NovoValorLimiteBrl: 999_999m);

        // Act & Assert — sem limite global, qualquer valor deve ser aceito.
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().NotThrowAsync();
        repo.Received(1).Update(existente);
    }
}
