using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes de unidade para <see cref="CalculadoraCet.CalcularCetNce"/>.
/// NCE é operação doméstica em BRL: sem IRRF, sem IOF câmbio, IOF crédito aplicável.
/// SPEC §7 — Onda 2.
/// </summary>
public sealed class CalculadoraCetNceTests
{
    private static readonly LocalDate DataDesembolso = new(2026, 5, 15);

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Proposta CriarPropostaNce(
        decimal valorOferecido = 1_500_000m,
        decimal taxaAaPercentual = 14.5m,
        decimal iofPercentual = 0.38m,
        decimal spreadAaPercentual = 0m,
        int prazoDias = 180,
        Periodicidade periodicidade = Periodicidade.Bullet,
        bool exigeNdf = false,
        decimal? custoNdfAa = null,
        bool garantiaEhCdbCativo = false,
        decimal? rendimentoCdbAa = null,
        decimal valorGarantia = 0m,
        Moeda moeda = Moeda.Brl)
    {
        return new Proposta(
            cotacaoId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            moedaOriginal: moeda,
            valorOferecidoMoedaOriginal: new Money(valorOferecido, moeda),
            taxaAaPercentual: taxaAaPercentual,
            iofPercentual: iofPercentual,
            spreadAaPercentual: spreadAaPercentual,
            prazoDias: prazoDias,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: periodicidade,
            exigeNdf: exigeNdf,
            custoNdfAaPercentual: custoNdfAa,
            garantiaExigida: "Aval dos sócios + duplicatas de exportação",
            valorGarantiaExigidaBrl: new Money(valorGarantia, Moeda.Brl),
            garantiaEhCdbCativo: garantiaEhCdbCativo,
            rendimentoCdbAaPercentual: rendimentoCdbAa,
            dataCaptura: DataDesembolso);
    }

    // ── Testes RED → GREEN ────────────────────────────────────────────────────

    /// <summary>
    /// NCE BRL bullet 180 dias sem IOF → CET deve ser próximo à taxa nominal.
    /// Com IOF zero e sem spread, o CET anualizado TIR ≈ taxa da proposta.
    /// </summary>
    [Fact]
    public void CalcularCetNce_zero_IOF_iguala_taxa_nominal_para_Bullet_180d()
    {
        var proposta = CriarPropostaNce(
            valorOferecido: 1_000_000m,
            taxaAaPercentual: 14m,
            iofPercentual: 0m,
            spreadAaPercentual: 0m,
            prazoDias: 180,
            periodicidade: Periodicidade.Bullet);

        decimal cet = CalculadoraCet.CalcularCetNce(proposta, DataDesembolso);

        // Bullet 180d sem IOF: CET = taxa_aa (capitalização simples ≈ TIR anualizada)
        cet.Should().BeApproximately(14m, precision: 0.5m,
            because: "sem IOF e spread, CET NCE Bullet deve ser próximo à taxa nominal a.a.");
    }

    /// <summary>
    /// IOF crédito aumenta o CET em relação à mesma proposta sem IOF.
    /// NCE BRL trimestral: IOF 0,38% pago no t=0 eleva TIR.
    /// </summary>
    [Fact]
    public void CalcularCetNce_iof_credito_eleva_cet_em_relacao_a_zero_iof()
    {
        var propostaSemIof = CriarPropostaNce(iofPercentual: 0m, prazoDias: 360);
        var propostaComIof = CriarPropostaNce(iofPercentual: 0.38m, prazoDias: 360);

        decimal cetSemIof = CalculadoraCet.CalcularCetNce(propostaSemIof, DataDesembolso);
        decimal cetComIof = CalculadoraCet.CalcularCetNce(propostaComIof, DataDesembolso);

        cetComIof.Should().BeGreaterThan(cetSemIof,
            because: "IOF crédito é custo em t=0 que eleva o CET (SPEC §7.3)");
    }

    /// <summary>
    /// Quando EstruturaAmortizacao = Bullet, ProjetarFluxo ignora PeriodicidadeJuros
    /// e gera um único evento no vencimento. Portanto duas propostas NCE Bullet com
    /// periodicidades distintas (Bullet vs. Trimestral) produzem CET idêntico.
    /// Testa que o comportamento é estável e não lança exceção.
    /// </summary>
    [Fact]
    public void CalcularCetNce_estrutura_bullet_ignora_periodicidade_juros_e_produz_cet_igual()
    {
        var propostaBullet = CriarPropostaNce(
            taxaAaPercentual: 12m,
            iofPercentual: 0.38m,
            prazoDias: 360,
            periodicidade: Periodicidade.Bullet);

        var propostaTrimestral = CriarPropostaNce(
            taxaAaPercentual: 12m,
            iofPercentual: 0.38m,
            prazoDias: 360,
            periodicidade: Periodicidade.Trimestral);

        decimal cetBullet = CalculadoraCet.CalcularCetNce(propostaBullet, DataDesembolso);
        decimal cetTrimestral = CalculadoraCet.CalcularCetNce(propostaTrimestral, DataDesembolso);

        // Com EstruturaAmortizacao.Bullet, ProjetarFluxo força Periodicidade.Bullet
        // independente de PeriodicidadeJuros — ambos produzem fluxo idêntico.
        cetTrimestral.Should().Be(cetBullet,
            because: "EstruturaAmortizacao.Bullet gera único evento no vencimento; PeriodicidadeJuros não altera o fluxo");

        cetBullet.Should().BeGreaterThan(0m,
            because: "CET NCE Bullet BRL 12% a.a. com IOF deve ser positivo");
    }

    /// <summary>
    /// SPEC §7.1: proposta com MoedaOriginal != BRL deve ser rejeitada.
    /// </summary>
    [Fact]
    public void CalcularCetNce_rejeita_MoedaOriginal_USD()
    {
        var propostaUsd = CriarPropostaNce(moeda: Moeda.Usd);

        var act = () => CalculadoraCet.CalcularCetNce(propostaUsd, DataDesembolso);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*MoedaOriginal*Brl*");
    }

    /// <summary>
    /// SPEC §7.1: proposta com ExigeNdf=true deve ser rejeitada.
    /// NCE não tem hedge cambial (operação em BRL).
    /// </summary>
    [Fact]
    public void CalcularCetNce_rejeita_ExigeNdf_true()
    {
        // ExigeNdf=true requer CustoNdfAa para satisfazer invariante de Proposta
        var propostaComNdf = CriarPropostaNce(exigeNdf: true, custoNdfAa: 1.5m);

        var act = () => CalculadoraCet.CalcularCetNce(propostaComNdf, DataDesembolso);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ExigeNdf*");
    }

    /// <summary>
    /// CET NCE deve retornar valor positivo para inputs válidos típicos.
    /// SPEC §7.3 — cenário golden indicativo ≈ 12,50%-13,20% para taxa 12%, IOF 0,38%, 360d.
    /// </summary>
    [Fact]
    public void CalcularCetNce_proposta_tipica_brl_trimestral_retorna_cet_positivo_plausivel()
    {
        var proposta = CriarPropostaNce(
            valorOferecido: 5_000_000m,
            taxaAaPercentual: 12m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 360,
            periodicidade: Periodicidade.Trimestral);

        decimal cet = CalculadoraCet.CalcularCetNce(proposta, DataDesembolso);

        // CET deve estar na faixa indicativa da SPEC §7.4
        cet.Should().BeInRange(12m, 14m,
            because: "CET NCE BRL 12% a.a. IOF 0,38% 360d trimestral deve cair na faixa 12%-14%");
    }

    /// <summary>
    /// Fachada <see cref="CalculadoraCet.CalcularCet"/> com ptax=null deve despachar para CalcularCetNce
    /// para proposta NCE BRL, e não mais lançar NotImplementedException.
    /// </summary>
    [Fact]
    public void CalcularCet_fachada_com_ptax_null_para_proposta_brl_dispacheia_para_CalcularCetNce()
    {
        var proposta = CriarPropostaNce(
            taxaAaPercentual: 14.5m,
            iofPercentual: 0.38m,
            prazoDias: 180);

        decimal? ptaxNull = null;
        decimal cet = CalculadoraCet.CalcularCet(proposta, ptaxNull, DataDesembolso);

        cet.Should().BeGreaterThan(0m,
            because: "fachada com ptax null + moeda BRL deve usar CalcularCetNce e retornar CET positivo");
    }

    /// <summary>
    /// SPEC §7.1 override: taxaAaPercentualOverride substitui taxa da proposta.
    /// </summary>
    [Fact]
    public void CalcularCetNce_com_taxaAaOverride_usa_taxa_override()
    {
        var proposta = CriarPropostaNce(taxaAaPercentual: 14.5m, prazoDias: 360);

        decimal cetTaxaOriginal = CalculadoraCet.CalcularCetNce(proposta, DataDesembolso);
        decimal cetTaxaMenor = CalculadoraCet.CalcularCetNce(proposta, DataDesembolso, taxaAaPercentualOverride: 12m);

        cetTaxaMenor.Should().BeLessThan(cetTaxaOriginal,
            because: "override com taxa menor deve produzir CET menor");
    }
}
