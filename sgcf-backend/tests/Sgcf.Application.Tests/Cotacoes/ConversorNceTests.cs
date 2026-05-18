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
/// Testes de unidade para <see cref="ConversorNce"/>.
/// Verifica que o conversor cria <see cref="NceDetail"/> corretamente a partir do contexto.
/// SPEC §6 — Onda 2.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorNceTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 6, 5, 12, 0);
    private static readonly LocalDate DataAbertura = new(2026, 6, 5);

    private static IClock CriarClock() =>
        Substitute.For<IClock>().WithCurrentInstant(AgentInstant);

    private static ConverterEmContratoContext CriarContexto(
        string? nceNumero = "NCE-BB-2026-00045",
        DateOnly? dataEmissao = null,
        string? bancoMandatario = "Banco do Brasil S.A.",
        NceInputs? nce = null)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-NCE-001",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: new Money(5_000_000m, Moeda.Brl),
            prazoMaximoDias: 360,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: clock);

        // Transição Rascunho → EmCaptacao: necessária antes de AdicionarProposta.
        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Brl,
            valorOferecidoMoedaOriginal: new Money(5_000_000m, Moeda.Brl),
            taxaAaPercentual: 12m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 360,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Trimestral,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Aval dos sócios",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataAbertura);

        // Simular contrato criado (usado apenas como referência de ID)
        Guid contratoId = Guid.NewGuid();
        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-0001",
            bancoId: proposta.BancoId,
            modalidade: ModalidadeContrato.Nce,
            valorPrincipal: new Money(5_000_000m, Moeda.Brl),
            dataContratacao: DataAbertura,
            dataVencimento: DataAbertura.PlusDays(360),
            taxaAa: Percentual.De(12m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: Periodicidade.Trimestral,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: DataAbertura.PlusDays(360),
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            periodicidadeJuros: Periodicidade.Trimestral,
            convencaoDataNaoUtil: ConvencaoDataNaoUtil.Following);

        DateOnly dataEmissaoValue = dataEmissao ?? new DateOnly(2026, 6, 5);
        NceInputs nceInputs = nce ?? new NceInputs(nceNumero, dataEmissaoValue, bancoMandatario);

        ConverterEmContratoCommand cmd = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "NCE-BB-2026-00045",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 6, 5),
            DataVencimento: new DateOnly(2027, 6, 5),
            TaxaAa: 12m,
            Observacoes: null,
            Nce: nceInputs);

        return new ConverterEmContratoContext(cotacao, proposta, contrato, cmd, clock);
    }

    // ── Testes ────────────────────────────────────────────────────────────────

    [Fact]
    public void Modalidade_deve_ser_Nce()
    {
        var conversor = new ConversorNce();
        conversor.Modalidade.Should().Be(ModalidadeContrato.Nce);
    }

    [Fact]
    public async Task CriarDetailAsync_com_campos_completos_retorna_NceDetail_populado()
    {
        var conversor = new ConversorNce();
        var ctx = CriarContexto(
            nceNumero: "NCE-BB-2026-00045",
            dataEmissao: new DateOnly(2026, 6, 5),
            bancoMandatario: "Banco do Brasil S.A.");

        (Entity principal, Entity? secundario) = await conversor.CriarDetailAsync(ctx, default);

        principal.Should().BeOfType<NceDetail>();
        NceDetail detail = (NceDetail)principal;
        detail.NceNumero.Should().Be("NCE-BB-2026-00045");
        detail.DataEmissao.Should().Be(new LocalDate(2026, 6, 5));
        detail.BancoMandatario.Should().Be("Banco do Brasil S.A.");
        detail.ContratoId.Should().Be(ctx.ContratoCriado.Id);
    }

    [Fact]
    public async Task CriarDetailAsync_com_campos_nulos_retorna_NceDetail_com_todos_null()
    {
        var conversor = new ConversorNce();
        var ctx = CriarContexto(
            nceNumero: null,
            dataEmissao: null,
            bancoMandatario: null,
            nce: new NceInputs(null, null, null));

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, default);

        NceDetail detail = (NceDetail)principal;
        detail.NceNumero.Should().BeNull();
        detail.DataEmissao.Should().BeNull();
        detail.BancoMandatario.Should().BeNull();
    }

    [Fact]
    public async Task Secundario_eh_sempre_null()
    {
        var conversor = new ConversorNce();
        var ctx = CriarContexto();

        (_, Entity? secundario) = await conversor.CriarDetailAsync(ctx, default);

        secundario.Should().BeNull(
            because: "NCE é modalidade simples — retorna apenas NceDetail sem detail secundário");
    }

    [Fact]
    public async Task CriarDetailAsync_CreatedAt_e_UpdatedAt_sao_preenchidos()
    {
        var conversor = new ConversorNce();
        var ctx = CriarContexto();

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, default);

        NceDetail detail = (NceDetail)principal;
        detail.CreatedAt.Should().Be(AgentInstant);
        detail.UpdatedAt.Should().Be(AgentInstant);
    }
}

/// <summary>Extension auxiliar para configurar IClock via NSubstitute de forma fluente.</summary>
internal static class ClockSubstituteExtensions
{
    internal static IClock WithCurrentInstant(this IClock clock, Instant instant)
    {
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }
}
