using FluentAssertions;
using Sgcf.Domain.Hedge;
using Xunit;

namespace Sgcf.GoldenDataset.Hedge;

/// <summary>
/// Testes de regressão para cálculos MTM de NDF. Dados autoritativos do Annexo A §2.1 e §2.2.
/// Não altere os valores esperados sem aprovação de negócio.
/// </summary>
[Trait("Category", "Golden")]
public sealed class NdfMtmGoldenTests
{
    // ── Forward (§2.2): USD 200k, strike 5.50 ────────────────────────────────

    [Fact]
    public void Forward_SpotBaixo_PayoffNegativo()
    {
        decimal payoff = NdfMtmCalculador.CalcularMtmForward(200_000m, 5.50m, 4.80m);
        payoff.Should().Be(-140_000m);
    }

    [Fact]
    public void Forward_SpotIgualStrike_PayoffZero()
    {
        decimal payoff = NdfMtmCalculador.CalcularMtmForward(200_000m, 5.50m, 5.50m);
        payoff.Should().Be(0m);
    }

    [Fact]
    public void Forward_SpotAlto_PayoffPositivo()
    {
        decimal payoff = NdfMtmCalculador.CalcularMtmForward(200_000m, 5.50m, 5.80m);
        payoff.Should().Be(60_000m);
    }

    // ── Collar (§2.1): USD 200k, put 5.10, call 5.40 ────────────────────────

    [Fact]
    public void Collar_CenarioA_SpotAbaixoPut_PayoffNegativo()
    {
        decimal payoff = NdfMtmCalculador.CalcularMtmCollar(200_000m, 5.10m, 5.40m, 4.80m);
        payoff.Should().Be(-60_000m);
    }

    [Fact]
    public void Collar_CenarioB_SpotDentroDaBanda_PayoffZero()
    {
        decimal payoff = NdfMtmCalculador.CalcularMtmCollar(200_000m, 5.10m, 5.40m, 5.25m);
        payoff.Should().Be(0m);
    }

    [Fact]
    public void Collar_CenarioC_SpotAcimaCall_PayoffPositivo()
    {
        decimal payoff = NdfMtmCalculador.CalcularMtmCollar(200_000m, 5.10m, 5.40m, 5.80m);
        payoff.Should().Be(80_000m);
    }
}
