using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Tesouraria;
using Sgcf.Application.Tesouraria.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;
using Xunit;

namespace Sgcf.Application.Tests.Tesouraria;

[Trait("Category", "Application")]
public sealed class SaldoCaixaCommandTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant InstanteBase = Instant.FromUtc(2026, 5, 21, 10, 0);

    [Fact]
    public async Task Handle_UpsertSaldoExistente_ChamaAuditLogWriter()
    {
        var contaId = Guid.NewGuid();
        IClock clock = CriarClock(InstanteBase);

        SaldoCaixa saldoExistente = SaldoCaixa.Criar(
            contaId, new LocalDate(2026, 5, 21), new Money(500m, Moeda.Brl), "original", clock);

        ContaBancaria conta = ContaBancaria.Criar(
            Guid.NewGuid(), "Conta Teste", "0001", "12345-6", Moeda.Brl, clock);

        ISaldoCaixaRepository saldoRepo = Substitute.For<ISaldoCaixaRepository>();
        IContaBancariaRepository contaRepo = Substitute.For<IContaBancariaRepository>();
        IAuditLogWriter auditLog = Substitute.For<IAuditLogWriter>();

        saldoRepo.GetAsync(contaId, new LocalDate(2026, 5, 21), Arg.Any<CancellationToken>())
            .Returns(saldoExistente);
        contaRepo.GetByIdAsync(contaId, Arg.Any<CancellationToken>())
            .Returns(conta);
        auditLog.WriteAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new UpsertLoteSaldoCaixaCommandHandler(saldoRepo, contaRepo, auditLog, clock);
        var command = new UpsertLoteSaldoCaixaCommand(
            [new UpsertSaldoCaixaItemDto(contaId, "2026-05-21", 1000m, "Brl", "operador")]);

        await handler.Handle(command, CancellationToken.None);

        await auditLog.Received(1).WriteAsync(
            "SaldoCaixa", saldoExistente.Id, "UPDATE",
            Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UpsertSaldoNovo_NaoChamaAuditLogWriter()
    {
        var contaId = Guid.NewGuid();
        IClock clock = CriarClock(InstanteBase);

        ContaBancaria conta = ContaBancaria.Criar(
            Guid.NewGuid(), "Conta Nova", "0002", "99999-0", Moeda.Brl, clock);

        ISaldoCaixaRepository saldoRepo = Substitute.For<ISaldoCaixaRepository>();
        IContaBancariaRepository contaRepo = Substitute.For<IContaBancariaRepository>();
        IAuditLogWriter auditLog = Substitute.For<IAuditLogWriter>();

        saldoRepo.GetAsync(contaId, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((SaldoCaixa?)null);
        contaRepo.GetByIdAsync(contaId, Arg.Any<CancellationToken>())
            .Returns(conta);

        var handler = new UpsertLoteSaldoCaixaCommandHandler(saldoRepo, contaRepo, auditLog, clock);
        var command = new UpsertLoteSaldoCaixaCommand(
            [new UpsertSaldoCaixaItemDto(contaId, "2026-05-21", 750m, "Brl", "operador")]);

        await handler.Handle(command, CancellationToken.None);

        await auditLog.DidNotReceive().WriteAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ContaNaoEncontrada_LancaKeyNotFoundException()
    {
        var contaIdInexistente = Guid.NewGuid();
        IClock clock = CriarClock(InstanteBase);

        ISaldoCaixaRepository saldoRepo = Substitute.For<ISaldoCaixaRepository>();
        IContaBancariaRepository contaRepo = Substitute.For<IContaBancariaRepository>();
        IAuditLogWriter auditLog = Substitute.For<IAuditLogWriter>();

        contaRepo.GetByIdAsync(contaIdInexistente, Arg.Any<CancellationToken>())
            .Returns((ContaBancaria?)null);

        var handler = new UpsertLoteSaldoCaixaCommandHandler(saldoRepo, contaRepo, auditLog, clock);
        var command = new UpsertLoteSaldoCaixaCommand(
            [new UpsertSaldoCaixaItemDto(contaIdInexistente, "2026-05-21", 100m, "Brl", "operador")]);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{contaIdInexistente}*");
    }
}
