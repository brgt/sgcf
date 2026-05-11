using FsCheck;
using FsCheck.Xunit;
using Sgcf.Domain.Hedge;
using Xunit;

namespace Sgcf.Domain.Tests.Hedge;

[Trait("Category", "Domain")]
public sealed class NdfMtmCalculadorTests
{
    /// <summary>
    /// For any spot strictly inside [strikePut, strikeCall], the Collar payoff must be exactly 0.
    /// Uses 100 randomly-generated spots in the range [5.1001, 5.3999] (avoids boundaries).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Collar_SpotDentroDaBanda_PayoffSempreZero()
    {
        // Generate integers in [51001, 53999] and scale to [5.1001, 5.3999]
        // — strictly inside the [5.10, 5.40] collar band.
        Gen<decimal> spotDentroDaBanda =
            Gen.Choose(51001, 53999).Select(static v => (decimal)v / 10000m);

        return Prop.ForAll(
            Arb.From(spotDentroDaBanda),
            static spot => NdfMtmCalculador.CalcularMtmCollar(200_000m, 5.10m, 5.40m, spot) == 0m);
    }
}
