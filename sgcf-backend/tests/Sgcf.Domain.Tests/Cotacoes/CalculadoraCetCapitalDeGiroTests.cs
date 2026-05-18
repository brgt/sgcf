using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes de unidade para <see cref="CalculadoraCet.CalcularCetCapitalDeGiro"/>.
/// Capital de Giro é operação BRL pura: sem PTAX, sem NDF, sem IRRF. Base 360 dias.
/// SPEC §7 — Onda 3b.
/// </summary>
public sealed class CalculadoraCetCapitalDeGiroTests
{
    private static readonly LocalDate DataDesembolso = new(2026, 5, 18);

    private static Proposta CriarProposta(
        decimal valorOferecido = 1_500_000m,
        decimal taxaAaPercentual = 14.5m,
        decimal iofPercentual = 0.38m,
        decimal spreadAaPercentual = 0m,
        int prazoDias = 180,
        Periodicidade periodicidade = Periodicidade.Bullet,
        bool exigeNdf = false,
        decimal? custoNdfAa = null,
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
            garantiaExigida: "Aval dos sócios",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataDesembolso);
    }

    // ── Testes principais ─────────────────────────────────────────────────────

    /// <summary>
    /// Golden case da SPEC §7: Capital de Giro BRL 1,5M, 180 dias Bullet,
    /// taxa 14,5% a.a., IOF 0,38%, Mensal — CET deve ser maior que taxa nominal.
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_golden_brl_180d_bullet_iof038_retorna_cet_plausivel()
    {
        var proposta = CriarProposta(
            valorOferecido: 1_500_000m,
            taxaAaPercentual: 14.5m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 180,
            periodicidade: Periodicidade.Bullet);

        decimal cet = CalculadoraCet.CalcularCetCapitalDeGiro(proposta, DataDesembolso);

        // CET deve ser maior que taxa nominal (IOF eleva custo efetivo)
        cet.Should().BeGreaterThan(14.5m,
            because: "IOF 0,38% em t=0 eleva o CET acima da taxa nominal");

        // CET deve ser razoável — não mais que 20% para esses inputs
        cet.Should().BeLessThan(20m,
            because: "CET Capital de Giro BRL com taxa 14,5% e IOF 0,38% deve estar abaixo de 20%");
    }

    /// <summary>
    /// IOF crédito zero: CET deve ser próximo à taxa nominal (apenas juros no fluxo).
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_iof_zero_cet_proximo_taxa_nominal()
    {
        var proposta = CriarProposta(
            taxaAaPercentual: 14m,
            iofPercentual: 0m,
            prazoDias: 180,
            periodicidade: Periodicidade.Bullet);

        decimal cet = CalculadoraCet.CalcularCetCapitalDeGiro(proposta, DataDesembolso);

        cet.Should().BeApproximately(14m, precision: 0.5m,
            because: "sem IOF, CET Bullet BRL deve ser próximo à taxa nominal");
    }

    /// <summary>
    /// IOF eleva o CET — capital de giro com IOF deve ter CET maior que sem IOF.
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_iof_credito_eleva_cet()
    {
        var propostaSemIof = CriarProposta(iofPercentual: 0m, prazoDias: 360);
        var propostaComIof = CriarProposta(iofPercentual: 0.38m, prazoDias: 360);

        decimal cetSemIof = CalculadoraCet.CalcularCetCapitalDeGiro(propostaSemIof, DataDesembolso);
        decimal cetComIof = CalculadoraCet.CalcularCetCapitalDeGiro(propostaComIof, DataDesembolso);

        cetComIof.Should().BeGreaterThan(cetSemIof,
            because: "IOF crédito em t=0 eleva o CET (SPEC §7.3)");
    }

    /// <summary>
    /// PTAX null: CalcularCet (fachada) com ptax=null e moeda BRL deve chamar CalcularCetCapitalDeGiro
    /// para proposta Capital de Giro, não lançar NotImplementedException.
    /// </summary>
    [Fact]
    public void CalcularCet_fachada_ptax_null_brl_capital_de_giro_nao_lanca_excecao()
    {
        var proposta = CriarProposta(
            taxaAaPercentual: 14.5m,
            iofPercentual: 0.38m,
            prazoDias: 180);

        decimal? ptaxNull = null;
        var act = () => CalculadoraCet.CalcularCet(proposta, ptaxNull, DataDesembolso);

        act.Should().NotThrow(
            because: "fachada com ptax=null e BRL deve despachar para CalcularCetCapitalDeGiro");
    }

    /// <summary>
    /// taxaAaPercentualOverride substitui taxa da proposta no cálculo.
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_com_override_usa_taxa_substituida()
    {
        var proposta = CriarProposta(taxaAaPercentual: 14.5m, prazoDias: 360);

        decimal cetOriginal = CalculadoraCet.CalcularCetCapitalDeGiro(proposta, DataDesembolso);
        decimal cetMenor = CalculadoraCet.CalcularCetCapitalDeGiro(proposta, DataDesembolso, taxaAaPercentualOverride: 12m);

        cetMenor.Should().BeLessThan(cetOriginal,
            because: "override com taxa menor deve produzir CET menor");
    }

    /// <summary>
    /// Rejeita proposta com moeda diferente de BRL — Capital de Giro é operação doméstica.
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_rejeita_moeda_usd()
    {
        var propostaUsd = CriarProposta(moeda: Moeda.Usd);

        var act = () => CalculadoraCet.CalcularCetCapitalDeGiro(propostaUsd, DataDesembolso);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*MoedaOriginal*Brl*");
    }

    /// <summary>
    /// Rejeita proposta com ExigeNdf=true — Capital de Giro não tem hedge cambial.
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_rejeita_ExigeNdf_true()
    {
        var propostaComNdf = CriarProposta(exigeNdf: true, custoNdfAa: 1.5m);

        var act = () => CalculadoraCet.CalcularCetCapitalDeGiro(propostaComNdf, DataDesembolso);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ExigeNdf*");
    }

    /// <summary>
    /// CET deve ser positivo para inputs válidos típicos.
    /// </summary>
    [Fact]
    public void CalcularCetCapitalDeGiro_retorna_cet_positivo()
    {
        var proposta = CriarProposta(
            valorOferecido: 1_500_000m,
            taxaAaPercentual: 14.5m,
            iofPercentual: 0.38m,
            prazoDias: 180);

        decimal cet = CalculadoraCet.CalcularCetCapitalDeGiro(proposta, DataDesembolso);

        cet.Should().BePositive(because: "CET de operação com taxa e IOF deve ser positivo");
    }
}
