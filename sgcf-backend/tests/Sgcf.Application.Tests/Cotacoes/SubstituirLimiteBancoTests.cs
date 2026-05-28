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
public sealed class SubstituirLimiteBancoTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 28, 12, 0);
    private static readonly Guid BancoId = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock c = NSubstitute.Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(Agora);
        return c;
    }

    private static LimiteBanco CriarLimite(
        LocalDate? inicio = null,
        LocalDate? fim = null,
        decimal valorUtilizado = 0m)
    {
        IClock clock = CriarClock();
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: inicio ?? new LocalDate(2026, 1, 1),
            clock: clock,
            dataVigenciaFim: fim);

        if (valorUtilizado > 0)
        {
            limite.RegistrarUso(new Money(valorUtilizado, Moeda.Brl), clock);
        }

        return limite;
    }

    private static SubstituirLimiteBancoCommandHandler CriarHandler(
        ILimiteBancoRepository? repo = null,
        ILimiteGlobalBancoRepository? limiteGlobal = null,
        IClock? clock = null)
    {
        return new SubstituirLimiteBancoCommandHandler(
            repo ?? NSubstitute.Substitute.For<ILimiteBancoRepository>(),
            limiteGlobal ?? NSubstitute.Substitute.For<ILimiteGlobalBancoRepository>(),
            clock ?? CriarClock());
    }

    // ── Substituição bem-sucedida ────────────────────────────────────────────

    [Fact]
    public async Task Handle_Substituicao_EncerraAnteriorECriaSucessor()
    {
        // Arrange
        var novoInicio = new DateOnly(2027, 1, 1);
        LimiteBanco anterior = CriarLimite(inicio: new LocalDate(2026, 1, 1));
        LimiteBancoDto? sucessorCapturado = null;

        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);
        repo.When(r => r.Add(Arg.Any<LimiteBanco>()))
            .Do(ci => sucessorCapturado = LimiteBancoDto.From(ci.Arg<LimiteBanco>()));

        var handler = CriarHandler(repo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: novoInicio,
            NovoValorLimiteBrl: 15_000_000m);

        // Act
        LimiteBancoDto resultado = await handler.Handle(cmd, default);

        // Assert — anterior tem dataVigenciaFim = novoInicio - 1 dia
        anterior.DataVigenciaFim.Should().Be(new LocalDate(2026, 12, 31));

        // Assert — resultado é o DTO do sucessor
        resultado.DataVigenciaInicio.Should().Be(novoInicio);
        resultado.ValorLimiteBrl.Should().Be(15_000_000m);
        resultado.BancoId.Should().Be(BancoId);
    }

    [Fact]
    public async Task Handle_Substituicao_SucessorNaoHerdaAntecipacao()
    {
        // Arrange
        LimiteBanco anterior = CriarLimite();
        anterior.ConfigurarAntecipacao(
            PadraoAntecipacao.A,
            Percentual.De(1m).AsDecimal,
            null, null, null, null,
            CriarClock());

        LimiteBanco? sucessorAdicionado = null;
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);
        repo.When(r => r.Add(Arg.Any<LimiteBanco>()))
            .Do(ci => sucessorAdicionado = ci.Arg<LimiteBanco>());

        var handler = CriarHandler(repo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: new DateOnly(2027, 1, 1),
            NovoValorLimiteBrl: 10_000_000m);

        // Act
        await handler.Handle(cmd, default);

        // Assert — sucessor não tem antecipação configurada
        sucessorAdicionado.Should().NotBeNull();
        sucessorAdicionado!.PadraoAntecipacao.Should().BeNull();
        sucessorAdicionado.BreakFundingFeePct.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Substituicao_MotivoEncerramentoPersistidoNoAnterior()
    {
        // Arrange
        LimiteBanco anterior = CriarLimite();
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);

        var handler = CriarHandler(repo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: new DateOnly(2027, 1, 1),
            NovoValorLimiteBrl: 10_000_000m,
            MotivoEncerramento: "Renovação anual — comitê mai/2026");

        // Act
        await handler.Handle(cmd, default);

        // Assert
        anterior.MotivoEncerramento.Should().Be("Renovação anual — comitê mai/2026");
    }

    // ── Validações ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NovoInicioAnteriorAoInicioExistente_LancaArgumentException()
    {
        // Arrange
        LimiteBanco anterior = CriarLimite(inicio: new LocalDate(2026, 6, 1));
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);

        var handler = CriarHandler(repo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: new DateOnly(2026, 5, 1),
            NovoValorLimiteBrl: 10_000_000m);

        // Act
        var act = async () => await handler.Handle(cmd, default);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NovoInicio*posterior*");
    }

    [Fact]
    public async Task Handle_SobreposicaoDoSucessor_LancaInvalidOperationException()
    {
        // Arrange
        LimiteBanco anterior = CriarLimite(inicio: new LocalDate(2026, 1, 1));
        LimiteBanco conflito = CriarLimite(inicio: new LocalDate(2027, 6, 1));

        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns(conflito);

        var handler = CriarHandler(repo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: new DateOnly(2027, 1, 1),
            NovoValorLimiteBrl: 10_000_000m);

        // Act
        var act = async () => await handler.Handle(cmd, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sobreposição*");
    }

    [Fact]
    public async Task Handle_ValorAcimaLimiteGlobal_LancaInvalidOperationException()
    {
        // Arrange
        LimiteBanco anterior = CriarLimite();
        LimiteGlobalBanco limiteGlobal = LimiteGlobalBanco.Criar(
            BancoId,
            new Money(5_000_000m, Moeda.Brl),
            new LocalDate(2026, 1, 1),
            CriarClock());

        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        var globalRepo = NSubstitute.Substitute.For<ILimiteGlobalBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);
        globalRepo.GetVigenteByBancoAsync(BancoId, default).Returns(limiteGlobal);

        var handler = CriarHandler(repo, globalRepo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: new DateOnly(2027, 1, 1),
            NovoValorLimiteBrl: 10_000_000m);

        // Act
        var act = async () => await handler.Handle(cmd, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LG-09*");
    }

    // ── Persistência atômica ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PersisteSoComUmSaveChanges()
    {
        // Arrange — garante que tudo é commitado em uma única chamada
        LimiteBanco anterior = CriarLimite();
        var repo = NSubstitute.Substitute.For<ILimiteBancoRepository>();
        repo.GetByIdTrackingAsync(anterior.Id, default).Returns(anterior);
        repo.FindOverlappingAsync(BancoId, ModalidadeContrato.Finimp,
            Arg.Any<LocalDate>(), Arg.Any<LocalDate?>(), Arg.Any<Guid?>(), default)
            .Returns((LimiteBanco?)null);

        var handler = CriarHandler(repo);
        var cmd = new SubstituirLimiteBancoCommand(
            anterior.Id,
            NovoInicio: new DateOnly(2027, 1, 1),
            NovoValorLimiteBrl: 10_000_000m);

        // Act
        await handler.Handle(cmd, default);

        // Assert — Add chamado 1x, SaveChanges chamado 1x
        repo.Received(1).Add(Arg.Any<LimiteBanco>());
        await repo.Received(1).SaveChangesAsync(default);
    }
}
