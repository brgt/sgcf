using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Domain.Tests.Cronograma;

[Trait("Category", "Domain")]
public sealed class CustomizadaStrategyTests
{
    // ── Teste 1: 2 parcelas simples — eventos ordenados por data ─────────────────

    [Fact]
    public void Gerar_2ParcelasSimples_RetornaEventosOrdenadosPorData()
    {
        EntradaCustomizada entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            Parcelas:
            [
                new ParcelaCustomizada(
                    Numero: 2,
                    DataPrevista: new LocalDate(2026, 8, 1),
                    ValorPrincipal: new Money(50_000m, Moeda.Brl),
                    ValorJuros: new Money(1_500m, Moeda.Brl)),
                new ParcelaCustomizada(
                    Numero: 1,
                    DataPrevista: new LocalDate(2026, 5, 1),
                    ValorPrincipal: new Money(50_000m, Moeda.Brl),
                    ValorJuros: new Money(2_000m, Moeda.Brl)),
            ]);

        IReadOnlyList<EventoGeradoCustomizado> eventos = CustomizadaStrategy.Gerar(entrada);

        // Deve haver 4 eventos: JUROS+PRINCIPAL por parcela
        eventos.Should().HaveCount(4);

        // Devem estar ordenados por data, JUROS antes de PRINCIPAL na mesma data
        LocalDate primeiraData = eventos[0].DataPrevista;
        LocalDate segundaData = eventos[2].DataPrevista;

        (primeiraData < segundaData).Should().BeTrue();
        eventos[0].Tipo.Should().Be(TipoEventoCronograma.Juros);
        eventos[1].Tipo.Should().Be(TipoEventoCronograma.Principal);
    }

    // ── Teste 2: Lista vazia lança ArgumentException ──────────────────────────────

    [Fact]
    public void Gerar_ListaVazia_LancaArgumentException()
    {
        EntradaCustomizada entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            Parcelas: []);

        System.Action act = () => CustomizadaStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 3: ValorPrincipal da entrada zero lança ArgumentException ──────────

    [Fact]
    public void Gerar_ValorPrincipalZero_LancaArgumentException()
    {
        EntradaCustomizada entrada = new(
            ValorPrincipal: new Money(0m, Moeda.Brl),
            Parcelas:
            [
                new ParcelaCustomizada(
                    Numero: 1,
                    DataPrevista: new LocalDate(2026, 6, 1),
                    ValorPrincipal: new Money(0m, Moeda.Brl),
                    ValorJuros: new Money(100m, Moeda.Brl)),
            ]);

        System.Action act = () => CustomizadaStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 4: Última parcela tem SaldoDevedorApos = 0 ─────────────────────────

    [Fact]
    public void Gerar_UltimaParcela_SaldoDevedorAposZero()
    {
        EntradaCustomizada entrada = new(
            ValorPrincipal: new Money(90_000m, Moeda.Brl),
            Parcelas:
            [
                new ParcelaCustomizada(
                    Numero: 1,
                    DataPrevista: new LocalDate(2026, 4, 1),
                    ValorPrincipal: new Money(30_000m, Moeda.Brl),
                    ValorJuros: new Money(900m, Moeda.Brl)),
                new ParcelaCustomizada(
                    Numero: 2,
                    DataPrevista: new LocalDate(2026, 7, 1),
                    ValorPrincipal: new Money(30_000m, Moeda.Brl),
                    ValorJuros: new Money(600m, Moeda.Brl)),
                new ParcelaCustomizada(
                    Numero: 3,
                    DataPrevista: new LocalDate(2026, 10, 1),
                    ValorPrincipal: new Money(30_000m, Moeda.Brl),
                    ValorJuros: new Money(300m, Moeda.Brl)),
            ]);

        IReadOnlyList<EventoGeradoCustomizado> eventos = CustomizadaStrategy.Gerar(entrada);

        EventoGeradoCustomizado ultimoPrincipal = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .OrderByDescending(e => e.DataPrevista)
            .First();

        ultimoPrincipal.SaldoDevedorApos.Should().Be(0m);
    }

    // ── Teste 5: Moeda diferente entre parcela e entrada lança ArgumentException ──

    [Fact]
    public void Gerar_MoedaDiferenteEntreParcelasEEntrada_LancaArgumentException()
    {
        EntradaCustomizada entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            Parcelas:
            [
                new ParcelaCustomizada(
                    Numero: 1,
                    DataPrevista: new LocalDate(2026, 6, 1),
                    ValorPrincipal: new Money(100_000m, Moeda.Usd), // moeda errada
                    ValorJuros: new Money(1_000m, Moeda.Usd)),
            ]);

        System.Action act = () => CustomizadaStrategy.Gerar(entrada);

        act.Should().Throw<ArgumentException>();
    }

    // ── Teste 6: Números de parcela duplicados NÃO lançam exceção ────────────────
    // Dois pagamentos com o mesmo Numero em datas diferentes são válidos
    // (step-down schedules do Balcão Caixa usam esse padrão)

    [Fact]
    public void Gerar_NumerosParcelaDuplicados_NaoLancaExcecao()
    {
        EntradaCustomizada entrada = new(
            ValorPrincipal: new Money(100_000m, Moeda.Brl),
            Parcelas:
            [
                new ParcelaCustomizada(
                    Numero: 1,
                    DataPrevista: new LocalDate(2026, 6, 1),
                    ValorPrincipal: new Money(50_000m, Moeda.Brl),
                    ValorJuros: new Money(500m, Moeda.Brl)),
                new ParcelaCustomizada(
                    Numero: 1,
                    DataPrevista: new LocalDate(2026, 9, 1),
                    ValorPrincipal: new Money(50_000m, Moeda.Brl),
                    ValorJuros: new Money(250m, Moeda.Brl)),
            ]);

        System.Action act = () => CustomizadaStrategy.Gerar(entrada);

        // Dois registros com Numero=1 em datas distintas são aceitos
        act.Should().NotThrow();

        IReadOnlyList<EventoGeradoCustomizado> eventos = CustomizadaStrategy.Gerar(entrada);
        eventos.Should().HaveCount(4);
    }
}
