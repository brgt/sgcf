using System.Linq;

using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Domain.Tests.Cronograma;

[Trait("Category", "Domain")]
public sealed class SacStrategyTests
{
    // ── Dados base: USD 1MM, 6% a.a., base 360, 4 semestrais, 2026-01-01 → 2028-01-01 ──

    private static readonly Money PrincipalBase = new(1_000_000m, Moeda.Usd);
    private static readonly Percentual Taxa6Aa = Percentual.De(6m);
    private static readonly LocalDate DataDesembolsoBase = new LocalDate(2026, 1, 1);
    private static readonly LocalDate DataVencimentoBase = new LocalDate(2028, 1, 1);

    private static EntradaSac EntradaPadrao(int numeroParcelas = 4) =>
        new(
            ValorPrincipal: PrincipalBase,
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: DataDesembolsoBase,
            DataVencimento: DataVencimentoBase,
            NumeroParcelas: numeroParcelas,
            AliqIrrf: null);

    // ── Teste 1: Número correto de eventos sem IRRF ───────────────────────────

    [Fact]
    public void Gerar_4Parcelas_SemIrrf_GeraOitoEventos()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: 4 parcelas × (Juros + Principal) = 8 eventos
        eventos.Should().HaveCount(8);
        eventos.Where(e => e.Tipo == TipoEventoCronograma.Juros).Should().HaveCount(4);
        eventos.Where(e => e.Tipo == TipoEventoCronograma.Principal).Should().HaveCount(4);
        eventos.Should().NotContain(e => e.Tipo == TipoEventoCronograma.IrrfRetido);
    }

    // ── Teste 2: Amortização fixa igual em todas as parcelas ─────────────────

    [Fact]
    public void Gerar_AmortizacaoFixa_IgualEmTodas()
    {
        // Arrange — qualquer número de parcelas
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: AmortizacaoFixa = 1_000_000 / 4 = 250_000
        decimal amortizacaoEsperada = 250_000m;
        IEnumerable<EventoGeradoSac> eventosPrincipal = eventos.Where(e => e.Tipo == TipoEventoCronograma.Principal);
        foreach (EventoGeradoSac ep in eventosPrincipal)
        {
            ep.Valor.Valor.Should().Be(amortizacaoEsperada);
            ep.Valor.Moeda.Should().Be(Moeda.Usd);
        }
    }

    // ── Teste 3: Juros decrescentes (cada parcela tem menos juros que a anterior) ─

    [Fact]
    public void Gerar_Juros_DecrescentesEntreParcelasConsecutivas()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: juros[i] < juros[i-1] para todas as parcelas (saldo decresce)
        List<decimal> juros = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Juros)
            .OrderBy(e => e.NumeroParcela)
            .Select(e => e.Valor.Valor)
            .ToList();

        juros.Should().HaveCount(4);
        for (int i = 1; i < juros.Count; i++)
        {
            juros[i].Should().BeLessThan(juros[i - 1],
                because: $"juros da parcela {i + 1} devem ser menores que os da parcela {i}");
        }
    }

    // ── Teste 4: Saldo devedor zero após a última parcela ────────────────────

    [Fact]
    public void Gerar_SaldoDevedorApos_UltimaParcela_DeveSerZero()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: último evento de Principal tem SaldoDevedorApos = 0
        EventoGeradoSac ultimoPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .OrderBy(e => e.NumeroParcela)
            .Last();

        ultimoPrincipal.SaldoDevedorApos.Should().Be(0m);
    }

    // ── Teste 5: Saldo total amortizado = ValorPrincipal ─────────────────────

    [Fact]
    public void Gerar_SomaPrincipal_IgualValorPrincipalInicial()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert
        decimal somaPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .Sum(e => e.Valor.Valor);

        somaPrincipal.Should().Be(1_000_000m);
    }

    // ── Teste 6: Juros da parcela 1 calculados corretamente ──────────────────

    [Fact]
    public void Gerar_JurosParcela1_CalculadosCorretamente()
    {
        // Arrange — USD 1MM, 6% a.a., base 360
        // Period 1: 2026-01-01 to 2026-01-01.PlusDays(730*1/4) = PlusDays(182) = 2026-07-02
        // Dias = 182; Juros = 1_000_000 × 0.06 × 182 / 360 = 30_333.33
        EntradaSac entrada = EntradaPadrao(4);
        decimal jurosEsperados = Math.Round(1_000_000m * 0.06m * 182m / 360m, 2, MidpointRounding.AwayFromZero);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert
        EventoGeradoSac juros1 = eventos.Single(e => e.NumeroParcela == 1 && e.Tipo == TipoEventoCronograma.Juros);
        juros1.Valor.Valor.Should().Be(jurosEsperados);
        juros1.Valor.Moeda.Should().Be(Moeda.Usd);
        juros1.DataPrevista.Should().Be(new LocalDate(2026, 7, 2));
        juros1.SaldoDevedorApos.Should().BeNull();
    }

    // ── Teste 7: Saldo após parcela 1 = principal - amortização ─────────────

    [Fact]
    public void Gerar_SaldoAposParcela1_IgualPrincipalMenosAmortizacao()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: saldo após parcela 1 = 1_000_000 - 250_000 = 750_000
        EventoGeradoSac principal1 = eventos.Single(e => e.NumeroParcela == 1 && e.Tipo == TipoEventoCronograma.Principal);
        principal1.SaldoDevedorApos.Should().Be(750_000m);
    }

    // ── Teste 8: Com IRRF 15% — gera evento IrrfRetido por parcela ───────────

    [Fact]
    public void Gerar_ComIrrf15Pct_GeraEventoIrrfRetidoPorParcela()
    {
        // Arrange
        EntradaSac entrada = new(
            ValorPrincipal: PrincipalBase,
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: DataDesembolsoBase,
            DataVencimento: DataVencimentoBase,
            NumeroParcelas: 4,
            AliqIrrf: Percentual.De(15m));

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: 4 parcelas × (Juros + Principal + IrrfRetido) = 12 eventos
        eventos.Should().HaveCount(12);
        eventos.Where(e => e.Tipo == TipoEventoCronograma.IrrfRetido).Should().HaveCount(4);
    }

    // ── Teste 9: IRRF gross-up calculado corretamente na parcela 1 ───────────

    [Fact]
    public void Gerar_IrrfParcela1_GrossUpCalculadoCorretamente()
    {
        // Arrange
        EntradaSac entrada = new(
            ValorPrincipal: PrincipalBase,
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: DataDesembolsoBase,
            DataVencimento: DataVencimentoBase,
            NumeroParcelas: 4,
            AliqIrrf: Percentual.De(15m));

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        EventoGeradoSac juros1 = eventos.Single(e => e.NumeroParcela == 1 && e.Tipo == TipoEventoCronograma.Juros);
        EventoGeradoSac irrf1 = eventos.Single(e => e.NumeroParcela == 1 && e.Tipo == TipoEventoCronograma.IrrfRetido);

        // IRRF gross-up = juros × 0.15 / (1 - 0.15)
        decimal irrfEsperado = Math.Round(juros1.Valor.Valor * 0.15m / 0.85m, 2, MidpointRounding.AwayFromZero);

        // Assert
        irrf1.Valor.Valor.Should().Be(irrfEsperado);
        irrf1.Valor.Moeda.Should().Be(Moeda.Usd);
        irrf1.SaldoDevedorApos.Should().BeNull();
    }

    // ── Teste 10: DataVencimento <= DataDesembolso lança exceção ─────────────

    [Fact]
    public void Gerar_DataVencimentoIgualADesembolso_LancaArgumentException()
    {
        // Arrange
        LocalDate data = new LocalDate(2026, 1, 1);
        EntradaSac entrada = new(
            ValorPrincipal: PrincipalBase,
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: data,
            DataVencimento: data,
            NumeroParcelas: 4,
            AliqIrrf: null);

        // Act
        System.Action act = () => SacStrategy.Gerar(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 11: NumeroParcelas = 0 lança exceção ───────────────────────────

    [Fact]
    public void Gerar_NumeroParcelasZero_LancaArgumentException()
    {
        // Arrange
        EntradaSac entrada = new(
            ValorPrincipal: PrincipalBase,
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: DataDesembolsoBase,
            DataVencimento: DataVencimentoBase,
            NumeroParcelas: 0,
            AliqIrrf: null);

        // Act
        System.Action act = () => SacStrategy.Gerar(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 12: ValorPrincipal zero lança exceção ───────────────────────────

    [Fact]
    public void Gerar_ValorPrincipalZero_LancaArgumentException()
    {
        // Arrange
        EntradaSac entrada = new(
            ValorPrincipal: new Money(0m, Moeda.Usd),
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: DataDesembolsoBase,
            DataVencimento: DataVencimentoBase,
            NumeroParcelas: 4,
            AliqIrrf: null);

        // Act
        System.Action act = () => SacStrategy.Gerar(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 13: Número de parcelas correto para cronograma completo ─────────

    [Fact]
    public void Gerar_NumeroParcelas_CorrespondeAoSolicitado()
    {
        // Arrange
        int numeroParcelas = 6;
        EntradaSac entrada = new(
            ValorPrincipal: PrincipalBase,
            TaxaAa: Taxa6Aa,
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: DataDesembolsoBase,
            DataVencimento: DataVencimentoBase,
            NumeroParcelas: numeroParcelas,
            AliqIrrf: null);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: 6 × 2 = 12 eventos
        eventos.Should().HaveCount(numeroParcelas * 2);
        eventos.Select(e => e.NumeroParcela).Distinct().Should().HaveCount(numeroParcelas);
    }

    // ── Teste 14: Cada parcela tem a data prevista posterior à anterior ────────

    [Fact]
    public void Gerar_DatasPrevistas_SaoOrdenadadasCrescentemente()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: datas dos eventos Principal estão em ordem crescente
        List<LocalDate> datas = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .OrderBy(e => e.NumeroParcela)
            .Select(e => e.DataPrevista)
            .ToList();

        for (int i = 1; i < datas.Count; i++)
        {
            datas[i].Should().BeGreaterThan(datas[i - 1],
                because: $"data da parcela {i + 1} deve ser posterior à parcela {i}");
        }
    }

    // ── Teste 15: Sem IOF câmbio (Lei 4.131 nunca tem IOF câmbio) ─────────────

    [Fact]
    public void Gerar_NuncaGeraEventoIofCambio()
    {
        // Arrange
        EntradaSac entrada = EntradaPadrao(4);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert: IOF câmbio não se aplica a Lei 4.131 (empréstimo direto do exterior)
        eventos.Should().NotContain(e => e.Tipo == TipoEventoCronograma.IofCambio);
    }
}
