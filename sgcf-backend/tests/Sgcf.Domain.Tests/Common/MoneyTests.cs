using FluentAssertions;

using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Common;

[Trait("Category", "Domain")]
public sealed class MoneyTests
{
    [Fact]
    public void Constructor_NormalizesTo6DecimalPlaces()
    {
        var money = new Money(100.1234567m, Moeda.Brl);
        // 100.1234567 arredondado para 6 casas HalfUp → 100.123457
        money.Valor.Should().Be(100.123457m);
        money.Moeda.Should().Be(Moeda.Brl);
    }

    [Fact]
    public void Constructor_ExactValue_StoresAsIs()
    {
        var money = new Money(100.123456m, Moeda.Brl);
        money.Valor.Should().Be(100.123456m);
    }

    [Fact]
    public void Somar_SameMoeda_ReturnsSum()
    {
        var a = new Money(100m, Moeda.Brl);
        var b = new Money(200m, Moeda.Brl);

        var resultado = a.Somar(b);

        resultado.Valor.Should().Be(300m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    [Fact]
    public void Somar_DifferentMoeda_ThrowsInvalidOperationException()
    {
        var brl = new Money(100m, Moeda.Brl);
        var usd = new Money(100m, Moeda.Usd);

        var act = () => brl.Somar(usd);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BRL*USD*");
    }

    [Fact]
    public void Multiplicar_ReturnsCorrectValue()
    {
        var money = new Money(1000m, Moeda.Brl);

        var resultado = money.Multiplicar(0.06m);

        resultado.Valor.Should().Be(60.000000m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    [Fact]
    public void Subtrair_SameMoeda_ReturnsDifference()
    {
        var a = new Money(500m, Moeda.Brl);
        var b = new Money(200m, Moeda.Brl);

        var resultado = a.Subtrair(b);

        resultado.Valor.Should().Be(300m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    [Fact]
    public void Subtrair_DifferentMoeda_ThrowsInvalidOperationException()
    {
        var brl = new Money(500m, Moeda.Brl);
        var usd = new Money(200m, Moeda.Usd);

        var act = () => brl.Subtrair(usd);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dividir_ValidDivisor_ReturnsCorrectValue()
    {
        var money = new Money(300m, Moeda.Brl);

        var resultado = money.Dividir(3m);

        resultado.Valor.Should().Be(100m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    [Fact]
    public void Dividir_ZeroDivisor_ThrowsDivideByZeroException()
    {
        var money = new Money(300m, Moeda.Brl);

        var act = () => money.Dividir(0m);

        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void Zero_ReturnsMoneyWithZeroValue()
    {
        var zero = Money.Zero(Moeda.Usd);

        zero.Valor.Should().Be(0m);
        zero.Moeda.Should().Be(Moeda.Usd);
    }

    [Fact]
    public void Equality_SameValorAndMoeda_AreEqual()
    {
        var a = new Money(100m, Moeda.Brl);
        var b = new Money(100m, Moeda.Brl);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValor_AreNotEqual()
    {
        var a = new Money(100m, Moeda.Brl);
        var b = new Money(200m, Moeda.Brl);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentMoeda_AreNotEqual()
    {
        var a = new Money(100m, Moeda.Brl);
        var b = new Money(100m, Moeda.Usd);

        a.Should().NotBe(b);
    }

    [Fact]
    public void MaiorQue_WhenLarger_ReturnsTrue()
    {
        var a = new Money(200m, Moeda.Brl);
        var b = new Money(100m, Moeda.Brl);

        a.MaiorQue(b).Should().BeTrue();
    }

    [Fact]
    public void MaiorQue_WhenSmaller_ReturnsFalse()
    {
        var a = new Money(100m, Moeda.Brl);
        var b = new Money(200m, Moeda.Brl);

        a.MaiorQue(b).Should().BeFalse();
    }

    [Fact]
    public void MenorQue_WhenSmaller_ReturnsTrue()
    {
        var a = new Money(100m, Moeda.Brl);
        var b = new Money(200m, Moeda.Brl);

        a.MenorQue(b).Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var money = new Money(100m, Moeda.Brl);
        money.ToString().Should().Be("BRL 100.000000");
    }

    [Fact]
    public void Multiplicar_WithRounding_AppliesHalfUp()
    {
        // 3 * (1/3) = 1.000000 após arredondamento correto
        var money = new Money(3m, Moeda.Brl);
        var resultado = money.Dividir(3m).Multiplicar(3m);
        resultado.Valor.Should().Be(3m);
    }
}
