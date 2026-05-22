using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Antecipacao;
using Sgcf.Application.Antecipacao.Commands;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Antecipacao;

/// <summary>
/// Testes unitários do handler de simulação de antecipação usando mocks.
/// Verifica persistência, alertas e regras de isenção sem depender de banco de dados.
/// Após S32: PadraoAntecipacao e parâmetros de cálculo residem em LimiteBanco.
/// </summary>
[Trait("Category", "Domain")]
public sealed class SimularAntecipacaoCommandTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 11, 12, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static Contrato CriarContrato(Guid bancoId, Moeda moeda = Moeda.Usd)
    {
        IClock clock = CriarClock();
        return Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, moeda),
            dataContratacao: new LocalDate(2025, 1, 15),
            dataVencimento: new LocalDate(2026, 7, 15),
            taxaAa: Percentual.De(8.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    private static Banco CriarBanco(string codigoCompe, IClock clock)
    {
        Banco banco = Banco.Criar(codigoCompe, "Banco Teste S.A.", "Teste", clock);
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: true,
            exigeAnuenciaExpressa: false,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 2,
            clock: clock);
        return banco;
    }

    /// <summary>
    /// Cria um LimiteBanco com PadraoAntecipacao A (BreakFundingFee) configurado.
    /// Os parâmetros são passados em fração (0.01 = 1%).
    /// </summary>
    private static LimiteBanco CriarLimitePadraoA(Guid bancoId, IClock clock)
    {
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2025, 1, 1),
            clock: clock,
            padraoAntecipacao: PadraoAntecipacao.A);

        // BreakFundingFeePct = 1% → fração 0.01
        limite.ConfigurarAntecipacao(
            padraoAntecipacao: PadraoAntecipacao.A,
            breakFundingFeePct: Percentual.De(1m).AsDecimal,
            tlaPctSobreSaldo: null,
            tlaPctPorMesRemanescente: null,
            valorMinimoParcialPct: null,
            observacoesAntecipacao: null,
            clock: clock);

        return limite;
    }

    private static LimiteBanco CriarLimitePadraoB(Guid bancoId, IClock clock)
    {
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2025, 1, 1),
            clock: clock,
            padraoAntecipacao: PadraoAntecipacao.B);

        return limite;
    }

    private static LimiteBanco CriarLimitePadraoD(Guid bancoId, IClock clock)
    {
        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2025, 1, 1),
            clock: clock,
            padraoAntecipacao: PadraoAntecipacao.D);

        limite.ConfigurarAntecipacao(
            padraoAntecipacao: PadraoAntecipacao.D,
            breakFundingFeePct: null,
            tlaPctSobreSaldo: Percentual.De(2m).AsDecimal,       // 2%
            tlaPctPorMesRemanescente: Percentual.De(0.1m).AsDecimal, // 0.1%
            valorMinimoParcialPct: null,
            observacoesAntecipacao: null,
            clock: clock);

        return limite;
    }

    // ── Teste 1: SalvarSimulacao=true → AddAsync chamado uma vez ──────────────

    [Fact]
    public async Task PadraoA_SalvarSimulacao_True_PersistSimulacao()
    {
        // Arrange
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);
        Banco banco = CriarBanco("036", clock);
        LimiteBanco limiteBanco = CriarLimitePadraoA(bancoId, clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        limiteBancoRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, Arg.Any<CancellationToken>())
            .Returns(limiteBanco);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, limiteBancoRepo, simulacaoRepo, clock);

        SimularAntecipacaoCommand cmd = new(
            ContratoId: contrato.Id,
            TipoAntecipacao: TipoAntecipacao.LiquidacaoTotalAntecipada,
            DataEfetiva: new LocalDate(2026, 6, 1),
            ValorPrincipalAQuitarMoedaOriginal: null,
            TaxaMercadoAtualAa: null,
            IndenizacaoBancoMoedaOriginal: null,
            SalvarSimulacao: true,
            CreatedBy: "test@proxysgroup.com",
            Source: "API");

        // Act
        ResultadoSimulacaoDto result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await simulacaoRepo.Received(1).AddAsync(Arg.Any<SimulacaoAntecipacao>(), Arg.Any<CancellationToken>());
        result.SimulacaoId.Should().NotBeNull();
    }

    // ── Teste 2: Padrão B emite alerta Sicredi ────────────────────────────────

    [Fact]
    public async Task PadraoB_Resultado_ExigeAnuenciaExpressa()
    {
        // Arrange
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId, Moeda.Usd);

        // Banco com ExigeAnuenciaExpressa = true (política institucional)
        Banco banco = Banco.Criar("748", "Sicredi", "Sicredi", clock);
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: false,
            exigeAnuenciaExpressa: true,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 5,
            clock: clock);

        LimiteBanco limiteBanco = CriarLimitePadraoB(bancoId, clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        limiteBancoRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, Arg.Any<CancellationToken>())
            .Returns(limiteBanco);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, limiteBancoRepo, simulacaoRepo, clock);

        SimularAntecipacaoCommand cmd = new(
            ContratoId: contrato.Id,
            TipoAntecipacao: TipoAntecipacao.LiquidacaoTotalAntecipada,
            DataEfetiva: new LocalDate(2026, 6, 1),
            ValorPrincipalAQuitarMoedaOriginal: null,
            TaxaMercadoAtualAa: null,
            IndenizacaoBancoMoedaOriginal: null,
            SalvarSimulacao: false,
            CreatedBy: "test@proxysgroup.com",
            Source: "API");

        // Act
        ResultadoSimulacaoDto result = await handler.Handle(cmd, CancellationToken.None);

        // Assert — Padrão B (Sicredi) deve ter alerta crítico sobre período total
        result.Alertas.Should().NotBeEmpty();
        string alertaUnido = string.Concat(result.Alertas);
        alertaUnido.Should().ContainAny("período total", "Sicredi");
    }

    // ── Teste: LimiteBanco não encontrado → InvalidOperationException ────────

    [Fact]
    [Trait("Category", "Application")]
    public async Task LimiteBancoNaoEncontrado_LancaInvalidOperationException()
    {
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);
        Banco banco = CriarBanco("036", clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        limiteBancoRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, Arg.Any<CancellationToken>())
            .Returns((LimiteBanco?)null);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, limiteBancoRepo, simulacaoRepo, clock);

        SimularAntecipacaoCommand cmd = new(
            ContratoId: contrato.Id,
            TipoAntecipacao: TipoAntecipacao.LiquidacaoTotalAntecipada,
            DataEfetiva: new LocalDate(2026, 6, 1),
            ValorPrincipalAQuitarMoedaOriginal: null,
            TaxaMercadoAtualAa: null,
            IndenizacaoBancoMoedaOriginal: null,
            SalvarSimulacao: false,
            CreatedBy: "test@proxysgroup.com",
            Source: "API");

        Func<Task> act = () => handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Limite bancário não configurado*");
    }

    // ── Teste: LimiteBanco sem PadraoAntecipacao → InvalidOperationException ─

    [Fact]
    [Trait("Category", "Application")]
    public async Task LimiteBanco_PadraoAntecipacaoNulo_LancaInvalidOperationException()
    {
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);
        Banco banco = CriarBanco("036", clock);

        LimiteBanco limiteSemPadrao = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2025, 1, 1),
            clock: clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        limiteBancoRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, Arg.Any<CancellationToken>())
            .Returns(limiteSemPadrao);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, limiteBancoRepo, simulacaoRepo, clock);

        SimularAntecipacaoCommand cmd = new(
            ContratoId: contrato.Id,
            TipoAntecipacao: TipoAntecipacao.LiquidacaoTotalAntecipada,
            DataEfetiva: new LocalDate(2026, 6, 1),
            ValorPrincipalAQuitarMoedaOriginal: null,
            TaxaMercadoAtualAa: null,
            IndenizacaoBancoMoedaOriginal: null,
            SalvarSimulacao: false,
            CreatedBy: "test@proxysgroup.com",
            Source: "API");

        Func<Task> act = () => handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Teste 3: Padrão D com RefinanciamentoInterno → TLA = 0 ───────────────

    [Fact]
    public async Task PadraoD_RefinanciamentoInterno_TlaZero()
    {
        // Arrange
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId, Moeda.Brl);
        Banco banco = CriarBanco("104", clock);
        LimiteBanco limiteBanco = CriarLimitePadraoD(bancoId, clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        limiteBancoRepo.GetByBancoModalidadeAsync(bancoId, ModalidadeContrato.Finimp, Arg.Any<CancellationToken>())
            .Returns(limiteBanco);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, limiteBancoRepo, simulacaoRepo, clock);

        SimularAntecipacaoCommand cmd = new(
            ContratoId: contrato.Id,
            TipoAntecipacao: TipoAntecipacao.RefinanciamentoInterno,
            DataEfetiva: new LocalDate(2026, 6, 1),
            ValorPrincipalAQuitarMoedaOriginal: null,
            TaxaMercadoAtualAa: null,
            IndenizacaoBancoMoedaOriginal: null,
            SalvarSimulacao: false,
            CreatedBy: "test@proxysgroup.com",
            Source: "API");

        // Act
        ResultadoSimulacaoDto result = await handler.Handle(cmd, CancellationToken.None);

        // Assert — TLA = 0 para refinanciamento interno
        // TOTAL deve ser apenas VTD (principal + juros pro rata), sem TLA
        result.Alertas.Should().NotBeEmpty(because: "alerta de isenção deve ser emitido");
        string alertaUnido = string.Concat(result.Alertas).ToLowerInvariant();
        alertaUnido.Should().ContainAny("isenção", "refinanciamento");
    }
}
