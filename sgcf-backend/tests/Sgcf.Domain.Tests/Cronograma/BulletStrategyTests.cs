using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Domain.Tests.Cronograma;

[Trait("Category", "Domain")]
public sealed class BulletStrategyTests
{
    // ── Teste 1: FINIMP BB Tóquio USD 200k, 181 dias, 5,879% a.a., base 360 ─

    [Fact]
    public void Gerar_FinimpBbToquio_JurosCalculadosCorretamente()
    {
        // Arrange
        LocalDate dataDesembolso = new LocalDate(2026, 1, 1);
        LocalDate dataVencimento = new LocalDate(2026, 7, 1); // 181 dias

        EntradaBullet entrada = new(
            ValorPrincipal: new Money(200_000m, Moeda.Usd),
            TaxaAa: Percentual.DeFracao(0.05879m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Cálculo esperado: 200000 × 0.05879 × 181 / 360 = 5911.66 (HalfUp 2dp)
        decimal jurosEsperados = Math.Round(200_000m * 0.05879m * 181m / 360m, 2, MidpointRounding.AwayFromZero);

        // Act
        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        // Assert
        eventos.Should().HaveCount(2);

        EventoGeradoBullet eventoPrincipal = eventos.Single(e => e.Tipo == TipoEventoCronograma.Principal);
        eventoPrincipal.Valor.Valor.Should().Be(200_000m);
        eventoPrincipal.Valor.Moeda.Should().Be(Moeda.Usd);
        eventoPrincipal.DataPrevista.Should().Be(dataVencimento);
        eventoPrincipal.NumeroEvento.Should().Be(1);
        eventoPrincipal.SaldoDevedorApos.Should().Be(0m);

        EventoGeradoBullet eventoJuros = eventos.Single(e => e.Tipo == TipoEventoCronograma.Juros);
        eventoJuros.Valor.Valor.Should().Be(jurosEsperados);
        eventoJuros.Valor.Moeda.Should().Be(Moeda.Usd);
        eventoJuros.DataPrevista.Should().Be(dataVencimento);
        eventoJuros.NumeroEvento.Should().Be(1);
        eventoJuros.SaldoDevedorApos.Should().BeNull();
    }

    // ── Teste 2: FINIMP com IRRF 15% ─────────────────────────────────────────

    [Fact]
    public void Gerar_ComIrrf15Pct_EventoIrrfRetidoCalculadoCorretamente()
    {
        // Arrange
        LocalDate dataDesembolso = new LocalDate(2026, 1, 1);
        LocalDate dataVencimento = new LocalDate(2027, 1, 1); // 365 dias corridos, base 360 = 360 dias útil por convenção

        EntradaBullet entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            AliqIrrf: Percentual.De(15m),
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        // Assert — deve haver IRRF retido
        EventoGeradoBullet eventoIrrf = eventos.Single(e => e.Tipo == TipoEventoCronograma.IrrfRetido);
        eventoIrrf.NumeroEvento.Should().Be(1);
        eventoIrrf.DataPrevista.Should().Be(dataVencimento);
        eventoIrrf.SaldoDevedorApos.Should().BeNull();

        // IRRF gross-up = Juros × 0.15 / (1 - 0.15)
        EventoGeradoBullet eventoJuros = eventos.Single(e => e.Tipo == TipoEventoCronograma.Juros);
        decimal aliqIrrf = 0.15m;
        decimal irrfEsperado = Math.Round(eventoJuros.Valor.Valor * aliqIrrf / (1m - aliqIrrf), 2, MidpointRounding.AwayFromZero);
        eventoIrrf.Valor.Valor.Should().Be(irrfEsperado);
        eventoIrrf.Valor.Moeda.Should().Be(Moeda.Usd);
    }

    // ── Teste 2b: caso canônico IRRF gross-up — USD 100k, 360 dias base 360, 6% a.a. ─

    [Fact]
    public void Gerar_Irrf_JurosExatos6000_IrrfGrossUp1058p82()
    {
        // Arrange — 360 dias corridos entre 2026-01-01 e 2026-12-27
        // Para garantir prazo = 360: de 2026-01-01 a 2026-12-27 = 360 dias
        LocalDate dataDesembolso = new LocalDate(2026, 1, 1);
        LocalDate dataVencimento = new LocalDate(2026, 12, 27); // 360 dias

        EntradaBullet entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            AliqIrrf: Percentual.De(15m),
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        // Juros = 100000 × 0.06 × 360/360 = 6000.00
        EventoGeradoBullet eventoJuros = eventos.Single(e => e.Tipo == TipoEventoCronograma.Juros);
        eventoJuros.Valor.Valor.Should().Be(6_000m);

        // IRRF gross-up = 6000 × 0.15 / (1 - 0.15) = 6000 × 0.15 / 0.85 = 1058.82
        EventoGeradoBullet eventoIrrf = eventos.Single(e => e.Tipo == TipoEventoCronograma.IrrfRetido);
        eventoIrrf.Valor.Valor.Should().Be(1058.82m);
    }

    // ── Teste 3: FINIMP com IOF câmbio 0,38% ─────────────────────────────────

    [Fact]
    public void Gerar_ComIofCambio038Pct_EventoIofNaDataDesembolso()
    {
        // Arrange
        LocalDate dataDesembolso = new LocalDate(2026, 3, 1);
        LocalDate dataVencimento = new LocalDate(2026, 9, 1);

        EntradaBullet entrada = new(
            ValorPrincipal: new Money(500_000m, Moeda.Usd),
            TaxaAa: Percentual.De(5m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            AliqIrrf: null,
            AliqIofCambio: Percentual.De(0.38m),
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        // Assert — evento IOF na data de desembolso
        EventoGeradoBullet eventoIof = eventos.Single(e => e.Tipo == TipoEventoCronograma.IofCambio);
        eventoIof.NumeroEvento.Should().Be(0);
        eventoIof.DataPrevista.Should().Be(dataDesembolso);
        eventoIof.SaldoDevedorApos.Should().BeNull();

        decimal iofEsperado = Math.Round(500_000m * 0.0038m, 2, MidpointRounding.AwayFromZero);
        eventoIof.Valor.Valor.Should().Be(iofEsperado);
        eventoIof.Valor.Moeda.Should().Be(Moeda.Usd);
    }

    // ── Teste 4: Validação — DataVencimento <= DataDesembolso ─────────────────

    [Fact]
    public void Gerar_DataVencimentoIgualADesembolso_LancaArgumentException()
    {
        // Arrange
        LocalDate data = new LocalDate(2026, 6, 1);

        EntradaBullet entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(5m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: data,
            DataVencimento: data,
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        System.Action act = () => BulletStrategy.Gerar(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Gerar_DataVencimentoAnteriorADesembolso_LancaArgumentException()
    {
        // Arrange
        EntradaBullet entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(5m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 6, 1),
            DataVencimento: new LocalDate(2026, 5, 1),
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        System.Action act = () => BulletStrategy.Gerar(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 5: Validação — ValorPrincipal.Valor = 0 ────────────────────────

    [Fact]
    public void Gerar_ValorPrincipalZero_LancaArgumentException()
    {
        // Arrange
        EntradaBullet entrada = new(
            ValorPrincipal: new Money(0m, Moeda.Usd),
            TaxaAa: Percentual.De(5m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2026, 7, 1),
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        System.Action act = () => BulletStrategy.Gerar(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 6: Sem IRRF quando AliqIrrf é null ─────────────────────────────

    [Fact]
    public void Gerar_SemAliqIrrf_NaoGeraEventoIrrfRetido()
    {
        // Arrange
        EntradaBullet entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2026, 7, 1),
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: null);

        // Act
        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        // Assert
        eventos.Should().NotContain(e => e.Tipo == TipoEventoCronograma.IrrfRetido);
    }

    // ── Teste 7: Tarifa ROF em BRL na data de desembolso ─────────────────────

    [Fact]
    public void Gerar_ComTarifaRof_EventoTarifaRofNaDataDesembolso()
    {
        EntradaBullet entrada = new(
            ValorPrincipal: new Money(200_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2026, 7, 1),
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: 1_500m,
            TarifaCadempBrl: null);

        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        EventoGeradoBullet eventoRof = eventos.Single(e => e.Tipo == TipoEventoCronograma.TarifaRof);
        eventoRof.NumeroEvento.Should().Be(0);
        eventoRof.DataPrevista.Should().Be(new LocalDate(2026, 1, 1));
        eventoRof.Valor.Valor.Should().Be(1_500m);
        eventoRof.Valor.Moeda.Should().Be(Moeda.Brl);
        eventoRof.SaldoDevedorApos.Should().BeNull();
    }

    // ── Teste 8: Tarifa CADEMP em BRL na data de desembolso ──────────────────

    [Fact]
    public void Gerar_ComTarifaCademp_EventoTarifaCadempNaDataDesembolso()
    {
        EntradaBullet entrada = new(
            ValorPrincipal: new Money(200_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataVencimento: new LocalDate(2026, 7, 1),
            AliqIrrf: null,
            AliqIofCambio: null,
            TarifaRofBrl: null,
            TarifaCadempBrl: 800m);

        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        EventoGeradoBullet eventoCademp = eventos.Single(e => e.Tipo == TipoEventoCronograma.TarifaCademp);
        eventoCademp.NumeroEvento.Should().Be(0);
        eventoCademp.DataPrevista.Should().Be(new LocalDate(2026, 1, 1));
        eventoCademp.Valor.Valor.Should().Be(800m);
        eventoCademp.Valor.Moeda.Should().Be(Moeda.Brl);
    }

    // ── Teste 9: Invariante — SUM(eventos PRINCIPAL) == ValorPrincipalInicial ─

    [Fact]
    public void Gerar_SomaPrincipal_IgualValorPrincipalInicial()
    {
        EntradaBullet entrada = new(
            ValorPrincipal: new Money(350_000m, Moeda.Usd),
            TaxaAa: Percentual.De(7m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 2, 1),
            DataVencimento: new LocalDate(2026, 8, 1),
            AliqIrrf: Percentual.De(15m),
            AliqIofCambio: Percentual.De(0.38m),
            TarifaRofBrl: 2_000m,
            TarifaCadempBrl: 500m);

        IReadOnlyList<EventoGeradoBullet> eventos = BulletStrategy.Gerar(entrada);

        decimal somaPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .Sum(e => e.Valor.Valor);

        somaPrincipal.Should().Be(350_000m);
    }
}
