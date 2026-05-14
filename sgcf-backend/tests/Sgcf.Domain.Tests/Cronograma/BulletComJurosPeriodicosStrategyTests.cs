using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Domain.Tests.Cronograma;

[Trait("Category", "Domain")]
public sealed class BulletComJurosPeriodicosStrategyTests
{
    // ── Teste 1: FINIMP 720 dias — 3 coupons semestrais intermediários + evento final ──

    [Fact]
    public void Gerar_720DiasSemestral_Retorna3CouponsIntermediariosMaisEventoFinal()
    {
        // Arrange
        LocalDate dataDesembolso = new LocalDate(2026, 5, 14);
        LocalDate dataVencimento = new LocalDate(2028, 5, 14); // 731 dias corridos

        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(1_000_000m, Moeda.Usd),
            TaxaAa: Percentual.DeFracao(0.065m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            PeriodoJurosMeses: 6);

        // Act
        IReadOnlyList<EventoGeradoBulletComJuros> eventos = BulletComJurosPeriodicosStrategy.Gerar(entrada);

        // Assert: 3 coupons intermediários (JUROS) + 1 JUROS final + 1 PRINCIPAL final = 5 eventos
        IEnumerable<EventoGeradoBulletComJuros> eventosJuros = eventos.Where(e => e.Tipo == TipoEventoCronograma.Juros);
        IEnumerable<EventoGeradoBulletComJuros> eventosPrincipal = eventos.Where(e => e.Tipo == TipoEventoCronograma.Principal);

        eventosJuros.Should().HaveCount(4); // 3 intermediários + 1 final
        eventosPrincipal.Should().HaveCount(1);

        // Coupons intermediários devem estar nas datas certas
        eventosJuros.Select(e => e.DataPrevista).Should().Contain(new LocalDate(2026, 11, 14));
        eventosJuros.Select(e => e.DataPrevista).Should().Contain(new LocalDate(2027, 5, 14));
        eventosJuros.Select(e => e.DataPrevista).Should().Contain(new LocalDate(2027, 11, 14));
        eventosJuros.Select(e => e.DataPrevista).Should().Contain(dataVencimento);

        // Principal final no vencimento com saldo zero
        EventoGeradoBulletComJuros principal = eventosPrincipal.Single();
        principal.DataPrevista.Should().Be(dataVencimento);
        principal.Valor.Valor.Should().Be(1_000_000m);
        principal.SaldoDevedorApos.Should().Be(0m);
    }

    // ── Teste 2: PeriodoJurosMeses = 0 lança ArgumentException ──────────────────

    [Fact]
    public void Gerar_PeriodoJuros0_LancaArgumentException()
    {
        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2026, 7, 1),
            PeriodoJurosMeses: 0);

        System.Action act = () => BulletComJurosPeriodicosStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 3: ValorPrincipal zero lança ArgumentException ─────────────────────

    [Fact]
    public void Gerar_ValorPrincipalZero_LancaArgumentException()
    {
        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(0m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2026, 7, 1),
            PeriodoJurosMeses: 6);

        System.Action act = () => BulletComJurosPeriodicosStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 4: DataVencimento anterior ao desembolso lança ArgumentException ───

    [Fact]
    public void Gerar_DataVencimentoAnteriorDesembolso_LancaArgumentException()
    {
        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 6, 1),
            DataVencimento: new LocalDate(2026, 5, 1),
            PeriodoJurosMeses: 6);

        System.Action act = () => BulletComJurosPeriodicosStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 5: IOF câmbio gera evento na data de desembolso ────────────────────

    [Fact]
    public void Gerar_ComIofCambio_GeraEventoIofNaDataDesembolso()
    {
        LocalDate dataDesembolso = new LocalDate(2026, 3, 1);

        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(500_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: new LocalDate(2027, 3, 1),
            PeriodoJurosMeses: 6,
            AliqIofCambio: Percentual.De(0.38m));

        IReadOnlyList<EventoGeradoBulletComJuros> eventos = BulletComJurosPeriodicosStrategy.Gerar(entrada);

        EventoGeradoBulletComJuros eventoIof = eventos.Single(e => e.Tipo == TipoEventoCronograma.IofCambio);
        eventoIof.NumeroEvento.Should().Be(0);
        eventoIof.DataPrevista.Should().Be(dataDesembolso);
        eventoIof.SaldoDevedorApos.Should().BeNull();

        decimal iofEsperado = Math.Round(500_000m * 0.0038m, 2, MidpointRounding.AwayFromZero);
        eventoIof.Valor.Valor.Should().Be(iofEsperado);
        eventoIof.Valor.Moeda.Should().Be(Moeda.Usd);
    }

    // ── Teste 6: IRRF gera evento em cada coupon intermediário e no final ────────

    [Fact]
    public void Gerar_ComIrrf_GeraEventoIrrfEmCadaCoupon()
    {
        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2027, 1, 1),
            PeriodoJurosMeses: 6,
            AliqIrrf: Percentual.De(15m));

        IReadOnlyList<EventoGeradoBulletComJuros> eventos = BulletComJurosPeriodicosStrategy.Gerar(entrada);

        // 1 coupon intermediário (2026-07-01) + 1 final (2027-01-01) = 2 IRRF
        IEnumerable<EventoGeradoBulletComJuros> irrfEventos = eventos.Where(e => e.Tipo == TipoEventoCronograma.IrrfRetido);
        irrfEventos.Should().HaveCount(2);

        // Cada IRRF deve corresponder ao gross-up do JUROS do mesmo evento
        foreach (EventoGeradoBulletComJuros irrf in irrfEventos)
        {
            EventoGeradoBulletComJuros juros = eventos.Single(e =>
                e.Tipo == TipoEventoCronograma.Juros &&
                e.NumeroEvento == irrf.NumeroEvento);

            decimal irrfEsperado = Math.Round(
                juros.Valor.Valor * 0.15m / (1m - 0.15m),
                2,
                MidpointRounding.AwayFromZero);

            irrf.Valor.Valor.Should().Be(irrfEsperado);
        }
    }

    // ── Teste 7: TaxaAa zero — todos os JUROS são zero, PRINCIPAL permanece intacto ─

    [Fact]
    public void Gerar_TaxaZero_JurosZeroTodosCoupons()
    {
        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(200_000m, Moeda.Usd),
            TaxaAa: Percentual.De(0m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2027, 1, 1),
            PeriodoJurosMeses: 6);

        IReadOnlyList<EventoGeradoBulletComJuros> eventos = BulletComJurosPeriodicosStrategy.Gerar(entrada);

        foreach (EventoGeradoBulletComJuros e in eventos.Where(e => e.Tipo == TipoEventoCronograma.Juros))
        {
            e.Valor.Valor.Should().Be(0m);
        }

        EventoGeradoBulletComJuros principal = eventos.Single(e => e.Tipo == TipoEventoCronograma.Principal);
        principal.Valor.Valor.Should().Be(200_000m);
        principal.SaldoDevedorApos.Should().Be(0m);
    }

    // ── Teste 8: Caso A — FINIMP 720 dias, 3 coupons semestrais, valores exatos ──

    [Fact]
    public void Gerar_CasoA_Finimp720Dias3ParcelasSemestraisComIrrf()
    {
        // Arrange
        // 2026-05-14 a 2028-05-14 = 731 dias (ano bissexto 2028)
        // Coupons intermediários: 2026-11-14 (184d), 2027-05-14 (181d), 2027-11-14 (184d)
        // Final: 2027-11-14 a 2028-05-14 (182d)
        LocalDate dataDesembolso = new LocalDate(2026, 5, 14);
        LocalDate dataVencimento = new LocalDate(2028, 5, 14);

        EntradaBulletComJuros entrada = new(
            ValorPrincipal: new Money(1_000_000m, Moeda.Usd),
            TaxaAa: Percentual.DeFracao(0.065m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            PeriodoJurosMeses: 6,
            AliqIrrf: Percentual.De(15m));

        // Act
        IReadOnlyList<EventoGeradoBulletComJuros> eventos = BulletComJurosPeriodicosStrategy.Gerar(entrada);

        // Assert — juros esperados calculados com: principal × taxa × dias / 360, HalfUp 2dp
        // J1: 1_000_000 × 0.065 × 184 / 360 = 33_222.22
        // J2: 1_000_000 × 0.065 × 181 / 360 = 32_680.56
        // J3: 1_000_000 × 0.065 × 184 / 360 = 33_222.22
        // J4: 1_000_000 × 0.065 × 182 / 360 = 32_861.11
        EventoGeradoBulletComJuros coupon1 = eventos.Single(e =>
            e.Tipo == TipoEventoCronograma.Juros && e.DataPrevista == new LocalDate(2026, 11, 14));
        EventoGeradoBulletComJuros coupon2 = eventos.Single(e =>
            e.Tipo == TipoEventoCronograma.Juros && e.DataPrevista == new LocalDate(2027, 5, 14));
        EventoGeradoBulletComJuros coupon3 = eventos.Single(e =>
            e.Tipo == TipoEventoCronograma.Juros && e.DataPrevista == new LocalDate(2027, 11, 14));
        EventoGeradoBulletComJuros couponFinal = eventos.Single(e =>
            e.Tipo == TipoEventoCronograma.Juros && e.DataPrevista == dataVencimento);

        coupon1.Valor.Valor.Should().Be(33_222.22m);
        coupon2.Valor.Valor.Should().Be(32_680.56m);
        coupon3.Valor.Valor.Should().Be(33_222.22m);
        couponFinal.Valor.Valor.Should().Be(32_861.11m);

        // PRINCIPAL final com valor integral e saldo zero
        EventoGeradoBulletComJuros principal = eventos.Single(e => e.Tipo == TipoEventoCronograma.Principal);
        principal.DataPrevista.Should().Be(dataVencimento);
        principal.Valor.Valor.Should().Be(1_000_000m);
        principal.SaldoDevedorApos.Should().Be(0m);

        // IRRF em cada coupon — gross-up: juros × 0.15 / 0.85, HalfUp 2dp
        // IRRF1 = 33_222.22 × 0.15 / 0.85 = 5_862.74
        EventoGeradoBulletComJuros irrf1 = eventos.Single(e =>
            e.Tipo == TipoEventoCronograma.IrrfRetido && e.DataPrevista == new LocalDate(2026, 11, 14));
        irrf1.Valor.Valor.Should().Be(5_862.74m);

        // IRRF no final: 32_861.11 × 0.15 / 0.85 = 5_799.02
        EventoGeradoBulletComJuros irrfFinal = eventos.Single(e =>
            e.Tipo == TipoEventoCronograma.IrrfRetido && e.DataPrevista == dataVencimento);
        irrfFinal.Valor.Valor.Should().Be(5_799.02m);
    }
}
