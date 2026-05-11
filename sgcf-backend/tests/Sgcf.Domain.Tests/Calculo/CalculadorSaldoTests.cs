using System.Collections.Generic;

using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Calculo;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Domain.Tests.Calculo;

[Trait("Category", "Domain")]
public sealed class CalculadorSaldoTests
{
    // ── Dados base do contrato 4131 BB semestral ──────────────────────────────

    private static readonly Money PrincipalInicial = new(1_000_000m, Moeda.Usd);
    private static readonly Percentual TaxaAa = Percentual.De(6m);
    private static readonly BaseCalculo Base = BaseCalculo.Dias360;
    private static readonly LocalDate DataDesembolso = new LocalDate(2026, 1, 1);

    // Eventos do cronograma (todos inicialmente Previsto)
    private static List<EventoSaldoItem> CriarEventosPrevisto()
    {
        return new List<EventoSaldoItem>
        {
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2026, 6, 30),  new Money(30_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2026, 6, 30),  new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 27), new Money(22_500m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 27), new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2027, 6, 25),  new Money(15_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2027, 6, 25),  new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2027, 12, 22), new Money(7_500m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2027, 12, 22), new Money(250_000m, Moeda.Usd)),
        };
    }

    // ── Teste 1: Saldo em 2026-05-07, nenhum pagamento efetuado ─────────────

    [Fact]
    public void Calcular_SaldoEm20260507_NenhumPagamento_ResultadoCorreto()
    {
        // Arrange
        LocalDate dataReferencia = new LocalDate(2026, 5, 7);

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: CriarEventosPrevisto());

        // Dias de 2026-01-01 a 2026-05-07 = 126 dias (NodaTime Period.Between conta d2-d1)
        // Juros = 1000000 × 0.06 × 126 / 360 = 21000.00 (HalfUp 2dp)
        decimal jurosEsperados = Math.Round(1_000_000m * 0.06m * 126m / 360m, 2, MidpointRounding.AwayFromZero);

        // Act
        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        // Assert
        resultado.SaldoPrincipalAberto.Valor.Should().Be(1_000_000m);
        resultado.SaldoPrincipalAberto.Moeda.Should().Be(Moeda.Usd);
        resultado.JurosProvisionados.Valor.Should().Be(jurosEsperados);
        resultado.ComissoesAPagar.Valor.Should().Be(0m);
        resultado.SaldoTotal.Valor.Should().Be(1_000_000m + jurosEsperados);
        resultado.SaldoTotal.Moeda.Should().Be(Moeda.Usd);
    }

    // ── Teste 2: Saldo em 2026-09-01, após 1º evento pago (jun/2026) ─────────

    [Fact]
    public void Calcular_SaldoEm20260901_PrimeiroPagamentoPago_ResultadoCorreto()
    {
        // Arrange
        LocalDate dataReferencia = new LocalDate(2026, 9, 1);
        LocalDate dataPrimeiroPagamento = new LocalDate(2026, 6, 30);

        IReadOnlyList<EventoSaldoItem> eventos = new List<EventoSaldoItem>
        {
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Pago,     dataPrimeiroPagamento, new Money(30_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Pago,     dataPrimeiroPagamento, new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 27), new Money(22_500m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 27), new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2027, 6, 25),  new Money(15_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2027, 6, 25),  new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2027, 12, 22), new Money(7_500m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2027, 12, 22), new Money(250_000m, Moeda.Usd)),
        };

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: eventos);

        // Principal aberto = 1000000 - 250000 = 750000
        // Último pagamento de juros = 2026-06-30
        // Dias de 2026-06-30 a 2026-09-01 = 63 dias
        // Juros = 750000 × 0.06 × 63 / 360 = 7875.00
        decimal jurosEsperados = Math.Round(750_000m * 0.06m * 63m / 360m, 2, MidpointRounding.AwayFromZero);

        // Act
        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        // Assert
        resultado.SaldoPrincipalAberto.Valor.Should().Be(750_000m);
        resultado.JurosProvisionados.Valor.Should().Be(jurosEsperados);
        resultado.ComissoesAPagar.Valor.Should().Be(0m);
        resultado.SaldoTotal.Valor.Should().Be(750_000m + jurosEsperados);
    }

    // ── Teste 3: Invariante — SaldoTotal = Principal + Juros + Comissoes ──────

    [Fact]
    public void Calcular_SaldoTotal_SempreIgualSomaDosComponentes()
    {
        // Arrange
        LocalDate dataReferencia = new LocalDate(2026, 4, 1);

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: CriarEventosPrevisto());

        // Act
        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        // Assert — SaldoTotal = SaldoPrincipalAberto + JurosProvisionados + ComissoesAPagar
        decimal somaComponentes = resultado.SaldoPrincipalAberto.Valor
            + resultado.JurosProvisionados.Valor
            + resultado.ComissoesAPagar.Valor;

        resultado.SaldoTotal.Valor.Should().Be(somaComponentes);
    }

    // ── Teste 4: DataReferencia anterior ao DataDesembolso lança exceção ──────

    [Fact]
    public void Calcular_DataReferenciaAnteriorAoDesembolso_LancaArgumentException()
    {
        // Arrange
        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: new LocalDate(2026, 6, 1),
            DataReferencia: new LocalDate(2026, 5, 1), // anterior
            Eventos: new List<EventoSaldoItem>());

        // Act
        System.Action act = () => CalculadorSaldo.Calcular(entrada);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 5 (BRL A): Saldo em 2026-03-02 com PTAX 5.30 ──────────────────
    // 60 dias de 2026-01-01 a 2026-03-02
    // Juros = 1,000,000 × 0.06 × 60 / 360 = 10,000.00 USD
    // SaldoPrincipalAbertoBrl = 1,000,000 × 5.30 = 5,300,000.00
    // JurosProvisionadosBrl   = 10,000 × 5.30     =    53,000.00
    // SaldoTotalBrl           = 1,010,000 × 5.30  = 5,353,000.00

    [Fact]
    public void Calcular_BrlSaldoEm20260302_ComPtax530_ResultadoCorreto()
    {
        // Arrange
        LocalDate dataReferencia = new LocalDate(2026, 3, 2);
        decimal taxaCambio = 5.30m;

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: CriarEventosPrevisto(),
            TaxaCambio: taxaCambio);

        // Act
        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        // Assert — USD components
        resultado.SaldoPrincipalAberto.Valor.Should().Be(1_000_000m);
        resultado.JurosProvisionados.Valor.Should().Be(10_000m);
        resultado.ComissoesAPagar.Valor.Should().Be(0m);

        // Assert — BRL components
        resultado.SaldoPrincipalAbertoBrl.Should().NotBeNull();
        resultado.SaldoPrincipalAbertoBrl!.Value.Moeda.Should().Be(Moeda.Brl);
        resultado.SaldoPrincipalAbertoBrl!.Value.Valor.Should().Be(5_300_000m);

        resultado.JurosProvisionadosBrl.Should().NotBeNull();
        resultado.JurosProvisionadosBrl!.Value.Valor.Should().Be(53_000m);

        resultado.ComissoesAPagarBrl.Should().NotBeNull();
        resultado.ComissoesAPagarBrl!.Value.Valor.Should().Be(0m);

        resultado.SaldoTotalBrl.Should().NotBeNull();
        resultado.SaldoTotalBrl!.Value.Valor.Should().Be(5_353_000m);
    }

    // ── Teste 6 (BRL B): Saldo em 2026-09-15 após pagamento em jun/2026 ──────
    // Principal aberto = 750,000
    // 77 dias de 2026-06-30 a 2026-09-15
    // Juros = 750,000 × 0.06 × 77 / 360 = 9,625.00 USD
    // SaldoTotal USD = 759,625.00
    // SaldoTotalBrl = 759,625 × 5.30 = 4,026,012.50

    [Fact]
    public void Calcular_BrlSaldoEm20260915_AposPrimeiroPagamento_ResultadoCorreto()
    {
        // Arrange
        LocalDate dataReferencia = new LocalDate(2026, 9, 15);
        LocalDate dataPrimeiroPagamento = new LocalDate(2026, 6, 30);
        decimal taxaCambio = 5.30m;

        IReadOnlyList<EventoSaldoItem> eventos = new List<EventoSaldoItem>
        {
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Pago,     dataPrimeiroPagamento, new Money(30_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Pago,     dataPrimeiroPagamento, new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 27), new Money(22_500m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 27), new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2027, 6, 25),  new Money(15_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2027, 6, 25),  new Money(250_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,     StatusEventoCronograma.Previsto, new LocalDate(2027, 12, 22), new Money(7_500m, Moeda.Usd)),
            new(TipoEventoCronograma.Principal, StatusEventoCronograma.Previsto, new LocalDate(2027, 12, 22), new Money(250_000m, Moeda.Usd)),
        };

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: eventos,
            TaxaCambio: taxaCambio);

        decimal jurosEsperados = Math.Round(750_000m * 0.06m * 77m / 360m, 2, MidpointRounding.AwayFromZero);

        // Act
        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        // Assert — USD components
        resultado.SaldoPrincipalAberto.Valor.Should().Be(750_000m);
        resultado.JurosProvisionados.Valor.Should().Be(jurosEsperados);
        resultado.ComissoesAPagar.Valor.Should().Be(0m);

        // Assert — BRL components
        resultado.SaldoPrincipalAbertoBrl.Should().NotBeNull();
        resultado.SaldoPrincipalAbertoBrl!.Value.Moeda.Should().Be(Moeda.Brl);
        resultado.SaldoPrincipalAbertoBrl!.Value.Valor.Should().Be(750_000m * taxaCambio);

        resultado.JurosProvisionadosBrl.Should().NotBeNull();
        resultado.JurosProvisionadosBrl!.Value.Valor.Should().Be(jurosEsperados * taxaCambio);

        resultado.SaldoTotalBrl.Should().NotBeNull();
        resultado.SaldoTotalBrl!.Value.Valor.Should().Be(4_026_012.50m);
    }

    // ── Teste 7: Comissão a pagar (IOF câmbio, status Previsto) inclusa no saldo

    [Fact]
    public void Calcular_ComComissaoIofPrevista_IncluiNoSaldoTotal()
    {
        LocalDate dataReferencia = new LocalDate(2026, 3, 1);

        IReadOnlyList<EventoSaldoItem> eventos = new List<EventoSaldoItem>
        {
            new(TipoEventoCronograma.Principal,  StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 31), new Money(1_000_000m, Moeda.Usd)),
            new(TipoEventoCronograma.Juros,      StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 31), new Money(60_000m, Moeda.Usd)),
            new(TipoEventoCronograma.IofCambio,  StatusEventoCronograma.Previsto, new LocalDate(2026, 1, 1),   new Money(3_800m, Moeda.Usd)),
        };

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: eventos);

        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        resultado.ComissoesAPagar.Valor.Should().Be(3_800m);
        resultado.SaldoTotal.Valor.Should().Be(
            resultado.SaldoPrincipalAberto.Valor +
            resultado.JurosProvisionados.Valor +
            resultado.ComissoesAPagar.Valor);
    }

    // ── Teste 8: Evento com status Atrasado inclui comissão no saldo ──────────

    [Fact]
    public void Calcular_ComComissaoAtrasada_IncluiNoSaldoTotal()
    {
        LocalDate dataReferencia = new LocalDate(2026, 4, 1);

        IReadOnlyList<EventoSaldoItem> eventos = new List<EventoSaldoItem>
        {
            new(TipoEventoCronograma.Principal,    StatusEventoCronograma.Previsto, new LocalDate(2026, 12, 31), new Money(1_000_000m, Moeda.Usd)),
            new(TipoEventoCronograma.ComissaoSblc, StatusEventoCronograma.Atrasado, new LocalDate(2026, 2, 28),  new Money(5_000m, Moeda.Usd)),
        };

        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: PrincipalInicial,
            TaxaAa: TaxaAa,
            BaseCalculo: Base,
            DataDesembolso: DataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: eventos);

        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        resultado.ComissoesAPagar.Valor.Should().Be(5_000m);
        resultado.SaldoTotal.Valor.Should().BeGreaterThan(1_000_000m + 5_000m);
    }
}
