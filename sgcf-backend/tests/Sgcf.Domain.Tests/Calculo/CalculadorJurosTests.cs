using FluentAssertions;

using FsCheck;
using FsCheck.Xunit;

using NodaTime;

using Sgcf.Domain.Calculo;
using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Calculo;

[Trait("Category", "Domain")]
public sealed class CalculadorJurosTests
{
    // -------------------------------------------------------------------------
    // Golden test — Contrato 4131 BB (Anexo B §6.6)
    // Principal: USD 1.000.000 @ 6% a.a. base 360 por 60 dias
    // Esperado: USD 10.000,00 (= 1.000.000 × 0,06 × 60/360)
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Golden")]
    public void CalcularJurosProRata_4131BB_60Dias_RetornaUsd10000()
    {
        var principal = new Money(1_000_000m, Moeda.Usd);
        var taxa = Percentual.De(6m);

        var juros = CalculadorJuros.CalcularJurosProRata(principal, taxa, 60, BaseCalculo.Dias360);

        juros.Should().Be(new Money(10_000m, Moeda.Usd));
    }

    // -------------------------------------------------------------------------
    // Casos extremos / guarda-chuvas
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularJurosProRata_DiasZero_RetornaZero()
    {
        var principal = new Money(1_000_000m, Moeda.Usd);
        var taxa = Percentual.De(6m);

        var juros = CalculadorJuros.CalcularJurosProRata(principal, taxa, 0, BaseCalculo.Dias360);

        juros.Should().Be(Money.Zero(Moeda.Usd));
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularJurosProRata_DiasNegativos_LancaArgumentOutOfRange()
    {
        var principal = new Money(1_000_000m, Moeda.Usd);
        var taxa = Percentual.De(6m);

        var act = () => CalculadorJuros.CalcularJurosProRata(principal, taxa, -1, BaseCalculo.Dias360);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("diasDecorridos");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularJurosProRata_BaseCalculo365_CalculoCorreto()
    {
        // 1.000.000 USD @ 6% × 182/365 = 29.917,808219178...
        // HalfUp 6dp → 29.917,808219
        var principal = new Money(1_000_000m, Moeda.Usd);
        var taxa = Percentual.De(6m);

        var juros = CalculadorJuros.CalcularJurosProRata(principal, taxa, 182, BaseCalculo.Dias365);

        // 1_000_000 × 0.06 × 182 / 365 = 10920 / 365 = 29917.808219178...
        var esperado = new Money(1_000_000m * 0.06m * 182m / 365m, Moeda.Usd);
        juros.Should().Be(esperado);
    }

    // -------------------------------------------------------------------------
    // IRRF Gross-Up
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularIrrfGrossUp_Aliquota15Pct_CalculaCorretamente()
    {
        // IRRF efetivo = 100 × 0,15 / (1 − 0,15) = 15 / 0,85 = 17,647059...
        var juros = new Money(100m, Moeda.Usd);
        var aliquota = Percentual.De(15m);

        var irrf = CalculadorJuros.CalcularIrrfGrossUp(juros, aliquota);

        // 100 × (0.15 / 0.85) = 17.647058823...  → HalfUp 6dp = 17.647059
        var esperado = new Money(100m * (0.15m / 0.85m), Moeda.Usd);
        irrf.Should().Be(esperado);
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularIrrfGrossUp_AliquotaZero_LancaArgumentOutOfRange()
    {
        var juros = new Money(100m, Moeda.Usd);
        var aliquota = Percentual.De(0m);

        var act = () => CalculadorJuros.CalcularIrrfGrossUp(juros, aliquota);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("aliquotaIrrf");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularIrrfGrossUp_Aliquota100Pct_LancaArgumentOutOfRange()
    {
        var juros = new Money(100m, Moeda.Usd);
        var aliquota = Percentual.De(100m);

        var act = () => CalculadorJuros.CalcularIrrfGrossUp(juros, aliquota);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("aliquotaIrrf");
    }

    // -------------------------------------------------------------------------
    // CalcularJurosEntreDatas
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularJurosEntreDatas_DesembolsoAVencimento_CalculaCorreto()
    {
        // 01/jan/2026 → 02/mar/2026 = 60 dias corridos
        var principal = new Money(1_000_000m, Moeda.Usd);
        var taxa = Percentual.De(6m);
        var inicio = new LocalDate(2026, 1, 1);
        var fim = new LocalDate(2026, 3, 2);

        var juros = CalculadorJuros.CalcularJurosEntreDatas(principal, taxa, inicio, fim, BaseCalculo.Dias360);

        juros.Should().Be(new Money(10_000m, Moeda.Usd));
    }

    // -------------------------------------------------------------------------
    // Property-based tests (FsCheck)
    // -------------------------------------------------------------------------

    [Property]
    [Trait("Category", "PropertyBased")]
    public Property JurosProRata_SempreMaiorIgualZero(PositiveInt principal, PositiveInt diasPos)
    {
        var p = new Money(principal.Get, Moeda.Usd);
        var taxa = Percentual.De(5m);
        int dias = diasPos.Get % 3650; // max 10 anos
        var juros = CalculadorJuros.CalcularJurosProRata(p, taxa, dias, BaseCalculo.Dias360);
        return (juros.Valor >= 0).ToProperty();
    }

    [Property]
    [Trait("Category", "PropertyBased")]
    public Property JurosProRata_ProporcionaisAosPrincipais(PositiveInt mult)
    {
        var p1 = new Money(1000m, Moeda.Usd);
        var p2 = new Money(1000m * mult.Get, Moeda.Usd);
        var taxa = Percentual.De(6m);
        var j1 = CalculadorJuros.CalcularJurosProRata(p1, taxa, 30, BaseCalculo.Dias360);
        var j2 = CalculadorJuros.CalcularJurosProRata(p2, taxa, 30, BaseCalculo.Dias360);

        // j2 deve ser mult vezes j1 (dentro de tolerância de arredondamento HalfUp 6dp)
        if (j1.Valor == 0m)
        {
            return (j2.Valor == 0m).ToProperty();
        }

        decimal ratio = j2.Valor / j1.Valor;
        return (Math.Abs(ratio - mult.Get) < 0.000001m).ToProperty();
    }
}
