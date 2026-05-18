using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Conversores;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes de unidade para <see cref="ConversorCapitalDeGiro"/> — Onda 3b.
/// SPEC §6: conversor cria CapitalDeGiroDetail e retorna (detail, null).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorCapitalDeGiroTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 10, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 18);

    private static ConverterEmContratoContext CriarContexto(string? numeroOperacao = "OP-7788-1234")
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-CDG-001",
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorAlvoBrl: new Money(1_500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: clock);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Brl,
            valorOferecidoMoedaOriginal: new Money(1_500_000m, Moeda.Brl),
            taxaAaPercentual: 14.5m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 180,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Mensal,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Aval dos sócios",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataAbertura);

        Contrato contrato = Contrato.Criar(
            numeroExterno: "CG-2026-0001",
            bancoId: proposta.BancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(1_500_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2026, 6, 5),
            dataVencimento: new LocalDate(2026, 12, 5),
            taxaAa: Percentual.De(14.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: Periodicidade.Mensal,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2026, 12, 5),
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            periodicidadeJuros: Periodicidade.Mensal,
            convencaoDataNaoUtil: ConvencaoDataNaoUtil.Following);

        CapitalDeGiroInputs? inputs = numeroOperacao is null ? null : new CapitalDeGiroInputs(numeroOperacao);

        ConverterEmContratoCommand cmd = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "CG-2026-0001",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 6, 5),
            DataVencimento: new DateOnly(2026, 12, 5),
            TaxaAa: 14.5m,
            CapitalDeGiro: inputs);

        return new ConverterEmContratoContext(cotacao, proposta, contrato, cmd, clock);
    }

    // ── Testes principais ─────────────────────────────────────────────────────

    /// <summary>
    /// Modalidade do conversor deve ser CapitalDeGiro.
    /// </summary>
    [Fact]
    public void Modalidade_e_CapitalDeGiro()
    {
        var conversor = new ConversorCapitalDeGiro();
        conversor.Modalidade.Should().Be(ModalidadeContrato.CapitalDeGiro);
    }

    /// <summary>
    /// CriarDetailAsync deve retornar (CapitalDeGiroDetail, null) — sem detail secundário.
    /// SPEC §6: ConversorCapitalDeGiro nunca cria FgiDetail.
    /// </summary>
    [Fact]
    public async Task CriarDetailAsync_retorna_CapitalDeGiroDetail_e_null_secundario()
    {
        var conversor = new ConversorCapitalDeGiro();
        ConverterEmContratoContext ctx = CriarContexto();

        (Entity principal, Entity? secundario) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        principal.Should().BeOfType<CapitalDeGiroDetail>();
        secundario.Should().BeNull(because: "CapitalDeGiro nunca cria detail secundário (SPEC §6)");
    }

    /// <summary>
    /// NumeroOperacao do CapitalDeGiroDetail deve vir do CapitalDeGiroInputs do command.
    /// </summary>
    [Fact]
    public async Task CriarDetailAsync_propaga_NumeroOperacao_do_command()
    {
        const string NumeroOp = "OP-7788-9999";
        var conversor = new ConversorCapitalDeGiro();
        ConverterEmContratoContext ctx = CriarContexto(numeroOperacao: NumeroOp);

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        var detail = (CapitalDeGiroDetail)principal;
        detail.NumeroOperacao.Should().Be(NumeroOp);
    }

    /// <summary>
    /// NumeroOperacao null (CapitalDeGiroInputs ausente) é aceito — SPEC EC-10.
    /// </summary>
    [Fact]
    public async Task CriarDetailAsync_sem_CapitalDeGiroInputs_propaga_NumeroOperacao_null()
    {
        var conversor = new ConversorCapitalDeGiro();
        ConverterEmContratoContext ctx = CriarContexto(numeroOperacao: null);

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        var detail = (CapitalDeGiroDetail)principal;
        detail.NumeroOperacao.Should().BeNull(
            because: "CapitalDeGiroInputs ausente → NumeroOperacao null (SPEC EC-10)");
    }

    /// <summary>
    /// ContratoId do detail deve ser o Id do contrato criado pelo handler.
    /// </summary>
    [Fact]
    public async Task CriarDetailAsync_detalhe_tem_contratoId_correto()
    {
        var conversor = new ConversorCapitalDeGiro();
        ConverterEmContratoContext ctx = CriarContexto();

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        var detail = (CapitalDeGiroDetail)principal;
        detail.ContratoId.Should().Be(ctx.ContratoCriado.Id);
    }

    /// <summary>
    /// CreatedAt e UpdatedAt são preenchidos via clock.
    /// </summary>
    [Fact]
    public async Task CriarDetailAsync_timestamps_sao_preenchidos()
    {
        var conversor = new ConversorCapitalDeGiro();
        ConverterEmContratoContext ctx = CriarContexto();

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        var detail = (CapitalDeGiroDetail)principal;
        detail.CreatedAt.Should().Be(AgentInstant);
        detail.UpdatedAt.Should().Be(AgentInstant);
    }
}
