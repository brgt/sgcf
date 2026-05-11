using FluentAssertions;

using Sgcf.Domain.Calculo;
using Sgcf.Domain.Common;

using Xunit;

namespace Sgcf.Domain.Tests.Calculo;

[Trait("Category", "Domain")]
public sealed class CalculadorIofTests
{
    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularIof_PrincipalBrl_AliquotaPadrao_RetornaIofCorreto()
    {
        // BRL 1.000.000 × 0,38% = BRL 3.800,00
        var principal = new Money(1_000_000m, Moeda.Brl);

        var iof = CalculadorIof.CalcularIof(principal, CalculadorIof.AliquotaPadrao);

        iof.Should().Be(new Money(3_800m, Moeda.Brl));
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularIof_MoedaNaoBrl_LancaArgumentException()
    {
        // IOF câmbio calcula apenas sobre BRL — USD deve ser convertido antes
        var principal = new Money(1_000_000m, Moeda.Usd);

        var act = () => CalculadorIof.CalcularIof(principal, CalculadorIof.AliquotaPadrao);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("principalBrl");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void AliquotaPadrao_EhZeroVirgulaTrintaEOitoPorcento()
    {
        // Confirma que a constante de alíquota padrão está correta (0,38% vigente desde 2013)
        CalculadorIof.AliquotaPadrao.AsDecimal.Should().Be(0.0038m);
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void CalcularIof_AliquotaZero_RetornaZero()
    {
        var principal = new Money(1_000_000m, Moeda.Brl);
        var aliquotaZero = Percentual.De(0m);

        var iof = CalculadorIof.CalcularIof(principal, aliquotaZero);

        iof.Should().Be(Money.Zero(Moeda.Brl));
    }
}
