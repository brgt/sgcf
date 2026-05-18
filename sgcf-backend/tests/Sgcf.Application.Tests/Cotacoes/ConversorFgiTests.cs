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
/// Testes de unidade para <see cref="ConversorFgi"/>.
/// Verifica que o conversor cria <see cref="FgiDetail"/> corretamente a partir do contexto,
/// valida inputs obrigatórios e rejeita combinações inválidas.
/// SPEC §6 — Onda 3a (docs/specs/cotacoes/modalidades/fgi.md §6.1).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorFgiTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 12, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 18);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    private static ConverterEmContratoContext CriarContexto(
        decimal? taxaFgiAaPercentual = 0.5m,
        decimal? percentualCoberto = 80m,
        string? numeroOperacaoFgi = "FGI-2026-CAIXA-001234")
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-FGI-001",
            modalidade: ModalidadeContrato.Fgi,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 365,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,  // FGI é BRL puro — sem PTAX
            ptaxUsadaUsdBrl: null,
            clock: clock);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Brl,
            valorOferecidoMoedaOriginal: new Money(500_000m, Moeda.Brl),
            taxaAaPercentual: 12m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 365,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Cobertura FGI 80%",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataAbertura);

        Contrato contrato = Contrato.Criar(
            numeroExterno: "FGI-2026-001",
            bancoId: proposta.BancoId,
            modalidade: ModalidadeContrato.Fgi,
            valorPrincipal: new Money(500_000m, Moeda.Brl),
            dataContratacao: DataAbertura,
            dataVencimento: DataAbertura.PlusDays(365),
            taxaAa: Percentual.De(12m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: Periodicidade.Bullet,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: DataAbertura.PlusDays(365),
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            periodicidadeJuros: Periodicidade.Bullet,
            convencaoDataNaoUtil: ConvencaoDataNaoUtil.Following);

        FgiInputs? fgiInputs = taxaFgiAaPercentual.HasValue
            ? new FgiInputs(taxaFgiAaPercentual.Value, percentualCoberto)
            : null;

        ConverterEmContratoCommand cmd = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "FGI-2026-001",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 5, 18),
            DataVencimento: new DateOnly(2027, 5, 18),
            TaxaAa: 12m,
            Observacoes: null,
            NumeroOperacaoFgi: numeroOperacaoFgi,
            Fgi: fgiInputs);

        return new ConverterEmContratoContext(cotacao, proposta, contrato, cmd, clock);
    }

    // ── Testes ────────────────────────────────────────────────────────────────

    [Fact]
    public void Modalidade_deve_ser_Fgi()
    {
        var conversor = new ConversorFgi();
        conversor.Modalidade.Should().Be(ModalidadeContrato.Fgi);
    }

    [Fact]
    public async Task CriarDetailAsync_com_inputs_completos_retorna_FgiDetail_populado()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto(
            taxaFgiAaPercentual: 0.5m,
            percentualCoberto: 80m,
            numeroOperacaoFgi: "FGI-2026-CAIXA-001234");

        (Entity principal, Entity? secundario) = await conversor.CriarDetailAsync(ctx, default);

        principal.Should().BeOfType<FgiDetail>();
        FgiDetail detail = (FgiDetail)principal;
        detail.ContratoId.Should().Be(ctx.ContratoCriado.Id);
        detail.NumeroOperacaoFgi.Should().Be("FGI-2026-CAIXA-001234");

        // TaxaFgiAa: 0.5% → fração 0.005
        detail.TaxaFgiAa.Should().NotBeNull();
        detail.TaxaFgiAa!.Value.AsDecimal.Should().BeApproximately(0.005m, 0.0001m,
            because: "TaxaFgiAaPercentual 0.5 deve converter para fração 0.005 internamente");

        // PercentualCoberto: 80% → fração 0.80
        detail.PercentualCoberto.Should().NotBeNull();
        detail.PercentualCoberto!.Value.AsDecimal.Should().BeApproximately(0.8m, 0.001m,
            because: "PercentualCoberto 80 deve converter para fração 0.8 internamente");
    }

    [Fact]
    public async Task CriarDetailAsync_sem_TaxaFgiAa_lanca_InvalidOperationException()
    {
        var conversor = new ConversorFgi();
        // FgiInputs null → TaxaFgiAaPercentual ausente
        var ctx = CriarContexto(taxaFgiAaPercentual: null, percentualCoberto: null);

        var act = async () => await conversor.CriarDetailAsync(ctx, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TaxaFgiAaPercentual*",
                because: "TaxaFgiAaPercentual é obrigatória para modalidade FGI (SPEC §5.3)");
    }

    [Fact]
    public async Task CriarDetailAsync_com_TaxaFgiAa_zero_lanca_InvalidOperationException()
    {
        var conversor = new ConversorFgi();
        // FgiInputs com taxa = 0 deve ser rejeitado
        var ctx = CriarContexto(taxaFgiAaPercentual: 0m, percentualCoberto: 80m);

        var act = async () => await conversor.CriarDetailAsync(ctx, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TaxaFgiAaPercentual*",
                because: "taxa FGI zero implica ausência de FGI — deve ser rejeitada (EC-13)");
    }

    [Fact]
    public async Task CriarDetailAsync_com_PercentualCoberto_acima_de_100_lanca()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto(taxaFgiAaPercentual: 0.5m, percentualCoberto: 101m);

        var act = async () => await conversor.CriarDetailAsync(ctx, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PercentualCoberto*100*",
                because: "cobertura > 100% é fisicamente impossível (EC-3)");
    }

    [Fact]
    public async Task CriarDetailAsync_com_PercentualCoberto_zero_lanca()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto(taxaFgiAaPercentual: 0.5m, percentualCoberto: 0m);

        var act = async () => await conversor.CriarDetailAsync(ctx, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PercentualCoberto*",
                because: "cobertura zero não faz sentido — usar null se indefinido (EC-4)");
    }

    [Fact]
    public async Task CriarDetailAsync_sem_PercentualCoberto_persiste_null()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto(taxaFgiAaPercentual: 0.5m, percentualCoberto: null);

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, default);

        FgiDetail detail = (FgiDetail)principal;
        detail.PercentualCoberto.Should().BeNull(
            because: "PercentualCoberto null é permitido — cobertura pode ser indefinida (EC-5)");
    }

    [Fact]
    public async Task Secundario_eh_sempre_null()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto();

        (_, Entity? secundario) = await conversor.CriarDetailAsync(ctx, default);

        secundario.Should().BeNull(
            because: "FGI-modalidade retorna apenas FgiDetail sem detail secundário (SPEC §6.1)");
    }

    [Fact]
    public async Task CriarDetailAsync_sem_NumeroOperacaoFgi_persiste_null()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto(taxaFgiAaPercentual: 0.5m, numeroOperacaoFgi: null);

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, default);

        FgiDetail detail = (FgiDetail)principal;
        detail.NumeroOperacaoFgi.Should().BeNull(
            because: "NumeroOperacaoFgi é opcional — banco pode não ter repassado o número ainda");
    }

    [Fact]
    public async Task CriarDetailAsync_CreatedAt_e_UpdatedAt_sao_preenchidos()
    {
        var conversor = new ConversorFgi();
        var ctx = CriarContexto();

        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, default);

        FgiDetail detail = (FgiDetail)principal;
        detail.CreatedAt.Should().Be(AgentInstant);
        detail.UpdatedAt.Should().Be(AgentInstant);
    }
}
