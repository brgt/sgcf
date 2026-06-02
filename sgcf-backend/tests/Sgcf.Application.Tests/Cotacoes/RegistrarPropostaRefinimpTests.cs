using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes para a validação de moeda da proposta REFINIMP em RegistrarPropostaCommand.
/// SPEC §4.2 — Onda 1.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RegistrarPropostaRefinimpTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 6, 1, 12, 0);
    private static readonly LocalDate DataAbertura = new(2026, 6, 1);
    private static readonly LocalDate DataPtax = new(2026, 5, 31);
    private const decimal PtaxValida = 5.20m;

    private static readonly Guid BancoId = Guid.NewGuid();
    private static readonly Guid ContratoMaeId = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static CotacaoFx CriarPtaxFx() =>
        CotacaoFx.Criar(
            Moeda.Usd, TipoCotacao.PtaxD0,
            new Money(PtaxValida - 0.05m, Moeda.Brl),
            new Money(PtaxValida, Moeda.Brl),
            "BACEN", Agora.Minus(Duration.FromHours(13)));

    /// <summary>Cria cotação REFINIMP já em EmCaptacao.</summary>
    private static Cotacao CriarCotacaoRefinimp(IClock clock)
    {
        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-R0001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock,
            contratoMaeId: ContratoMaeId);

        cotacao.Enviar(clock);
        cotacao.AdicionarBancoAlvo(BancoId);
        return cotacao;
    }

    /// <summary>Cria cotação FINIMP já em EmCaptacao para testes de regressão.</summary>
    private static Cotacao CriarCotacaoFinimp(IClock clock)
    {
        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock);

        cotacao.Enviar(clock);
        cotacao.AdicionarBancoAlvo(BancoId);
        return cotacao;
    }

    private static Contrato CriarContratoMaeUsd(IClock clock) =>
        Contrato.Criar(
            numeroExterno: "FIN-2026-0001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2025, 12, 1),
            dataVencimento: new LocalDate(2026, 12, 1),
            taxaAa: Percentual.De(6.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

    private static RegistrarPropostaCommand CriarCmd(Guid cotacaoId, string moeda) =>
        new(
            CotacaoId: cotacaoId,
            BancoId: BancoId,
            MoedaOriginal: moeda,
            ValorOferecido: 500_000m,
            TaxaAa: 5.20m,
            IofPct: 0.38m,
            SpreadAa: 0.50m,
            PrazoDias: 180,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: "Bullet",
            ExigeNdf: false,
            CustoNdfAa: null,
            GarantiaExigida: "Aval",
            ValorGarantiaBrl: 0m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

    // ── Sucesso ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Handle: REFINIMP proposta USD com mãe USD → sucesso")]
    public async Task Handle_Refinimp_moeda_USD_mae_USD_sucesso()
    {
        IClock clock = CriarClock();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        IResolveTipoCotacaoService cotacaoResolver = Substitute.For<IResolveTipoCotacaoService>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();

        Cotacao cotacao = CriarCotacaoRefinimp(clock);
        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        Contrato mae = CriarContratoMaeUsd(clock);
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);

        RegistrarPropostaCommandHandler handler = new(repo, cotacaoResolver, clock, contratoRepo);
        RegistrarPropostaCommand cmd = CriarCmd(cotacao.Id, "Usd");

        // Act — não deve lançar
        PropostaDto resultado = await handler.Handle(cmd, default);

        resultado.MoedaOriginal.Should().Be("Usd");
    }

    // ── Erros ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Handle: REFINIMP proposta EUR com mãe USD → InvalidOperationException 409")]
    public async Task Handle_Refinimp_moeda_EUR_mae_USD_lanca_InvalidOperation_409()
    {
        IClock clock = CriarClock();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        IResolveTipoCotacaoService cotacaoResolver = Substitute.For<IResolveTipoCotacaoService>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();

        Cotacao cotacao = CriarCotacaoRefinimp(clock);
        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        // Faz cross-rate EUR fictício para não bloquear no fxRepo
        CotacaoFx eurUsd = CotacaoFx.Criar(
            Moeda.Eur, TipoCotacao.PtaxD0,
            new Money(1.08m, Moeda.Usd),
            new Money(1.09m, Moeda.Usd),
            "BACEN", Agora.Minus(Duration.FromHours(13)));
        cotacaoResolver.ResolverFxAsync(Moeda.Eur, TipoCotacao.PtaxD0, DataPtax, default)
            .Returns(eurUsd);

        Contrato mae = CriarContratoMaeUsd(clock);
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);

        RegistrarPropostaCommandHandler handler = new(repo, cotacaoResolver, clock, contratoRepo);
        RegistrarPropostaCommand cmd = CriarCmd(cotacao.Id, "Eur");

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*moeda*contrato mãe*");
    }

    // ── Regressão FINIMP ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Handle: FINIMP não executa branch de validação REFINIMP (regressão)")]
    public async Task Handle_Finimp_nao_executa_branch_Refinimp()
    {
        IClock clock = CriarClock();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        IResolveTipoCotacaoService cotacaoResolver = Substitute.For<IResolveTipoCotacaoService>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();

        Cotacao cotacao = CriarCotacaoFinimp(clock);
        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        // Para FINIMP/USD, o handler usa cotacao.PtaxUsadaUsdBrl diretamente (sem fxRepo)
        // IContratoRepository NÃO deve ser chamado para modalidade FINIMP.

        RegistrarPropostaCommandHandler handler = new(repo, cotacaoResolver, clock, contratoRepo);
        RegistrarPropostaCommand cmd = CriarCmd(cotacao.Id, "Usd");

        PropostaDto resultado = await handler.Handle(cmd, default);

        resultado.MoedaOriginal.Should().Be("Usd");
        // Garante que contratoRepo.GetByIdAsync NUNCA foi chamado para FINIMP
        await contratoRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
