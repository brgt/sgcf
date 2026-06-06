using FluentAssertions;

using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

[Trait("Category", "Domain")]
public sealed class IndexadorBaseTests
{
    [Fact]
    public void UnidadePrazo_tem_valores_estaveis()
    {
        ((int)UnidadePrazo.Dias).Should().Be(1);
        ((int)UnidadePrazo.Meses).Should().Be(2);
    }

    [Fact]
    public void TipoIndexador_cobre_os_oito_tipos_da_spec()
    {
        Enum.GetNames<TipoIndexador>().Should().BeEquivalentTo(
            "CdiPercentual", "CdiMaisSpread", "Prefixado", "Tlp",
            "Ipca", "Selic", "Sofr", "Euribor");
    }

    [Fact]
    public void Indexador_ausente_eh_coerente()
    {
        new IndexadorBase().EhCoerente().Should().BeTrue();
    }

    [Theory]
    [InlineData(TipoIndexador.CdiPercentual)]
    public void CdiPercentual_exige_percentualCdi(TipoIndexador tipo)
    {
        new IndexadorBase { Tipo = tipo, PercentualCdi = 112.5m }.EhCoerente().Should().BeTrue();
        new IndexadorBase { Tipo = tipo }.EhCoerente().Should().BeFalse();
    }

    [Fact]
    public void Prefixado_exige_taxaPrefixadaAa()
    {
        new IndexadorBase { Tipo = TipoIndexador.Prefixado, TaxaPrefixadaAa = 9.5m }.EhCoerente().Should().BeTrue();
        new IndexadorBase { Tipo = TipoIndexador.Prefixado }.EhCoerente().Should().BeFalse();
    }

    [Theory]
    [InlineData(TipoIndexador.CdiMaisSpread)]
    [InlineData(TipoIndexador.Sofr)]
    [InlineData(TipoIndexador.Euribor)]
    [InlineData(TipoIndexador.Tlp)]
    [InlineData(TipoIndexador.Ipca)]
    public void Tipos_com_spread_exigem_spreadAa(TipoIndexador tipo)
    {
        new IndexadorBase { Tipo = tipo, SpreadAa = 2.75m }.EhCoerente().Should().BeTrue();
        new IndexadorBase { Tipo = tipo }.EhCoerente().Should().BeFalse();
    }

    [Fact]
    public void Selic_eh_coerente_sem_campo_numerico()
    {
        new IndexadorBase { Tipo = TipoIndexador.Selic }.EhCoerente().Should().BeTrue();
    }
}
