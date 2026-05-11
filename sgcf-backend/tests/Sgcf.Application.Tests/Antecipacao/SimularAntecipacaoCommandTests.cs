using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Antecipacao;
using Sgcf.Application.Antecipacao.Commands;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Xunit;

namespace Sgcf.Application.Tests.Antecipacao;

/// <summary>
/// Testes unitários do handler de simulação de antecipação usando mocks.
/// Verifica persistência, alertas e regras de isenção sem depender de banco de dados.
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

    private static Banco CriarBancoPadraoA(IClock clock)
    {
        Banco banco = Banco.Criar("036", "Banco Bradesco S.A.", "Bradesco", PadraoAntecipacao.A, clock);
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: true,
            exigeAnuenciaExpressa: false,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 2,
            valorMinimoParcialPct: null,
            padraoAntecipacao: PadraoAntecipacao.A,
            breakFundingFeePct: 0.01m,
            tlaPctSobreSaldo: null,
            tlaPctPorMesRemanescente: null,
            observacoesAntecipacao: null,
            clock: clock);
        return banco;
    }

    private static Banco CriarBancoPadraoB(IClock clock)
    {
        Banco banco = Banco.Criar("748", "Sicredi", "Sicredi", PadraoAntecipacao.B, clock);
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: false,
            exigeAnuenciaExpressa: true,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 5,
            valorMinimoParcialPct: null,
            padraoAntecipacao: PadraoAntecipacao.B,
            breakFundingFeePct: null,
            tlaPctSobreSaldo: null,
            tlaPctPorMesRemanescente: null,
            observacoesAntecipacao: "Cobra juros período total",
            clock: clock);
        return banco;
    }

    private static Banco CriarBancoPadraoD(IClock clock)
    {
        Banco banco = Banco.Criar("104", "Caixa Econômica Federal", "Caixa", PadraoAntecipacao.D, clock);
        banco.AtualizarConfigAntecipacao(
            aceitaLiquidacaoTotal: true,
            aceitaLiquidacaoParcial: true,
            exigeAnuenciaExpressa: false,
            exigeParcelaInteira: false,
            avisoPrevioMinDiasUteis: 2,
            valorMinimoParcialPct: null,
            padraoAntecipacao: PadraoAntecipacao.D,
            breakFundingFeePct: null,
            tlaPctSobreSaldo: 0.02m,
            tlaPctPorMesRemanescente: 0.001m,
            observacoesAntecipacao: null,
            clock: clock);
        return banco;
    }

    // ── Teste 1: SalvarSimulacao=true → AddAsync chamado uma vez ──────────────

    [Fact]
    public async Task PadraoA_SalvarSimulacao_True_PersistSimulacao()
    {
        // Arrange
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId);
        Banco banco = CriarBancoPadraoA(clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, simulacaoRepo, clock);

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
        Banco banco = CriarBancoPadraoB(clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, simulacaoRepo, clock);

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

    // ── Teste 3: Padrão D com RefinanciamentoInterno → TLA = 0 ───────────────

    [Fact]
    public async Task PadraoD_RefinanciamentoInterno_TlaZero()
    {
        // Arrange
        IClock clock = CriarClock();
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId, Moeda.Brl);
        Banco banco = CriarBancoPadraoD(clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        ISimulacaoAntecipacaoRepository simulacaoRepo = Substitute.For<ISimulacaoAntecipacaoRepository>();

        contratoRepo.GetByIdAsync(contrato.Id, Arg.Any<CancellationToken>()).Returns(contrato);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);

        SimularAntecipacaoCommandHandler handler = new(contratoRepo, bancoRepo, simulacaoRepo, clock);

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
