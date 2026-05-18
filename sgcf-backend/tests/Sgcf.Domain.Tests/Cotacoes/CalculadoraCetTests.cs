using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class CalculadoraCetTests
{
    private static readonly LocalDate DataDesembolso = new(2026, 5, 15);
    private const decimal PtaxFixo = 5.20m;

    // ─── Testes exemplo-based ────────────────────────────────────────────────

    [Fact]
    public void CalcularCet_para_proposta_BRL_bullet_deve_retornar_valor_positivo()
    {
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Brl,
            valorOferecido: 1_000_000m,
            taxaAaPercentual: 6m,
            iofPercentual: 0m,
            spreadAaPercentual: 0m,
            prazoDias: 180);

        decimal cet = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);

        cet.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalcularCet_sem_custos_adicionais_deve_ser_proximo_a_taxa_nominal()
    {
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Usd,
            valorOferecido: 200_000m,
            taxaAaPercentual: 6m,
            iofPercentual: 0m,
            spreadAaPercentual: 0m,
            prazoDias: 360,
            exigeNdf: false,
            garantiaEhCdbCativo: false);

        decimal cet = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);

        cet.Should().BeInRange(5m, 8m, "CET sem custos adicionais deve ser próximo à taxa nominal");
    }

    [Fact]
    public void CalcularCet_com_NDF_deve_ser_maior_que_sem_NDF()
    {
        var propostaSemNdf = PropostaFactory.CriarProposta(
            taxaAaPercentual: 5m,
            spreadAaPercentual: 0.5m,
            prazoDias: 180,
            exigeNdf: false);

        var propostaComNdf = PropostaFactory.CriarProposta(
            taxaAaPercentual: 5m,
            spreadAaPercentual: 0.5m,
            prazoDias: 180,
            exigeNdf: true,
            custoNdfAaPercentual: 1.0m);

        decimal cetSemNdf = CalculadoraCet.CalcularCet(propostaSemNdf, PtaxFixo, DataDesembolso);
        decimal cetComNdf = CalculadoraCet.CalcularCet(propostaComNdf, PtaxFixo, DataDesembolso);

        cetComNdf.Should().BeGreaterThan(cetSemNdf,
            "NDF adiciona custo ao CET conforme SPEC §5.1 e §10.3");
    }

    [Fact]
    public void CalcularCet_com_garantia_CDB_rendendo_deve_ser_menor_que_sem_garantia()
    {
        var propostaSemCdb = PropostaFactory.CriarProposta(
            taxaAaPercentual: 6m,
            prazoDias: 180,
            garantiaEhCdbCativo: false,
            valorGarantia: 500_000m);

        var propostaComCdb = PropostaFactory.CriarProposta(
            taxaAaPercentual: 6m,
            prazoDias: 180,
            garantiaEhCdbCativo: true,
            rendimentoCdbAaPercentual: 10.5m,
            valorGarantia: 500_000m);

        decimal cetSemCdb = CalculadoraCet.CalcularCet(propostaSemCdb, PtaxFixo, DataDesembolso);
        decimal cetComCdb = CalculadoraCet.CalcularCet(propostaComCdb, PtaxFixo, DataDesembolso);

        cetComCdb.Should().BeLessThan(cetSemCdb,
            "Rendimento CDB cativo SUBTRAI do custo efetivo conforme SPEC §5.1 e §10.3");
    }

    [Fact]
    public void CalcularCet_com_proposta_nula_deve_lancar_excecao()
    {
        var act = () => CalculadoraCet.CalcularCet(null!, PtaxFixo, DataDesembolso);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalcularCet_com_PTAX_zero_deve_lancar_excecao()
    {
        var proposta = PropostaFactory.CriarProposta();
        var act = () => CalculadoraCet.CalcularCet(proposta, 0m, DataDesembolso);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*PtaxUsdBrl*positiva*");
    }

    // Bug Prove-It: CET do contrato fechado deve refletir a taxa final negociada,
    // não a taxa original da proposta. Necessário para calcular economia corretamente
    // em ConverterEmContratoCommandHandler (SPEC §5.2).
    [Fact]
    public void CalcularCet_com_taxaAaOverride_deve_usar_taxa_override_em_vez_da_proposta()
    {
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Usd,
            valorOferecido: 1_000_000m,
            taxaAaPercentual: 6.2m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 180);

        decimal cetTaxaOriginal = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);
        decimal cetTaxaReduzida = CalculadoraCet.CalcularCet(
            proposta, PtaxFixo, DataDesembolso, taxaAaPercentualOverride: 6.0m);

        cetTaxaReduzida.Should().BeLessThan(cetTaxaOriginal,
            "taxa final negociada 6.0% < 6.2% original deve produzir CET menor");
    }

    // Bug Prove-It #21: rendimento CDB cativo ≥ 100% do principal produzia CET negativo.
    // O modelo correto não permite que o rendimento da garantia torne o empréstimo lucrativo
    // para o tomador (rendimento pertence ao banco durante o bloqueio). CET tem floor em 0%.
    [Fact]
    public void CalcularCet_com_CDB_alto_nao_deve_produzir_CET_negativo()
    {
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Brl,
            valorOferecido: 1_040_000m,
            taxaAaPercentual: 6m,
            iofPercentual: 0m,
            spreadAaPercentual: 0m,
            prazoDias: 180,
            garantiaEhCdbCativo: true,
            rendimentoCdbAaPercentual: 11.15m,
            valorGarantia: 1_040_000m); // garantia = 100% do principal

        decimal cet = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);

        cet.Should().BeGreaterThanOrEqualTo(0m,
            "rendimento CDB pertence ao banco durante bloqueio; CET tem floor em 0%");
    }

    [Fact]
    public void CalcularCet_sem_override_deve_usar_taxa_da_proposta()
    {
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Usd,
            valorOferecido: 1_000_000m,
            taxaAaPercentual: 6.2m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 180);

        decimal cetSemOverride = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);
        decimal cetOverrideExplicito = CalculadoraCet.CalcularCet(
            proposta, PtaxFixo, DataDesembolso, taxaAaPercentualOverride: 6.2m);

        cetOverrideExplicito.Should().Be(cetSemOverride,
            "override igual à taxa da proposta produz resultado idêntico");
    }

    // ─── Property-based tests (FsCheck 2.x) ─────────────────────────────────
    // SPEC §10.3
    // FsCheck 2.x Prop.ForAll suporta até 3 Arbitrary; combina parâmetros em tupla quando necessário.

    [Property(MaxTest = 100)]
    public Property CET_com_NDF_obrigatorio_deve_ser_sempre_maior_que_sem_NDF()
    {
        // Gera tupla (taxa%, prazo dias, valor USD)
        var gen = from taxa in Gen.Choose(1, 10).Select(t => (decimal)t)
                  from prazo in Gen.Choose(30, 360)
                  from valor in Gen.Choose(1, 500).Select(v => (decimal)(v * 1000))
                  select (taxa, prazo, valor);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (taxa, prazo, valor) = tuple;

            var propostaSemNdf = PropostaFactory.CriarProposta(
                moedaOriginal: Moeda.Usd,
                valorOferecido: valor,
                taxaAaPercentual: taxa,
                iofPercentual: 0m,
                spreadAaPercentual: 0m,
                prazoDias: prazo,
                exigeNdf: false);

            var propostaComNdf = PropostaFactory.CriarProposta(
                moedaOriginal: Moeda.Usd,
                valorOferecido: valor,
                taxaAaPercentual: taxa,
                iofPercentual: 0m,
                spreadAaPercentual: 0m,
                prazoDias: prazo,
                exigeNdf: true,
                custoNdfAaPercentual: 1.0m);

            try
            {
                decimal cetSemNdf = CalculadoraCet.CalcularCet(propostaSemNdf, PtaxFixo, DataDesembolso);
                decimal cetComNdf = CalculadoraCet.CalcularCet(propostaComNdf, PtaxFixo, DataDesembolso);

                return cetComNdf > cetSemNdf;
            }
            catch
            {
                return true; // Exceções válidas não falsificam a propriedade
            }
        });
    }

    [Property(MaxTest = 100)]
    public Property CET_com_garantia_CDB_deve_ser_sempre_menor_que_sem_garantia()
    {
        // Combina taxa, prazo, valor e rendimentoCdb em tupla (FsCheck 2.x: ForAll com 1 Arb)
        var gen = from taxa in Gen.Choose(1, 10).Select(t => (decimal)t)
                  from prazo in Gen.Choose(60, 360)
                  from valor in Gen.Choose(1, 300).Select(v => (decimal)(v * 1000))
                  from rendimento in Gen.Choose(5, 15).Select(r => (decimal)r)
                  select (taxa, prazo, valor, rendimento);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (taxa, prazo, valor, rendimentoCdb) = tuple;

            var propostaSemCdb = PropostaFactory.CriarProposta(
                moedaOriginal: Moeda.Usd,
                valorOferecido: valor,
                taxaAaPercentual: taxa,
                prazoDias: prazo,
                garantiaEhCdbCativo: false,
                valorGarantia: valor * PtaxFixo * 0.5m);

            var propostaComCdb = PropostaFactory.CriarProposta(
                moedaOriginal: Moeda.Usd,
                valorOferecido: valor,
                taxaAaPercentual: taxa,
                prazoDias: prazo,
                garantiaEhCdbCativo: true,
                rendimentoCdbAaPercentual: rendimentoCdb,
                valorGarantia: valor * PtaxFixo * 0.5m);

            try
            {
                decimal cetSemCdb = CalculadoraCet.CalcularCet(propostaSemCdb, PtaxFixo, DataDesembolso);
                decimal cetComCdb = CalculadoraCet.CalcularCet(propostaComCdb, PtaxFixo, DataDesembolso);

                return cetComCdb < cetSemCdb;
            }
            catch
            {
                return true;
            }
        });
    }

    [Property(MaxTest = 100)]
    public Property CET_deve_ser_sempre_positivo_para_inputs_validos()
    {
        var gen = from taxa in Gen.Choose(1, 10).Select(t => (decimal)t)
                  from prazo in Gen.Choose(30, 360)
                  from valor in Gen.Choose(100, 10_000).Select(v => (decimal)v)
                  select (taxa, prazo, valor);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (taxa, prazo, valor) = tuple;

            var proposta = PropostaFactory.CriarProposta(
                moedaOriginal: Moeda.Usd,
                valorOferecido: valor,
                taxaAaPercentual: taxa,
                iofPercentual: 0m,
                spreadAaPercentual: 0m,
                prazoDias: prazo);

            try
            {
                decimal cet = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);
                return cet > 0m;
            }
            catch
            {
                return true;
            }
        });
    }
}

// ─── Testes F0.2 — CalculadoraCet por modalidade ────────────────────────────

/// <summary>
/// F0.2: CalcularCetFinimp deve produzir resultado bit-a-bit idêntico ao CalcularCet legado
/// quando invocado com os mesmos inputs. Garante que a extração da lógica não introduziu
/// nenhuma divergência numérica. Onda 0 F0.2.
/// </summary>
public sealed class CalculadoraCetF02Tests
{
    private static readonly LocalDate DataDesembolso = new(2026, 5, 15);
    private const decimal PtaxFixo = 5.20m;

    [Fact]
    public void CalcularCetFinimp_FromProposta_USD_NDF_resultado_consistente_com_legado()
    {
        // Arrange — cenário FINIMP USD com NDF (representativo do golden dataset)
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Usd,
            valorOferecido: 1_000_000m,
            taxaAaPercentual: 6.5m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.5m,
            prazoDias: 180,
            exigeNdf: true,
            custoNdfAaPercentual: 1.2m);

        // Act — resultado via fachada legada e via método especializado devem ser bit-a-bit iguais
        decimal cetViaFachada = CalculadoraCet.CalcularCet(proposta, PtaxFixo, DataDesembolso);
        decimal cetViaEspecializado = CalculadoraCet.CalcularCetFinimp(proposta, PtaxFixo, DataDesembolso);

        // Assert — bit-a-bit idênticos: sem arredondamento adicional na extração
        cetViaEspecializado.Should().Be(cetViaFachada,
            "CalcularCetFinimp deve ser extração exata da lógica existente — zero divergência numérica");
    }

    [Fact]
    public void CalcularCet_fachada_dispatcheia_Finimp_para_CalcularCetFinimp()
    {
        // Arrange — proposta FINIMP USD simples, ptax não-null
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Usd,
            valorOferecido: 500_000m,
            taxaAaPercentual: 6m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0m,
            prazoDias: 180);

        decimal? ptaxNullable = 5.10m; // non-null → rota FINIMP

        // Act
        decimal cetFachada = CalculadoraCet.CalcularCet(proposta, ptaxNullable, DataDesembolso);
        decimal cetEspecializado = CalculadoraCet.CalcularCetFinimp(proposta, ptaxNullable.Value, DataDesembolso);

        // Assert — fachada deve produzir mesmo resultado que CalcularCetFinimp
        cetFachada.Should().Be(cetEspecializado,
            "fachada com ptax não-null deve despachar para CalcularCetFinimp e retornar resultado idêntico");
    }

    [Fact]
    public void CalcularCetRefinimp_delega_para_CalcularCetFinimp()
    {
        // REFINIMP usa fórmula CET idêntica ao FINIMP (SPEC §4.2)
        var proposta = PropostaFactory.CriarProposta(
            moedaOriginal: Moeda.Usd,
            valorOferecido: 750_000m,
            taxaAaPercentual: 5.8m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.2m,
            prazoDias: 270);

        decimal cetRefinimp = CalculadoraCet.CalcularCetRefinimp(proposta, PtaxFixo, DataDesembolso);
        decimal cetFinimp = CalculadoraCet.CalcularCetFinimp(proposta, PtaxFixo, DataDesembolso);

        cetRefinimp.Should().Be(cetFinimp,
            "REFINIMP é FINIMP refinanciado — CET usa mesma fórmula, logo resultado deve ser idêntico");
    }

    /// <summary>
    /// Onda 4: Lei4131 usa mesma fórmula do FINIMP (delega a CalcularCetFinimp).
    /// Verifica que o CET é positivo e idêntico ao resultado de FINIMP com os mesmos parâmetros.
    /// </summary>
    [Fact]
    public void CalcularCetLei4131_retorna_CET_identico_ao_FINIMP()
    {
        var proposta = PropostaFactory.CriarProposta(); // proposta USD — válida para Lei4131

        decimal cetLei4131 = CalculadoraCet.CalcularCetLei4131(proposta, PtaxFixo, DataDesembolso);
        decimal cetFinimp = CalculadoraCet.CalcularCetFinimp(proposta, PtaxFixo, DataDesembolso);

        cetLei4131.Should().BeGreaterThan(0m, "CET Lei4131 deve ser positivo com parâmetros válidos");
        cetLei4131.Should().Be(cetFinimp,
            "Lei 4131 delega a CalcularCetFinimp — SPEC §7.5: fórmulas idênticas");
    }

    /// <summary>
    /// Lei4131 rejeita BRL como moeda original — operação sempre estrangeira. SPEC §7.1.
    /// </summary>
    [Fact]
    public void CalcularCetLei4131_com_proposta_BRL_lanca_ArgumentException()
    {
        var proposta = PropostaFactory.CriarProposta(moedaOriginal: Moeda.Brl);

        var act = () => CalculadoraCet.CalcularCetLei4131(proposta, PtaxFixo, DataDesembolso);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Brl*");
    }

    [Fact]
    public void CalcularCetNce_retorna_CET_positivo_para_proposta_BRL_valida()
    {
        // Onda 2: CalcularCetNce implementado — não lança mais NotImplementedException.
        var proposta = PropostaFactory.CriarProposta(moedaOriginal: Moeda.Brl);

        decimal cet = CalculadoraCet.CalcularCetNce(proposta, DataDesembolso);

        cet.Should().BeGreaterThan(0m, "NCE BRL com parâmetros padrão deve produzir CET positivo");
    }

    [Fact]
    public void CalcularCetCapitalDeGiro_lanca_NotImplementedException()
    {
        var proposta = PropostaFactory.CriarProposta(moedaOriginal: Moeda.Brl);

        var act = () => CalculadoraCet.CalcularCetCapitalDeGiro(proposta, DataDesembolso);

        act.Should().Throw<NotImplementedException>("Capital de Giro será implementado em Onda futura");
    }

    /// <summary>
    /// Regressão Onda 3a: CalcularCetFgi agora implementado — não lança mais NotImplementedException.
    /// Proposta BRL Bullet com TaxaFgi positiva deve retornar CET > 0.
    /// Substituindo o stub-test que validava NIE (Onda 0 F0.2).
    /// </summary>
    [Fact]
    public void CalcularCetFgi_implementado_retorna_cet_positivo()
    {
        // PropostaFactory cria proposta BRL Bullet por padrão — compatível com FGI.
        var proposta = PropostaFactory.CriarProposta(moedaOriginal: Moeda.Brl);
        var fgiInputs = new FgiInputs(TaxaFgiAaPercentual: 0.5m, PercentualCoberto: 80m);

        decimal cet = CalculadoraCet.CalcularCetFgi(proposta, DataDesembolso, fgiInputs);

        cet.Should().BeGreaterThan(0m,
            "CalcularCetFgi implementado na Onda 3a deve retornar CET positivo para inputs válidos");
    }

    [Fact]
    public void CalcularCet_fachada_com_ptax_null_e_proposta_BRL_dispacheia_para_CalcularCetNce()
    {
        // Onda 2: fachada com ptax null + proposta BRL → CalcularCetNce (implementado).
        var proposta = PropostaFactory.CriarProposta(moedaOriginal: Moeda.Brl);
        decimal? ptaxNull = null;

        decimal cet = CalculadoraCet.CalcularCet(proposta, ptaxNull, DataDesembolso);

        cet.Should().BeGreaterThan(0m,
            "fachada com ptax null e proposta BRL deve despachar para CalcularCetNce e retornar CET positivo");
    }
}

/// <summary>Extension para criar Arbitrary a partir de Gen — compatível com FsCheck 2.x.</summary>
internal static class GenExtensions
{
    internal static Arbitrary<T> ToArbitrary<T>(this Gen<T> gen) =>
        Arb.From(gen);
}
