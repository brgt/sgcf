using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes do tenor de prazo, derivação 30/360, campos de domínio e invariantes — SPEC S40 §2, §4.
/// </summary>
[Trait("Category", "Domain")]
public sealed class CotacaoTenorTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly LocalDate DataAbertura = new(2026, 5, 15);
    private static readonly LocalDate DataPtax = new(2026, 5, 14);
    private static readonly Money ValorAlvo = new(1_000_000m, Moeda.Brl);

    // ─── Derivação 30/360 (função pura) ──────────────────────────────────────

    [Theory]
    [InlineData(60, UnidadePrazo.Meses, 1800)]
    [InlineData(24, UnidadePrazo.Meses, 720)]
    [InlineData(180, UnidadePrazo.Dias, 180)]
    [InlineData(1, UnidadePrazo.Dias, 1)]
    public void DerivarPrazoMaximoDias_aplica_30_360(int valor, UnidadePrazo unidade, int esperado)
    {
        Cotacao.DerivarPrazoMaximoDias(valor, unidade).Should().Be(esperado);
    }

    [Fact]
    public void DerivarPrazoMaximoDias_rejeita_valor_menor_que_um()
    {
        var act = () => Cotacao.DerivarPrazoMaximoDias(0, UnidadePrazo.Meses);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ─── CriarComTenor ───────────────────────────────────────────────────────

    [Fact]
    public void CriarComTenor_meses_deriva_dias_e_preserva_intencao()
    {
        Cotacao cotacao = Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 60,
            prazoMaximoUnidade: UnidadePrazo.Meses,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Eur,
            dataPtaxReferencia: DataPtax,
            ptaxUsada: 6.10m,
            clock: Clock);

        cotacao.PrazoMaximoDias.Should().Be(1800);
        cotacao.PrazoMaximoValor.Should().Be(60);
        cotacao.PrazoMaximoUnidade.Should().Be(UnidadePrazo.Meses);
    }

    [Fact]
    public void CriarComTenor_moeda_estrangeira_nao_usd_preenche_ptaxUsada_e_zera_alias_usd()
    {
        Cotacao cotacao = Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-00002",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 36,
            prazoMaximoUnidade: UnidadePrazo.Meses,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Eur,
            dataPtaxReferencia: DataPtax,
            ptaxUsada: 6.10m,
            clock: Clock);

        cotacao.MoedaAlvo.Should().Be(Moeda.Eur);
        cotacao.PtaxUsada.Should().Be(6.10m);
        cotacao.PtaxUsadaUsdBrl.Should().BeNull("ptaxUsadaUsdBrl só é preenchido quando moedaAlvo = Usd");
    }

    [Fact]
    public void CriarComTenor_usd_espelha_alias_legado()
    {
        Cotacao cotacao = Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-00003",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 180,
            prazoMaximoUnidade: UnidadePrazo.Dias,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Usd,
            dataPtaxReferencia: DataPtax,
            ptaxUsada: 5.42m,
            clock: Clock);

        cotacao.PtaxUsada.Should().Be(5.42m);
        cotacao.PtaxUsadaUsdBrl.Should().Be(5.42m);
    }

    // ─── Invariantes de moedaAlvo ────────────────────────────────────────────

    [Fact]
    public void CriarComTenor_modalidade_brl_pura_rejeita_moeda_estrangeira()
    {
        var act = () => Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-00004",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 12,
            prazoMaximoUnidade: UnidadePrazo.Meses,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Usd,
            dataPtaxReferencia: null,
            ptaxUsada: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>().WithMessage("*BRL*");
    }

    [Fact]
    public void CriarComTenor_modalidade_cambial_rejeita_brl()
    {
        var act = () => Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-00005",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 12,
            prazoMaximoUnidade: UnidadePrazo.Meses,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Brl,
            dataPtaxReferencia: DataPtax,
            ptaxUsada: 6m,
            clock: Clock);

        act.Should().Throw<ArgumentException>().WithMessage("*moeda*");
    }

    // ─── Campos de domínio ───────────────────────────────────────────────────

    [Fact]
    public void CriarComTenor_carencia_armazenada_em_modalidade_aplicavel()
    {
        Cotacao cotacao = CriarFgi(new DadosDominioCotacao(CarenciaMeses: 18, PercentualCoberturaFgi: 80m));
        cotacao.CarenciaMeses.Should().Be(18);
        cotacao.PercentualCoberturaFgi.Should().Be(80m);
    }

    [Fact]
    public void CriarComTenor_carencia_default_zero_quando_ausente_em_aplicavel()
    {
        Cotacao cotacao = CriarFgi(new DadosDominioCotacao());
        cotacao.CarenciaMeses.Should().Be(0);
    }

    [Fact]
    public void CriarComTenor_carencia_negativa_lanca()
    {
        var act = () => CriarFgi(new DadosDominioCotacao(CarenciaMeses: -1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarComTenor_cobertura_fgi_fora_da_faixa_lanca()
    {
        var act = () => CriarFgi(new DadosDominioCotacao(PercentualCoberturaFgi: 150m));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarComTenor_carencia_ignorada_em_modalidade_nao_aplicavel()
    {
        // Finimp não comporta carência — valor enviado é ignorado (null), sem lançar.
        Cotacao cotacao = Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-00006",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 180,
            prazoMaximoUnidade: UnidadePrazo.Dias,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Usd,
            dataPtaxReferencia: DataPtax,
            ptaxUsada: 5.42m,
            clock: Clock,
            dominio: new DadosDominioCotacao(CarenciaMeses: 12));

        cotacao.CarenciaMeses.Should().BeNull();
    }

    // ─── Compatibilidade legada (Criar) ──────────────────────────────────────

    [Fact]
    public void Criar_legado_infere_tenor_em_dias()
    {
        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-00007",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: 540,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: Clock);

        cotacao.PrazoMaximoDias.Should().Be(540);
        cotacao.PrazoMaximoValor.Should().Be(540);
        cotacao.PrazoMaximoUnidade.Should().Be(UnidadePrazo.Dias);
        cotacao.MoedaAlvo.Should().Be(Moeda.Brl);
    }

    // ─── EditarTenor ─────────────────────────────────────────────────────────

    [Fact]
    public void EditarTenor_recalcula_dias_canonico()
    {
        Cotacao cotacao = CriarFgi(new DadosDominioCotacao());

        cotacao.EditarTenor(24, UnidadePrazo.Meses, Clock);

        cotacao.PrazoMaximoDias.Should().Be(720);
        cotacao.PrazoMaximoValor.Should().Be(24);
        cotacao.PrazoMaximoUnidade.Should().Be(UnidadePrazo.Meses);
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private static Cotacao CriarFgi(DadosDominioCotacao dominio) =>
        Cotacao.CriarComTenor(
            codigoInterno: "COT-2026-09999",
            modalidade: ModalidadeContrato.Fgi,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoValor: 48,
            prazoMaximoUnidade: UnidadePrazo.Meses,
            dataAbertura: DataAbertura,
            moedaAlvo: Moeda.Brl,
            dataPtaxReferencia: null,
            ptaxUsada: null,
            clock: Clock,
            dominio: dominio);
}
