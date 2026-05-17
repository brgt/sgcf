using FluentAssertions;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class AdicionarBancoNaCotacaoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ComLimiteSuficiente_AdicionaBanco()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteBanco limite = TestHelpers.CriarLimiteBanco(bancoId, valorLimiteBrl: 1_000_000m);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        // Act
        AdicionarBancoNaCotacaoResponse resultado = await handler.Handle(cmd, default);

        // Assert
        cotacao.BancosAlvo.Should().Contain(bancoId);
        await cotacaoRepo.Received(1).SaveChangesAsync(default);
        resultado.BancoId.Should().Be(bancoId);
        // Limite sem garantias → sem pré-preenchimento
        resultado.Proposta.Should().BeNull();
        resultado.Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SemLimiteCadastrado_LancaInvalidOperationException()
    {
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho();

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default)
            .Returns((LimiteBanco?)null);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não possui limite cadastrado*");
    }

    [Fact]
    public async Task Handle_ComLimiteInsuficiente_LancaInvalidOperationException()
    {
        Guid bancoId = Guid.NewGuid();
        // ValorAlvo = 500k, Limite = 300k → insuficiente
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteBanco limite = TestHelpers.CriarLimiteBanco(bancoId, valorLimiteBrl: 300_000m);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*limite disponível suficiente*");
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();

        cotacaoRepo.GetByIdAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(Guid.NewGuid(), Guid.NewGuid());

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ─── Pré-preenchimento Task 4.1 ───────────────────────────────────────────

    [Fact]
    public async Task Handle_LimiteComCdbCativo20Pct_RetornaGarantiaPreenchida()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        decimal valorAlvo = 1_000_000m;
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: valorAlvo);

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: TestHelpers.DataAberturaPadrao,
            clock: TestHelpers.CriarClock(),
            garantiasExigidas:
            [
                new GarantiaExigidaLimiteSpec(
                    Tipo: TipoGarantia.CdbCativo,
                    PercentualSobreLimite: 20m,
                    ValorFixoBrl: null,
                    Obrigatoria: true,
                    Observacoes: null)
            ]);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();
        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(
            cotacao.Id,
            bancoId,
            PreencherGarantiaAutomaticamente: true,
            RendimentoCdbAaPercentual: 12.5m);

        // Act
        AdicionarBancoNaCotacaoResponse resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.Proposta.Should().NotBeNull();
        resultado.Proposta!.GarantiaEhCdbCativo.Should().BeTrue();
        resultado.Proposta.GarantiaExigida.Should().Be("CDB cativo 20% (obrigatório)");
        resultado.Proposta.ValorGarantiaExigidaBrl.Should().Be(200_000m); // 20% de 1.000.000
        resultado.Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LimiteComCdbCativo_SemRendimento_LancaInvalidOperationException()
    {
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: TestHelpers.DataAberturaPadrao,
            clock: TestHelpers.CriarClock(),
            garantiasExigidas:
            [
                new GarantiaExigidaLimiteSpec(
                    Tipo: TipoGarantia.CdbCativo,
                    PercentualSobreLimite: 20m,
                    ValorFixoBrl: null,
                    Obrigatoria: true,
                    Observacoes: null)
            ]);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();
        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        // RendimentoCdbAaPercentual ausente — deve falhar (SPEC §3.3)
        AdicionarBancoNaCotacaoCommand cmd = new(
            cotacao.Id,
            bancoId,
            PreencherGarantiaAutomaticamente: true,
            RendimentoCdbAaPercentual: null);

        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CDB cativo*rendimentoCdbAaPercentual*");
    }

    [Fact]
    public async Task Handle_LimiteSemGarantias_RetornaPropostaNull()
    {
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);
        LimiteBanco limite = TestHelpers.CriarLimiteBanco(bancoId, valorLimiteBrl: 1_000_000m);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();
        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        AdicionarBancoNaCotacaoResponse resultado = await handler.Handle(cmd, default);

        resultado.Proposta.Should().BeNull();
        resultado.Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LimiteComAval_RetornaGarantiaComValorZero()
    {
        Guid bancoId = Guid.NewGuid();
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: 500_000m);

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: TestHelpers.DataAberturaPadrao,
            clock: TestHelpers.CriarClock(),
            garantiasExigidas:
            [
                new GarantiaExigidaLimiteSpec(
                    Tipo: TipoGarantia.Aval,
                    PercentualSobreLimite: null,
                    ValorFixoBrl: null,
                    Obrigatoria: true,
                    Observacoes: null)
            ]);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();
        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(cotacao.Id, bancoId);

        AdicionarBancoNaCotacaoResponse resultado = await handler.Handle(cmd, default);

        resultado.Proposta.Should().NotBeNull();
        resultado.Proposta!.GarantiaEhCdbCativo.Should().BeFalse();
        resultado.Proposta.ValorGarantiaExigidaBrl.Should().Be(0m);
        resultado.Proposta.GarantiaExigida.Should().Be("Aval (obrigatório)");
    }

    // ─── Alertas de coerência Task 4.2 ───────────────────────────────────────

    [Fact]
    public async Task Handle_ValorManualDivergente_RetornaAlerta()
    {
        Guid bancoId = Guid.NewGuid();
        decimal valorAlvo = 1_000_000m;
        Cotacao cotacao = TestHelpers.CriarCotacaoRascunho(valorAlvoBrl: valorAlvo);

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: TestHelpers.DataAberturaPadrao,
            clock: TestHelpers.CriarClock(),
            garantiasExigidas:
            [
                new GarantiaExigidaLimiteSpec(
                    Tipo: TipoGarantia.Aval,
                    PercentualSobreLimite: null,
                    ValorFixoBrl: null,
                    Obrigatoria: true,
                    Observacoes: null)
            ]);

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ILimiteBancoRepository limiteRepo = Substitute.For<ILimiteBancoRepository>();
        cotacaoRepo.GetByIdAsync(cotacao.Id, default).Returns(cotacao);
        limiteRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, default).Returns(limite);

        AdicionarBancoNaCotacaoCommandHandler handler = new(cotacaoRepo, limiteRepo);
        AdicionarBancoNaCotacaoCommand cmd = new(
            cotacao.Id,
            bancoId,
            PreencherGarantiaAutomaticamente: true,
            GarantiaExigidaManual: "Aval particular", // diverge de "Aval (obrigatório)"
            ValorGarantiaExigidaBrlManual: 999_999m,  // diverge de 0
            GarantiaEhCdbCativoManual: true);         // diverge de false

        AdicionarBancoNaCotacaoResponse resultado = await handler.Handle(cmd, default);

        resultado.Alertas.Should().HaveCount(3,
            "três campos manuais divergem do pré-preenchimento calculado");
        resultado.Alertas.Should().Contain(a => a.Contains("garantiaExigida"));
        resultado.Alertas.Should().Contain(a => a.Contains("valorGarantiaExigidaBrl"));
        resultado.Alertas.Should().Contain(a => a.Contains("garantiaEhCdbCativo"));
    }
}
