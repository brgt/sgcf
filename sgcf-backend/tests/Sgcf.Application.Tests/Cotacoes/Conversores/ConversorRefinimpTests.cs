using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Conversores;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Conversores;

/// <summary>
/// Testes unitários do ConversorRefinimp (implementação real — Onda 1).
/// SPEC §6.1, §8.4, regra 70% BB.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorRefinimpTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 6, 1, 12, 0);
    private static readonly Guid ContratoMaeId = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    private static Banco CriarBancoBB(IClock clock) =>
        Banco.Criar("001", "Banco do Brasil S.A.", "BB", PadraoAntecipacao.A, clock);

    private static Banco CriarBancoItau(IClock clock) =>
        Banco.Criar("341", "Itaú Unibanco S.A.", "Itaú", PadraoAntecipacao.A, clock);

    private static Contrato CriarContratoAncestralUsd(IClock clock, decimal valorUsd = 1_000_000m) =>
        Contrato.Criar(
            numeroExterno: "FIN-2026-0001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(valorUsd, Moeda.Usd),
            dataContratacao: new LocalDate(2025, 12, 1),
            dataVencimento: new LocalDate(2026, 12, 1),
            taxaAa: Percentual.De(6.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

    private static ConverterEmContratoContext CriarContexto(
        IClock clock,
        Guid bancoId,
        decimal valorPrincipalUsd,
        Guid contratoMaeId)
    {
        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-R0001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(2_600_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 6, 1),
            dataPtaxReferencia: new LocalDate(2026, 5, 31),
            ptaxUsadaUsdBrl: 5.20m,
            clock: clock,
            contratoMaeId: contratoMaeId);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: bancoId,
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(valorPrincipalUsd, Moeda.Usd),
            taxaAaPercentual: 5.20m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.50m,
            prazoDias: 180,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: new LocalDate(2026, 6, 1));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        Contrato contratoCriado = Contrato.Criar(
            numeroExterno: "REFI-BB-2026-0001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Refinimp,
            valorPrincipal: new Money(valorPrincipalUsd, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 6, 10),
            dataVencimento: new LocalDate(2026, 12, 10),
            taxaAa: Percentual.De(5.20m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        ConverterEmContratoCommand command = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "REFI-BB-2026-0001",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 6, 10),
            DataVencimento: new DateOnly(2026, 12, 10),
            TaxaAa: 5.20m,
            Refinimp: new RefinimpInputs(PercentualRefinanciado: valorPrincipalUsd / 1_000_000m));

        return new ConverterEmContratoContext(cotacao, proposta, contratoCriado, command, clock);
    }

    // ── Regra 70% BB ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "BB valor exatamente 70% do ancestral → sucesso")]
    public async Task CriarDetail_BB_valor_igual_70pct_ancestral_sucesso()
    {
        IClock clock = CriarClock();
        Banco bb = CriarBancoBB(clock);
        Contrato mae = CriarContratoAncestralUsd(clock, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);
        contratoRepo.GetAncestraNaoRefinimpAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(bb.Id, default).Returns(bb);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);
        ConverterEmContratoContext ctx = CriarContexto(clock, bb.Id, 700_000m, ContratoMaeId);

        // Act — 70% exato deve passar
        (Entity detail, Entity? sec) = await conversor.CriarDetailAsync(ctx, default);

        detail.Should().BeOfType<RefinimpDetail>();
        sec.Should().BeNull();
    }

    [Fact(DisplayName = "BB valor acima de 70% do ancestral → InvalidOperationException")]
    public async Task CriarDetail_BB_valor_acima_70pct_ancestral_rejeita_InvalidOperation()
    {
        IClock clock = CriarClock();
        Banco bb = CriarBancoBB(clock);
        Contrato mae = CriarContratoAncestralUsd(clock, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);
        contratoRepo.GetAncestraNaoRefinimpAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(bb.Id, default).Returns(bb);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);
        // 700_001 > 700_000 (70% de 1M)
        ConverterEmContratoContext ctx = CriarContexto(clock, bb.Id, 700_001m, ContratoMaeId);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Banco do Brasil*70%*");
    }

    [Fact(DisplayName = "Banco não-BB acima de 70% do ancestral → sucesso (sem restrição)")]
    public async Task CriarDetail_BancoNaoBB_acima_70pct_sucesso()
    {
        IClock clock = CriarClock();
        Banco itau = CriarBancoItau(clock);
        Contrato mae = CriarContratoAncestralUsd(clock, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);
        contratoRepo.GetAncestraNaoRefinimpAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(itau.Id, default).Returns(itau);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);
        // Itaú não tem restrição de 70%
        ConverterEmContratoContext ctx = CriarContexto(clock, itau.Id, 900_000m, ContratoMaeId);

        (Entity detail, _) = await conversor.CriarDetailAsync(ctx, default);

        detail.Should().BeOfType<RefinimpDetail>();
    }

    // ── Marcação do mãe imediato ─────────────────────────────────────────────

    [Fact(DisplayName = "Percentual >= 100% do ancestral → mãe marcado RefinanciadoTotal")]
    public async Task CriarDetail_percentual_100pct_marca_mae_RefinanciadoTotal()
    {
        IClock clock = CriarClock();
        Banco itau = CriarBancoItau(clock);
        Contrato mae = CriarContratoAncestralUsd(clock, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);
        contratoRepo.GetAncestraNaoRefinimpAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(itau.Id, default).Returns(itau);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);
        ConverterEmContratoContext ctx = CriarContexto(clock, itau.Id, 1_000_000m, ContratoMaeId);

        await conversor.CriarDetailAsync(ctx, default);

        mae.Status.Should().Be(StatusContrato.RefinanciadoTotal);
    }

    [Fact(DisplayName = "Percentual < 100% do ancestral → mãe marcado RefinanciadoParcial")]
    public async Task CriarDetail_percentual_50pct_marca_mae_RefinanciadoParcial()
    {
        IClock clock = CriarClock();
        Banco itau = CriarBancoItau(clock);
        Contrato mae = CriarContratoAncestralUsd(clock, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);
        contratoRepo.GetAncestraNaoRefinimpAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(itau.Id, default).Returns(itau);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);
        ConverterEmContratoContext ctx = CriarContexto(clock, itau.Id, 500_000m, ContratoMaeId);

        await conversor.CriarDetailAsync(ctx, default);

        mae.Status.Should().Be(StatusContrato.RefinanciadoParcial);
    }

    // ── Cadeia recursiva ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Cadeia de 3 níveis: BB usa ancestral FINIMP original para regra 70%")]
    public async Task CriarDetail_cadeia_3_niveis_usa_ancestral_correto()
    {
        IClock clock = CriarClock();
        Banco bb = CriarBancoBB(clock);

        // Cadeia: ancestral FINIMP 1M → mãe REFINIMP intermediário → este REFINIMP
        Contrato ancestral = CriarContratoAncestralUsd(clock, 1_000_000m);
        Contrato maeIntermediario = CriarContratoAncestralUsd(clock, 700_000m); // próprio REFINIMP

        Guid maeId = Guid.NewGuid();

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(maeId, default).Returns(maeIntermediario);
        // GetAncestraNaoRefinimpAsync navega até o FINIMP original
        contratoRepo.GetAncestraNaoRefinimpAsync(maeId, default).Returns(ancestral);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(bb.Id, default).Returns(bb);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-R0002",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(3_640_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 6, 1),
            dataPtaxReferencia: new LocalDate(2026, 5, 31),
            ptaxUsadaUsdBrl: 5.20m,
            clock: clock,
            contratoMaeId: maeId);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: bb.Id,
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(700_000m, Moeda.Usd),
            taxaAaPercentual: 5.20m, iofPercentual: 0.38m, spreadAaPercentual: 0.50m,
            prazoDias: 180, estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet, exigeNdf: false,
            custoNdfAaPercentual: null, garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false, rendimentoCdbAaPercentual: null,
            dataCaptura: new LocalDate(2026, 6, 1));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        Contrato contratoCriado = Contrato.Criar(
            numeroExterno: "REFI-BB-2026-0002",
            bancoId: bb.Id,
            modalidade: ModalidadeContrato.Refinimp,
            valorPrincipal: new Money(700_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 6, 10),
            dataVencimento: new LocalDate(2026, 12, 10),
            taxaAa: Percentual.De(5.20m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        ConverterEmContratoCommand command = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "REFI-BB-2026-0002",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 6, 10),
            DataVencimento: new DateOnly(2026, 12, 10),
            TaxaAa: 5.20m,
            Refinimp: new RefinimpInputs(0.70m));

        ConverterEmContratoContext ctx = new(cotacao, proposta, contratoCriado, command, clock);

        // 700K de ancestral 1M = 70% → exatamente no limite → deve passar
        (Entity detail, _) = await conversor.CriarDetailAsync(ctx, default);

        detail.Should().BeOfType<RefinimpDetail>();
        ((RefinimpDetail)detail).PercentualRefinanciado.AsDecimal.Should().BeApproximately(0.70m, 0.001m);
    }

    // ── Defesa moeda divergente ───────────────────────────────────────────────

    [Fact(DisplayName = "Moeda do contrato criado diverge do mãe → InvalidOperationException (defesa)")]
    public async Task CriarDetail_moeda_divergente_lanca_InvalidOperation()
    {
        IClock clock = CriarClock();
        Banco itau = CriarBancoItau(clock);

        // Mãe em USD; contrato criado em EUR (cenário não deveria acontecer com validação prévia)
        Contrato mae = CriarContratoAncestralUsd(clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(itau.Id, default).Returns(itau);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-R0003",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 6, 1),
            dataPtaxReferencia: new LocalDate(2026, 5, 31),
            ptaxUsadaUsdBrl: 5.20m,
            clock: clock,
            contratoMaeId: ContratoMaeId);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: itau.Id,
            moedaOriginal: Moeda.Eur,  // EUR enquanto mãe é USD
            valorOferecidoMoedaOriginal: new Money(500_000m, Moeda.Eur),
            taxaAaPercentual: 4.5m, iofPercentual: 0.38m, spreadAaPercentual: 0.50m,
            prazoDias: 180, estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet, exigeNdf: false,
            custoNdfAaPercentual: null, garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false, rendimentoCdbAaPercentual: null,
            dataCaptura: new LocalDate(2026, 6, 1));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        // Cria contrato em EUR (proposta aceita)
        Contrato contratoCriado = Contrato.Criar(
            numeroExterno: "REFI-ITA-2026-0003",
            bancoId: itau.Id,
            modalidade: ModalidadeContrato.Refinimp,
            valorPrincipal: new Money(500_000m, Moeda.Eur),
            dataContratacao: new LocalDate(2026, 6, 10),
            dataVencimento: new LocalDate(2026, 12, 10),
            taxaAa: Percentual.De(4.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        ConverterEmContratoCommand command = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "REFI-ITA-2026-0003",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 6, 10),
            DataVencimento: new DateOnly(2026, 12, 10),
            TaxaAa: 4.5m,
            Refinimp: new RefinimpInputs(0.5m));

        ConverterEmContratoContext ctx = new(cotacao, proposta, contratoCriado, command, clock);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*moeda*contrato mãe*");
    }

    // ── Retorno secundário ────────────────────────────────────────────────────

    [Fact(DisplayName = "Retorno secundário é sempre null para REFINIMP")]
    public async Task CriarDetail_retorno_Secundario_eh_null()
    {
        IClock clock = CriarClock();
        Banco itau = CriarBancoItau(clock);
        Contrato mae = CriarContratoAncestralUsd(clock);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.GetByIdAsync(ContratoMaeId, default).Returns(mae);
        contratoRepo.GetAncestraNaoRefinimpAsync(ContratoMaeId, default).Returns(mae);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.GetByIdAsync(itau.Id, default).Returns(itau);

        ConversorRefinimp conversor = new(contratoRepo, bancoRepo);
        ConverterEmContratoContext ctx = CriarContexto(clock, itau.Id, 500_000m, ContratoMaeId);

        (_, Entity? sec) = await conversor.CriarDetailAsync(ctx, default);

        sec.Should().BeNull();
    }
}
