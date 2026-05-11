using FluentAssertions;

using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Common;

[Trait("Category", "Domain")]
public sealed class ArredondamentoTests
{
    [Theory]
    [InlineData(2.5, 0, 3)]
    [InlineData(3.5, 0, 4)]
    [InlineData(4.5, 0, 5)]
    [InlineData(1.5, 0, 2)]
    public void HalfUp_MidpointValues_RoundsAwayFromZero(double valor, int casas, double esperado)
    {
        decimal resultado = Arredondamento.HalfUp((decimal)valor, casas);
        resultado.Should().Be((decimal)esperado);
    }

    [Fact]
    public void HalfUp_NegativeMidpoint_RoundsAwayFromZero()
    {
        // HalfUp comercial: -2.5 → -3 (away from zero)
        decimal resultado = Arredondamento.HalfUp(-2.5m, 0);
        resultado.Should().Be(-3m);
    }

    [Fact]
    public void HalfUp_TwoDecimalPlaces_RoundsCorrectly()
    {
        // 2.125 com 2 casas → 2.13 (o dígito após a segunda casa é 5, arredonda para cima)
        decimal resultado = Arredondamento.HalfUp(2.125m, 2);
        resultado.Should().Be(2.13m);
    }

    [Fact]
    public void HalfUp_TwoDecimalPlaces_RoundsDown()
    {
        // 2.124 com 2 casas → 2.12
        decimal resultado = Arredondamento.HalfUp(2.124m, 2);
        resultado.Should().Be(2.12m);
    }

    [Fact]
    public void HalfUp_SixDecimalPlaces_RoundsCorrectly()
    {
        // 1.0000005 com 6 casas → 1.000001
        decimal resultado = Arredondamento.HalfUp(1.0000005m, 6);
        resultado.Should().Be(1.000001m);
    }

    [Fact]
    public void HalfUp_ExactValue_NoRounding()
    {
        decimal resultado = Arredondamento.HalfUp(1.25m, 2);
        resultado.Should().Be(1.25m);
    }

    [Fact]
    public void HalfUp_Zero_ReturnsZero()
    {
        decimal resultado = Arredondamento.HalfUp(0m, 6);
        resultado.Should().Be(0m);
    }

    [Fact]
    public void HalfUp_DiffersFromBankersRounding()
    {
        // 2.5 com ToEven (padrão C#) → 2; com HalfUp → 3
        decimal halfUp = Arredondamento.HalfUp(2.5m, 0);
        decimal bankers = Math.Round(2.5m, 0, MidpointRounding.ToEven);

        halfUp.Should().Be(3m);
        bankers.Should().Be(2m);
        halfUp.Should().NotBe(bankers);
    }
}
