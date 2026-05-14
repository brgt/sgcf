using System.Linq;

using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Domain.Tests.Cronograma;

[Trait("Category", "Domain")]
public sealed class PriceStrategyTests
{
    // ── Teste 1: Parcela única — PMT = PV + Juros, saldo zero ────────────────

    [Fact]
    public void Gerar_1Parcela_RetornaPrincipalEJuros()
    {
        EntradaPrice entrada = new(
            ValorPrincipal: new Money(10_000m, Moeda.Brl),
            TaxaMensal: Percentual.De(1m),
            DataDesembolso: new LocalDate(2026, 5, 14),
            DataPrimeiroVencimento: new LocalDate(2026, 6, 14),
            NumeroParcelas: 1);

        IReadOnlyList<EventoGeradoPrice> eventos = PriceStrategy.Gerar(entrada);

        // Com n=1, PMT = PV (último período absorve principal inteiro), juros = PV × i
        eventos.Should().HaveCount(2);

        EventoGeradoPrice principal = eventos.Single(e => e.Tipo == TipoEventoCronograma.Principal);
        principal.NumeroParcela.Should().Be(1);
        principal.Valor.Valor.Should().Be(10_000m);
        principal.SaldoDevedorApos.Should().Be(0m);

        EventoGeradoPrice juros = eventos.Single(e => e.Tipo == TipoEventoCronograma.Juros);
        juros.NumeroParcela.Should().Be(1);
        juros.Valor.Valor.Should().Be(Math.Round(10_000m * 0.01m, 2, MidpointRounding.AwayFromZero));
        juros.SaldoDevedorApos.Should().BeNull();
    }

    // ── Teste 2: PMT constante em todas as 12 parcelas (tolerância 1 centavo) ─

    [Fact]
    public void Gerar_12ParcelasTaxaMensal1p5_PmtConstante()
    {
        EntradaPrice entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            TaxaMensal: Percentual.De(1.5m),
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataPrimeiroVencimento: new LocalDate(2026, 2, 1),
            NumeroParcelas: 12);

        IReadOnlyList<EventoGeradoPrice> eventos = PriceStrategy.Gerar(entrada);

        // Agrupa os eventos por parcela e soma Juros + Principal para obter PMT de cada período
        List<decimal> pmtPorParcela = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Juros || e.Tipo == TipoEventoCronograma.Principal)
            .GroupBy(e => e.NumeroParcela)
            .OrderBy(g => g.Key)
            .Select(g => g.Sum(e => e.Valor.Valor))
            .ToList();

        pmtPorParcela.Should().HaveCount(12);

        decimal pmtPrimeira = pmtPorParcela[0];

        // Todas as parcelas devem ter PMT igual, exceto possivelmente a última (drift de centavo)
        for (int i = 0; i < pmtPorParcela.Count - 1; i++)
        {
            pmtPorParcela[i].Should().Be(pmtPrimeira,
                because: $"parcela {i + 1} deve ter o mesmo PMT que a parcela 1");
        }

        // Última parcela: tolerância de até R$ 0.01 por acumulação de arredondamento
        Math.Abs(pmtPorParcela[^1] - pmtPrimeira).Should().BeLessThanOrEqualTo(0.01m,
            because: "a última parcela pode diferir em no máximo 1 centavo devido ao ajuste de saldo");
    }

    // ── Teste 3: NumeroParcelas = 0 lança ArgumentException ─────────────────

    [Fact]
    public void Gerar_NumeroParcelas0_LancaArgumentException()
    {
        EntradaPrice entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            TaxaMensal: Percentual.De(1.2m),
            DataDesembolso: new LocalDate(2026, 5, 14),
            DataPrimeiroVencimento: new LocalDate(2026, 6, 14),
            NumeroParcelas: 0);

        System.Action act = () => PriceStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 4: ValorPrincipal = 0 lança ArgumentException ─────────────────

    [Fact]
    public void Gerar_ValorPrincipalZero_LancaArgumentException()
    {
        EntradaPrice entrada = new(
            ValorPrincipal: new Money(0m, Moeda.Brl),
            TaxaMensal: Percentual.De(1.2m),
            DataDesembolso: new LocalDate(2026, 5, 14),
            DataPrimeiroVencimento: new LocalDate(2026, 6, 14),
            NumeroParcelas: 12);

        System.Action act = () => PriceStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 5: Taxa negativa — Percentual.De bloqueia na construção ─────────

    [Fact]
    public void Gerar_TaxaNegativa_LancaExcecao()
    {
        // Percentual.De lança ArgumentOutOfRangeException para valores negativos,
        // garantindo que a estratégia jamais receba uma taxa negativa.
        System.Action act = () => _ = Percentual.De(-1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── Teste 6: DataPrimeiroVencimento <= DataDesembolso lança ArgumentException

    [Fact]
    public void Gerar_DataVencimentoAnteriorDesembolso_LancaArgumentException()
    {
        EntradaPrice entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            TaxaMensal: Percentual.De(1.2m),
            DataDesembolso: new LocalDate(2026, 5, 14),
            DataPrimeiroVencimento: new LocalDate(2026, 5, 14),
            NumeroParcelas: 12);

        System.Action act = () => PriceStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 7: Saldo devedor zero após a última parcela ────────────────────

    [Fact]
    public void Gerar_SaldoZeroNaUltimaParcela()
    {
        EntradaPrice entrada = new(
            ValorPrincipal: new Money(50_000m, Moeda.Brl),
            TaxaMensal: Percentual.De(2m),
            DataDesembolso: new LocalDate(2026, 3, 1),
            DataPrimeiroVencimento: new LocalDate(2026, 4, 1),
            NumeroParcelas: 24);

        IReadOnlyList<EventoGeradoPrice> eventos = PriceStrategy.Gerar(entrada);

        EventoGeradoPrice ultimoPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .OrderBy(e => e.NumeroParcela)
            .Last();

        ultimoPrincipal.SaldoDevedorApos.Should().Be(0m);
    }

    // ── Teste 8: Soma dos principais ≈ ValorPrincipal original ───────────────

    [Fact]
    public void Gerar_SomaPrincipalIgualAoValorOriginal()
    {
        const decimal valorPrincipal = 80_000m;

        EntradaPrice entrada = new(
            ValorPrincipal: new Money(valorPrincipal, Moeda.Brl),
            TaxaMensal: Percentual.De(1.8m),
            DataDesembolso: new LocalDate(2026, 1, 15),
            DataPrimeiroVencimento: new LocalDate(2026, 2, 15),
            NumeroParcelas: 36);

        IReadOnlyList<EventoGeradoPrice> eventos = PriceStrategy.Gerar(entrada);

        decimal somaPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .Sum(e => e.Valor.Valor);

        // Tolerância de R$ 0.02 por acumulação de arredondamento HalfUp ao longo das parcelas
        Math.Abs(somaPrincipal - valorPrincipal).Should().BeLessThanOrEqualTo(0.02m,
            because: "a soma dos principais deve ser igual ao valor original dentro da tolerância de arredondamento");
    }

    // ── Teste 9: Com IRRF 15% — gera evento IrrfRetido por parcela ───────────

    [Fact]
    public void Gerar_ComIrrf_GeraEventoIrrf()
    {
        int numeroParcelas = 6;

        EntradaPrice entrada = new(
            ValorPrincipal: new Money(60_000m, Moeda.Brl),
            TaxaMensal: Percentual.De(1.5m),
            DataDesembolso: new LocalDate(2026, 4, 1),
            DataPrimeiroVencimento: new LocalDate(2026, 5, 1),
            NumeroParcelas: numeroParcelas,
            AliqIrrf: Percentual.De(15m));

        IReadOnlyList<EventoGeradoPrice> eventos = PriceStrategy.Gerar(entrada);

        // 6 parcelas × (Juros + Principal + IrrfRetido) = 18 eventos
        eventos.Should().HaveCount(numeroParcelas * 3);
        eventos.Where(e => e.Tipo == TipoEventoCronograma.IrrfRetido).Should().HaveCount(numeroParcelas);

        // Verifica o gross-up da primeira parcela
        EventoGeradoPrice juros1 = eventos.Single(e => e.NumeroParcela == 1 && e.Tipo == TipoEventoCronograma.Juros);
        EventoGeradoPrice irrf1 = eventos.Single(e => e.NumeroParcela == 1 && e.Tipo == TipoEventoCronograma.IrrfRetido);

        decimal irrfEsperado = Math.Round(juros1.Valor.Valor * 0.15m / 0.85m, 2, MidpointRounding.AwayFromZero);
        irrf1.Valor.Valor.Should().Be(irrfEsperado);
        irrf1.SaldoDevedorApos.Should().BeNull();
    }

    // ── Teste 10: Caso C — FGI-BV 60 parcelas BRL 600k 1,2% a.m. ────────────

    [Fact]
    public void Gerar_CasoC_FgiBv60ParcelasMensais()
    {
        const decimal valorPrincipal = 600_000m;
        const int numeroParcelas = 60;

        EntradaPrice entrada = new(
            ValorPrincipal: new Money(valorPrincipal, Moeda.Brl),
            TaxaMensal: Percentual.De(1.2m),
            DataDesembolso: new LocalDate(2026, 5, 14),
            DataPrimeiroVencimento: new LocalDate(2026, 6, 15),
            NumeroParcelas: numeroParcelas);

        IReadOnlyList<EventoGeradoPrice> eventos = PriceStrategy.Gerar(entrada);

        // 60 parcelas × (Juros + Principal) = 120 eventos
        eventos.Should().HaveCount(numeroParcelas * 2);

        // Saldo devedor zero na última parcela
        EventoGeradoPrice ultimoPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .OrderBy(e => e.NumeroParcela)
            .Last();

        ultimoPrincipal.SaldoDevedorApos.Should().Be(0m);
        ultimoPrincipal.NumeroParcela.Should().Be(numeroParcelas);

        // Soma dos principais dentro de R$ 0.60 de tolerância (60 parcelas × max 1 centavo de drift)
        decimal somaPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .Sum(e => e.Valor.Valor);

        Math.Abs(somaPrincipal - valorPrincipal).Should().BeLessThanOrEqualTo(0.60m,
            because: "a soma dos 60 principais deve aproximar o valor original dentro de R$ 0.60");

        // Datas: parcela 1 em 2026-06-15, parcela 60 em 2031-05-15
        EventoGeradoPrice primeiroPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .OrderBy(e => e.NumeroParcela)
            .First();

        primeiroPrincipal.DataPrevista.Should().Be(new LocalDate(2026, 6, 15));
        ultimoPrincipal.DataPrevista.Should().Be(new LocalDate(2031, 5, 15));
    }
}
