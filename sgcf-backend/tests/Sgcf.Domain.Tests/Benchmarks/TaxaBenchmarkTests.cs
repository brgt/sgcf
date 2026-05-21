using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Benchmarks;
using Xunit;

namespace Sgcf.Domain.Tests.Benchmarks;

/// <summary>
/// Testes unitários para TaxaBenchmark. GAP-CKP-14.
/// </summary>
public sealed class TaxaBenchmarkTests
{
    private static readonly Instant AgoraFixo =
        Instant.FromUtc(2025, 1, 15, 12, 0, 0);

    private static readonly LocalDate DataFixa = new(2025, 1, 15);

    [Fact]
    public void Criar_ComParametrosValidos_RetornaEntidade()
    {
        TaxaBenchmark taxa = TaxaBenchmark.Criar("Selic", DataFixa, 0.1075m, "BCB", AgoraFixo);

        taxa.TipoBenchmark.Should().Be("Selic");
        taxa.DataReferencia.Should().Be(DataFixa);
        taxa.TaxaAa.Should().Be(0.1075m);
        taxa.Fonte.Should().Be("BCB");
        taxa.RegistradoEm.Should().Be(AgoraFixo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_ComTipoVazio_LancaArgumentException(string tipo)
    {
        Action act = () => TaxaBenchmark.Criar(tipo, DataFixa, 0.1m, "BCB", AgoraFixo);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ComTaxaNegativa_LancaArgumentOutOfRangeException()
    {
        Action act = () => TaxaBenchmark.Criar("Selic", DataFixa, -0.01m, "BCB", AgoraFixo);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Atualizar_ComNovosDados_AlteraPropriedades()
    {
        TaxaBenchmark taxa = TaxaBenchmark.Criar("Selic", DataFixa, 0.10m, "BCB", AgoraFixo);
        Instant novoInstante = AgoraFixo.Plus(Duration.FromHours(1));

        taxa.Atualizar(0.1075m, "Manual", novoInstante);

        taxa.TaxaAa.Should().Be(0.1075m);
        taxa.Fonte.Should().Be("Manual");
        taxa.RegistradoEm.Should().Be(novoInstante);
    }

    [Fact]
    public void TaxaAa_ZeroEhValido()
    {
        TaxaBenchmark taxa = TaxaBenchmark.Criar("Sofr", DataFixa, 0m, "FED", AgoraFixo);

        taxa.TaxaAa.Should().Be(0m);
    }
}
