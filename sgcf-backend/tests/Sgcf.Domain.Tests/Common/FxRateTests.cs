using FluentAssertions;

using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Common;

[Trait("Category", "Domain")]
public sealed class FxRateTests
{
    [Fact]
    public void ConverterParaBrl_UsdValue_ReturnsCorrectBrl()
    {
        var rate = new FxRate(5.30m, Moeda.Usd);
        var usd = new Money(1000m, Moeda.Usd);

        var resultado = rate.ConverterParaBrl(usd);

        resultado.Valor.Should().Be(5300m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    [Fact]
    public void ConverterDoBrl_BrlValue_ReturnsCorrectForeignAmount()
    {
        var rate = new FxRate(5.30m, Moeda.Usd);
        var brl = new Money(5300m, Moeda.Brl);

        var resultado = rate.ConverterDoBrl(brl);

        resultado.Valor.Should().Be(1000m);
        resultado.Moeda.Should().Be(Moeda.Usd);
    }

    [Fact]
    public void Constructor_ZeroRate_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new FxRate(0m, Moeda.Usd);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("taxa");
    }

    [Fact]
    public void Constructor_NegativeRate_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new FxRate(-1m, Moeda.Usd);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("taxa");
    }

    [Fact]
    public void Constructor_BrlMoeda_ThrowsArgumentException()
    {
        var act = () => new FxRate(5.30m, Moeda.Brl);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("moeda");
    }

    [Fact]
    public void ConverterParaBrl_WrongMoeda_ThrowsInvalidOperationException()
    {
        var rate = new FxRate(5.30m, Moeda.Usd);
        var eur = new Money(1000m, Moeda.Eur);

        var act = () => rate.ConverterParaBrl(eur);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*USD*EUR*");
    }

    [Fact]
    public void ConverterDoBrl_NonBrlValue_ThrowsInvalidOperationException()
    {
        var rate = new FxRate(5.30m, Moeda.Usd);
        var usd = new Money(1000m, Moeda.Usd);

        var act = () => rate.ConverterDoBrl(usd);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BRL*");
    }

    [Fact]
    public void Constructor_StoresTaxaRoundedTo6Decimals()
    {
        // Taxa com mais de 6 casas é arredondada ao construir
        var rate = new FxRate(5.1234567m, Moeda.Usd);
        rate.Taxa.Should().Be(5.123457m);
    }

    [Fact]
    public void Constructor_StoresMoedaCorrectly()
    {
        var rate = new FxRate(6.50m, Moeda.Eur);
        rate.Moeda.Should().Be(Moeda.Eur);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var rate = new FxRate(5.30m, Moeda.Usd);
        rate.ToString().Should().Be("USD/BRL 5.300000");
    }

    [Fact]
    public void ConverterParaBrl_ThenConverterDoBrl_RoundTrip()
    {
        // Conversão de ida e volta com taxa exata
        var rate = new FxRate(5m, Moeda.Usd);
        var original = new Money(200m, Moeda.Usd);

        var brl = rate.ConverterParaBrl(original);
        var deVolta = rate.ConverterDoBrl(brl);

        deVolta.Valor.Should().Be(original.Valor);
        deVolta.Moeda.Should().Be(original.Moeda);
    }

    [Fact]
    public void ConverterParaBrl_SmallAmount_RoundsCorrectly()
    {
        // 1 USD a 5.333333 = 5.333333 BRL (6 casas exatas)
        var rate = new FxRate(5.333333m, Moeda.Usd);
        var usd = new Money(1m, Moeda.Usd);

        var resultado = rate.ConverterParaBrl(usd);

        resultado.Valor.Should().Be(5.333333m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }
}
