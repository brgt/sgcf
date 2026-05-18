using FluentAssertions;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários para <see cref="CalculadoraIrrfEstimado"/>.
/// Cobre a fórmula SPEC §8.1:
///   JurosProjetadosMoedaOriginal = ValorOferecido × (TaxaAa + Spread) × (PrazoDias / 360)
///   JurosProjetadosBrl           = JurosProjetadosMoedaOriginal × Ptax
///   IrrfEstimadoBrl              = JurosProjetadosBrl × (AliquotaIrrf / 100)
/// Onda 4 — Lei 4131.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CalculadoraIrrfEstimadoTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static readonly LocalDate DataCaptura = new(2026, 5, 18);
    private static readonly IClock Clock = TestHelpers.CriarClock();

    /// <summary>
    /// Cria proposta USD com os parâmetros fornecidos.
    /// A cotação estará em EmCaptacao com PTAX 5,0123.
    /// </summary>
    private static Proposta CriarPropostaUsd(
        decimal valorOferecidoUsd,
        decimal taxaAa,
        decimal spreadAa,
        int prazoDias)
    {
        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-IRRF-01",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: new Money(25_000_000m, Moeda.Brl),
            prazoMaximoDias: 720,
            dataAbertura: DataCaptura,
            dataPtaxReferencia: new LocalDate(2026, 5, 17),
            ptaxUsadaUsdBrl: 5.0123m,
            clock: Clock);

        cotacao.Enviar(Clock);

        return cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(valorOferecidoUsd, Moeda.Usd),
            taxaAaPercentual: taxaAa,
            iofPercentual: 0.38m,
            spreadAaPercentual: spreadAa,
            prazoDias: prazoDias,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "SBLC 100% (obrigatório)",
            valorGarantiaExigidaBrl: new Money(25_000_000m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataCaptura);
    }

    // ── Casos limites — alíquota nula ou zero ─────────────────────────────────

    /// <summary>
    /// Quando alíquota é null (não informada), IRRF = 0. SPEC §8.3.
    /// </summary>
    [Fact]
    public void Calcular_AliquotaNull_RetornaZero()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, 6m, 0.5m, 720);

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, 5.0123m, aliquotaIrrfPercentual: null);

        resultado.Should().Be(0m);
    }

    /// <summary>
    /// Quando alíquota é zero, IRRF = 0. Cobre acordos com isenção total.
    /// </summary>
    [Fact]
    public void Calcular_AliquotaZero_RetornaZero()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, 6m, 0.5m, 720);

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, 5.0123m, aliquotaIrrfPercentual: 0m);

        resultado.Should().Be(0m);
    }

    /// <summary>
    /// Quando alíquota é negativa, IRRF = 0. Defesa contra input inválido.
    /// </summary>
    [Fact]
    public void Calcular_AliquotaNegativa_RetornaZero()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, 6m, 0.5m, 720);

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, 5.0123m, aliquotaIrrfPercentual: -1m);

        resultado.Should().Be(0m);
    }

    // ── Golden case (SPEC §8.1 + §7.4) ───────────────────────────────────────

    /// <summary>
    /// Golden case da SPEC §8.1:
    ///   USD 5.000.000, taxa 6% a.a., spread 0,5% a.a., prazo 720 dias, PTAX 5,0123.
    ///   Alíquota IRRF 15% (regra geral).
    ///
    ///   JurosProjetadosUsd = 5_000_000 × (6 + 0,5)/100 × (720/360) = 5_000_000 × 0,065 × 2 = 650_000
    ///   JurosProjetadosBrl = 650_000 × 5,0123 = 3_257_995
    ///   IrrfEstimadoBrl    = 3_257_995 × 0,15 = 488_699,25 → arredondado: 488_699,25
    /// </summary>
    [Fact]
    public void Calcular_GoldenCase_15pct_RetornaValorEsperado()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, taxaAa: 6m, spreadAa: 0.5m, prazoDias: 720);
        const decimal ptax = 5.0123m;
        const decimal aliquota = 15m;

        // JurosProjetadosUsd = 5_000_000 × 0.065 × (720/360) = 650_000
        // JurosProjetadosBrl = 650_000 × 5.0123 = 3_257_995
        // IrrfEstimadoBrl    = 3_257_995 × 0.15 = 488_699.25
        const decimal esperado = 488_699.25m;

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, ptax, aliquota);

        resultado.Should().Be(esperado);
    }

    /// <summary>
    /// Alíquota 12,5% (acordo Japão — SPEC §2.4):
    ///   IrrfEstimadoBrl = 3_257_995 × 0,125 = 407_249,375 → arredondado: 407_249,38
    /// </summary>
    [Fact]
    public void Calcular_GoldenCase_12_5pct_RetornaValorEsperado()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, taxaAa: 6m, spreadAa: 0.5m, prazoDias: 720);
        const decimal ptax = 5.0123m;
        const decimal aliquota = 12.5m;

        // JurosProjetadosBrl = 3_257_995 (igual ao golden case acima)
        // IrrfEstimadoBrl    = 3_257_995 × 0.125 = 407_249.375 → AwayFromZero → 407_249.38
        const decimal esperado = 407_249.38m;

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, ptax, aliquota);

        resultado.Should().Be(esperado);
    }

    /// <summary>
    /// Alíquota 25% (jurisdição favorecida — SPEC §2.4):
    ///   IrrfEstimadoBrl = 3_257_995 × 0,25 = 814_498,75
    /// </summary>
    [Fact]
    public void Calcular_GoldenCase_25pct_RetornaValorEsperado()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, taxaAa: 6m, spreadAa: 0.5m, prazoDias: 720);
        const decimal ptax = 5.0123m;
        const decimal aliquota = 25m;

        // IrrfEstimadoBrl = 3_257_995 × 0.25 = 814_498.75
        const decimal esperado = 814_498.75m;

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, ptax, aliquota);

        resultado.Should().Be(esperado);
    }

    // ── Proporcionalidade de prazo ─────────────────────────────────────────────

    /// <summary>
    /// Prazo 360 dias (1 ano):
    ///   JurosProjetadosUsd = 5_000_000 × 0.065 × 1 = 325_000
    ///   JurosProjetadosBrl = 325_000 × 5.0123 = 1_628_997.50
    ///   IrrfEstimadoBrl    = 1_628_997.50 × 0.15 = 244_349.625 → arredondado: 244_349.63
    /// </summary>
    [Fact]
    public void Calcular_Prazo360Dias_ProporcionalAoPrazo()
    {
        Proposta proposta = CriarPropostaUsd(5_000_000m, taxaAa: 6m, spreadAa: 0.5m, prazoDias: 360);
        const decimal ptax = 5.0123m;
        const decimal aliquota = 15m;

        // JurosProjetadosUsd = 5_000_000 × 0.065 × 1 = 325_000
        // JurosProjetadosBrl = 325_000 × 5.0123 = 1_628_997.5
        // IrrfEstimadoBrl    = 1_628_997.5 × 0.15 = 244_349.625 → 244_349.63
        const decimal esperado = 244_349.63m;

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, ptax, aliquota);

        resultado.Should().Be(esperado);
    }

    // ── Resultado arredondado a 2 casas decimais (AwayFromZero) ───────────────

    /// <summary>
    /// Verifica que o resultado sempre tem no máximo 2 casas decimais.
    /// </summary>
    [Fact]
    public void Calcular_ResultadoTemDuasCasasDecimais()
    {
        Proposta proposta = CriarPropostaUsd(1_000_000m, taxaAa: 7.5m, spreadAa: 0.75m, prazoDias: 180);
        const decimal ptax = 5.20m;
        const decimal aliquota = 15m;

        decimal resultado = CalculadoraIrrfEstimado.Calcular(proposta, ptax, aliquota);

        // Verifica que o resultado é arredondado a 2 casas
        decimal rounded = Math.Round(resultado, 2, MidpointRounding.AwayFromZero);
        resultado.Should().Be(rounded, "o IRRF estimado deve ser arredondado a 2 casas decimais (AwayFromZero)");
    }
}
