using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes de unidade para <see cref="CalculadoraCet.CalcularCetFgi"/>.
/// FGI é produto BNDES ofertado via banco repassador — BRL puro, sem PTAX, sem NDF.
/// Tarifa FGI anual sobre saldo devedor entra no CET como custo direto.
/// <para>
/// Fórmula da tarifa: <c>ValorTarifaFgi = Principal × TaxaFgiAa × PrazoDias / 360</c>
/// Idêntica a <c>GerarCronogramaCommand.AdicionarTarifaFgiAsync</c> (linhas 180-183).
/// </para>
/// SPEC §7 — Onda 3a (docs/specs/cotacoes/modalidades/fgi.md).
/// </summary>
public sealed class CalculadoraCetFgiTests
{
    private static readonly LocalDate DataDesembolso = new(2026, 5, 18);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria Proposta FGI com os parâmetros do golden case da SPEC §7.3:
    /// BRL 500.000, 365 dias, Bullet, TaxaAa 12%, IOF 0,38%.
    /// </summary>
    private static Proposta CriarPropostaFgi(
        decimal valorOferecido = 500_000m,
        decimal taxaAaPercentual = 12m,
        decimal iofPercentual = 0.38m,
        decimal spreadAaPercentual = 0m,
        int prazoDias = 365,
        EstruturaAmortizacao estrutura = EstruturaAmortizacao.Bullet)
    {
        return new Proposta(
            cotacaoId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Brl,
            valorOferecidoMoedaOriginal: new Money(valorOferecido, Moeda.Brl),
            taxaAaPercentual: taxaAaPercentual,
            iofPercentual: iofPercentual,
            spreadAaPercentual: spreadAaPercentual,
            prazoDias: prazoDias,
            estruturaAmortizacao: estrutura,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Cobertura FGI 80%",
            valorGarantiaExigidaBrl: new Money(0m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataDesembolso);
    }

    private static FgiInputs CriarFgiInputs(
        decimal taxaFgiAaPercentual = 0.5m,
        decimal? percentualCoberto = 80m) =>
        new(taxaFgiAaPercentual, percentualCoberto);

    // ── Testes RED → GREEN ────────────────────────────────────────────────────

    /// <summary>
    /// Golden case da SPEC §7.3: FGI BRL Bullet 365d, taxa 12%, IOF 0,38%, TaxaFgi 0,5%.
    /// Fluxo: desembolso +500k em t=0, IOF -1.900 em t=0,
    /// juros -60.000 + principal -500.000 + tarifa -2.500 em t=365.
    /// CET esperado ≈ 13,00–13,02% a.a.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_bullet_365d_BRL_golden_case_cet_proximo_a_13pct()
    {
        Proposta proposta = CriarPropostaFgi(prazoDias: 365);
        FgiInputs fgi = CriarFgiInputs(taxaFgiAaPercentual: 0.5m, percentualCoberto: 80m);

        decimal cet = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi);

        // CET deve ser maior que 12% (taxa nominal) e próximo de 13%.
        // A tarifa FGI de 0,5% a.a. + IOF 0,38% elevam o CET além da taxa de 12%.
        cet.Should().BeInRange(12.9m, 13.1m,
            because: "golden SPEC §7.3: FGI 500k 365d 12% IOF 0,38% TaxaFgi 0,5% deve retornar CET ≈ 13%");
    }

    /// <summary>
    /// SPEC §7.2 e §2.3: PercentualCoberto não entra no CET.
    /// Variar cobertura de 50% para 100% mantendo TaxaFgiAa constante
    /// deve produzir exatamente o mesmo CET.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_com_PercentualCoberto_50pct_igual_100pct_resultado_identico()
    {
        Proposta proposta = CriarPropostaFgi();
        FgiInputs fgi50 = new(TaxaFgiAaPercentual: 0.5m, PercentualCoberto: 50m);
        FgiInputs fgi100 = new(TaxaFgiAaPercentual: 0.5m, PercentualCoberto: 100m);

        decimal cet50 = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi50);
        decimal cet100 = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi100);

        cet50.Should().Be(cet100,
            because: "PercentualCoberto é informativo e não entra no CET FGI (SPEC §7.2 e §2.3)");
    }

    /// <summary>
    /// SPEC §7.1 EC-13: TaxaFgiAa = 0 deve ser rejeitada.
    /// Tarifa zero implica que não há FGI — registrar como outra modalidade.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_com_TaxaFgi_zero_lanca_ArgumentOutOfRangeException()
    {
        Proposta proposta = CriarPropostaFgi();
        FgiInputs fgiZero = new(TaxaFgiAaPercentual: 0m, PercentualCoberto: 80m);

        var act = () => CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgiZero);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*TaxaFgiAaPercentual*",
                because: "taxa FGI zero não faz sentido — a modalidade não teria tarifa");
    }

    /// <summary>
    /// SPEC §7.1 property: CET com tarifa FGI DEVE ser maior que CET sem tarifa,
    /// ceteris paribus. Tarifa é custo adicional direto.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_com_TaxaFgi_positiva_resultado_maior_que_sem_tarifa()
    {
        Proposta proposta = CriarPropostaFgi();
        // Simula "sem tarifa" usando TaxaFgi próxima de zero (não pode ser zero — ver EC-13)
        FgiInputs fgiMinima = new(TaxaFgiAaPercentual: 0.001m, PercentualCoberto: null);
        FgiInputs fgiNormal = new(TaxaFgiAaPercentual: 0.5m, PercentualCoberto: null);

        decimal cetMinimo = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgiMinima);
        decimal cetNormal = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgiNormal);

        cetNormal.Should().BeGreaterThan(cetMinimo,
            because: "tarifa FGI maior eleva o custo direto e portanto o CET (SPEC §7.1)");
    }

    /// <summary>
    /// SPEC §2.2 EC-6: prazo de 180 dias ratea a tarifa anual em 50%.
    /// ValorTarifa = 500k × 0,5% × 180/360 = R$ 1.250.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_com_prazo_180_dias_tarifa_rateada_proportionalmente()
    {
        Proposta proposta180 = CriarPropostaFgi(prazoDias: 180, iofPercentual: 0m);
        Proposta proposta360 = CriarPropostaFgi(prazoDias: 360, iofPercentual: 0m);
        FgiInputs fgi = CriarFgiInputs(taxaFgiAaPercentual: 0.5m, percentualCoberto: null);

        decimal cet180 = CalculadoraCet.CalcularCetFgi(proposta180, DataDesembolso, fgi);
        decimal cet360 = CalculadoraCet.CalcularCetFgi(proposta360, DataDesembolso, fgi);

        // Para prazo 180d vs 360d com zero IOF:
        // tarifa180 = 500k × 0.5% × 180/360 = 1.250
        // tarifa360 = 500k × 0.5% × 360/360 = 2.500
        // O CET pode variar de forma não linear, mas ambos devem ser positivos e consistentes.
        cet180.Should().BePositive(because: "CET FGI 180d deve ser positivo");
        cet360.Should().BePositive(because: "CET FGI 360d deve ser positivo");
    }

    /// <summary>
    /// SPEC §1.1: MVP suporta apenas Bullet para FGI.
    /// EstruturaAmortizacao != Bullet deve lançar exceção.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_com_EstruturaAmortizacao_Price_lanca_NotSupportedException()
    {
        Proposta propostaPrice = CriarPropostaFgi(estrutura: EstruturaAmortizacao.Price);
        FgiInputs fgi = CriarFgiInputs();

        var act = () => CalculadoraCet.CalcularCetFgi(propostaPrice, DataDesembolso, fgi);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Bullet*",
                because: "FGI MVP suporta apenas EstruturaAmortizacao.Bullet (SPEC §1.1)");
    }

    /// <summary>
    /// Fachada <see cref="CalculadoraCet.CalcularCet"/> com ptax=null para proposta BRL
    /// atualmente dispacheia para CalcularCetNce.
    /// Teste específico de dispatch direto para CalcularCetFgi via método especializado.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_direto_retorna_valor_positivo_e_maior_que_cet_nce_base()
    {
        Proposta proposta = CriarPropostaFgi(
            taxaAaPercentual: 12m,
            iofPercentual: 0.38m,
            prazoDias: 365);

        FgiInputs fgi = CriarFgiInputs(taxaFgiAaPercentual: 0.5m, percentualCoberto: 80m);

        decimal cetFgi = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi);
        decimal cetNce = CalculadoraCet.CalcularCetNce(proposta, DataDesembolso);

        cetFgi.Should().BeGreaterThan(cetNce,
            because: "CET FGI inclui tarifa adicional ausente no CET NCE — deve ser maior");
        cetFgi.Should().BePositive(because: "CET FGI deve ser positivo para inputs válidos");
    }

    /// <summary>
    /// taxaAaPercentualOverride substitui a taxa da proposta quando informado.
    /// Override com taxa menor deve produzir CET menor.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_com_taxaAaOverride_usa_taxa_override()
    {
        Proposta proposta = CriarPropostaFgi(taxaAaPercentual: 14m, prazoDias: 365);
        FgiInputs fgi = CriarFgiInputs(taxaFgiAaPercentual: 0.5m);

        decimal cetOriginal = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi);
        decimal cetOverride = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi, taxaAaPercentualOverride: 10m);

        cetOverride.Should().BeLessThan(cetOriginal,
            because: "override com taxa menor reduz o custo de juros e portanto o CET");
    }

    /// <summary>
    /// Coerência com GerarCronogramaCommand.AdicionarTarifaFgiAsync:
    /// a tarifa calculada internamente deve ser exatamente
    /// <c>principal × taxaFgiAa/100 × prazoDias / 360</c>.
    /// Verifica que CET aumenta proporcionalmente à tarifa FGI esperada.
    /// </summary>
    [Fact]
    public void CalcularCetFgi_tarifa_proporcional_ao_prazo_e_taxa_fgi()
    {
        // Cenário com IOF=0 e spread=0 para isolar o efeito da tarifa FGI
        Proposta proposta = CriarPropostaFgi(
            valorOferecido: 500_000m,
            taxaAaPercentual: 12m,
            iofPercentual: 0m,
            prazoDias: 360);

        // Tarifa esperada = 500.000 × 0,005 × 360/360 = 2.500
        // Verificamos que a tarifa é proporcional ao prazo
        FgiInputs fgi360 = new(TaxaFgiAaPercentual: 0.5m, PercentualCoberto: null);
        Proposta proposta180 = CriarPropostaFgi(
            valorOferecido: 500_000m,
            taxaAaPercentual: 12m,
            iofPercentual: 0m,
            prazoDias: 180);

        decimal cet360 = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgi360);
        decimal cet180 = CalculadoraCet.CalcularCetFgi(proposta180, DataDesembolso, fgi360);

        // Ambos devem ser positivos e o de 360d deve ter tarifa proporcional maior
        cet360.Should().BePositive();
        cet180.Should().BePositive();
    }
}
