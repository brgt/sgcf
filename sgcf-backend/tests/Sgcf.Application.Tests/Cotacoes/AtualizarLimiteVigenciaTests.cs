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

[Trait("Category", "Unit")]
public sealed class AtualizarLimiteVigenciaTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 28, 10, 0);
    private static readonly Guid BancoId = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock c = NSubstitute.Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(Agora);
        return c;
    }

    private static LimiteBanco CriarLimite(decimal valorLimite = 10_000_000m, decimal valorUtilizado = 0m)
    {
        IClock clock = CriarClock();
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(valorLimite, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock);

        if (valorUtilizado > 0)
        {
            limite.RegistrarUso(new Money(valorUtilizado, Moeda.Brl), clock);
        }

        return limite;
    }

    private static UpdateLimiteBancoCommandHandler CriarHandler(
        ILimiteBancoRepository? repo = null,
        ILimiteGlobalBancoRepository? limiteGlobal = null,
        IClock? clock = null)
    {
        return new UpdateLimiteBancoCommandHandler(
            repo ?? NSubstitute.Substitute.For<ILimiteBancoRepository>(),
            limiteGlobal ?? NSubstitute.Substitute.For<ILimiteGlobalBancoRepository>(),
            clock ?? CriarClock());
    }

    // ── NovaDataVigenciaFim — sem utilização ─────────────────────────────────

    [Fact]
    public async Task Handle_ComNovaDataVigenciaFim_LimiteSemUso_RetornaResponseSemAvisos()
    {
        // Arrange
        LimiteBanco limite = CriarLimite(valorLimite: 10_000_000m, valorUtilizado: 0m);
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);

        var handler = CriarHandler(repo);
        var cmd = new UpdateLimiteBancoCommand(
            limite.Id,
            NovaDataVigenciaFim: new DateOnly(2026, 12, 31));

        // Act
        AtualizarLimiteBancoResponse response = await handler.Handle(cmd, default);

        // Assert
        response.Avisos.Should().BeEmpty();
        response.Limite.DataVigenciaFim.Should().Be(new DateOnly(2026, 12, 31));
    }

    // ── NovaDataVigenciaFim — com utilização ativa ───────────────────────────

    [Fact]
    public async Task Handle_ComNovaDataVigenciaFim_LimiteComUsoAtivo_RetornaResponseComAviso()
    {
        // Arrange
        LimiteBanco limite = CriarLimite(valorLimite: 10_000_000m, valorUtilizado: 3_000_000m);
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);

        var handler = CriarHandler(repo);
        var cmd = new UpdateLimiteBancoCommand(
            limite.Id,
            NovaDataVigenciaFim: new DateOnly(2026, 12, 31));

        // Act
        AtualizarLimiteBancoResponse response = await handler.Handle(cmd, default);

        // Assert
        response.Avisos.Should().ContainSingle()
            .Which.Should().Contain("3.000.000");
    }

    // ── MotivoEncerramento persiste ──────────────────────────────────────────

    [Fact]
    public async Task Handle_MotivoEncerramento_EhPersistidoNoLimite()
    {
        // Arrange
        LimiteBanco limite = CriarLimite();
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);

        var handler = CriarHandler(repo);
        var cmd = new UpdateLimiteBancoCommand(
            limite.Id,
            NovaDataVigenciaFim: new DateOnly(2026, 6, 30),
            MotivoEncerramento: "Banco retirou linha de crédito");

        // Act
        AtualizarLimiteBancoResponse response = await handler.Handle(cmd, default);

        // Assert
        response.Limite.MotivoEncerramento.Should().Be("Banco retirou linha de crédito");
    }

    // ── Sobreposição de vigência ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_NovaDataVigenciaFim_ComSobreposicao_LancaInvalidOperationException()
    {
        // Arrange
        LimiteBanco limite = CriarLimite();
        LimiteBanco conflito = CriarLimite();

        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns(conflito);

        var handler = CriarHandler(repo);
        var cmd = new UpdateLimiteBancoCommand(
            limite.Id,
            NovaDataVigenciaFim: new DateOnly(2027, 12, 31));

        // Act
        var act = async () => await handler.Handle(cmd, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sobreposição*");
    }

    // ── Sem novaDataVigenciaFim — compat. retroativa ─────────────────────────

    [Fact]
    public async Task Handle_SemNovaDataVigenciaFim_RetornaResponseComAvisosVazios()
    {
        // Arrange
        LimiteBanco limite = CriarLimite();
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);

        var handler = CriarHandler(repo);
        var cmd = new UpdateLimiteBancoCommand(
            limite.Id,
            NovoValorLimiteBrl: 15_000_000m);

        // Act
        AtualizarLimiteBancoResponse response = await handler.Handle(cmd, default);

        // Assert
        response.Avisos.Should().BeEmpty();
        response.Limite.ValorLimiteBrl.Should().Be(15_000_000m);
    }

    // ── NovaDataVigenciaFim não chama FindOverlapping quando ausente ─────────

    [Fact]
    public async Task Handle_SemNovaDataVigenciaFim_NaoVerificaSobreposicao()
    {
        // Arrange
        LimiteBanco limite = CriarLimite();
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(limite.Id, default).Returns(limite);

        var handler = CriarHandler(repo);
        var cmd = new UpdateLimiteBancoCommand(limite.Id);

        // Act
        await handler.Handle(cmd, default);

        // Assert — FindOverlappingAsync never called when no date change
        await repo.DidNotReceive().FindOverlappingAsync(
            Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(),
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
