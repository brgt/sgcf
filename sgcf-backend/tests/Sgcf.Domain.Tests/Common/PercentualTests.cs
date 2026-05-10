using FluentAssertions;

using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Common;

[Trait("Category", "Domain")]
public sealed class PercentualTests
{
    [Fact]
    public void De_SixPercent_AsDecimalIsPointZeroSix()
    {
        var p = Percentual.De(6m);
        p.AsDecimal.Should().Be(0.06m);
    }

    [Fact]
    public void De_SixPercent_AsHumanoIsSix()
    {
        var p = Percentual.De(6m);
        p.AsHumano.Should().Be(6m);
    }

    [Fact]
    public void De_Zero_AsDecimalIsZero()
    {
        var p = Percentual.De(0m);
        p.AsDecimal.Should().Be(0m);
    }

    [Fact]
    public void De_Negative_ThrowsArgumentOutOfRangeException()
    {
        var act = () => Percentual.De(-1m);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("percentualHumano");
    }

    [Fact]
    public void DeFracao_PointZeroSix_AsHumanoIsSix()
    {
        var p = Percentual.DeFracao(0.06m);
        p.AsHumano.Should().Be(6m);
    }

    [Fact]
    public void DeFracao_PointZeroSix_AsDecimalIsPointZeroSix()
    {
        var p = Percentual.DeFracao(0.06m);
        p.AsDecimal.Should().Be(0.06m);
    }

    [Fact]
    public void DeFracao_Negative_ThrowsArgumentOutOfRangeException()
    {
        var act = () => Percentual.DeFracao(-0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fracao");
    }

    [Fact]
    public void De_And_DeFracao_ProduceSameResult()
    {
        var fromHumano = Percentual.De(12.5m);
        var fromFracao = Percentual.DeFracao(0.125m);

        fromHumano.AsDecimal.Should().Be(fromFracao.AsDecimal);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = Percentual.De(6m);
        var b = Percentual.De(6m);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Percentual.De(6m);
        var b = Percentual.De(7m);

        a.Should().NotBe(b);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var p = Percentual.De(6m);
        // AsHumano = 6m → "6,0000%" or "6.0000%" depending on culture — format F4
        p.ToString().Should().EndWith("%");
        p.ToString().Should().Contain("6");
    }

    [Fact]
    public void De_OneHundredPercent_AsDecimalIsOne()
    {
        var p = Percentual.De(100m);
        p.AsDecimal.Should().Be(1m);
    }

    [Fact]
    public void De_FractionalPercent_PreservesDecimalPrecision()
    {
        // 0.5% = 0.005 fracional
        var p = Percentual.De(0.5m);
        p.AsDecimal.Should().Be(0.005m);
    }
}
