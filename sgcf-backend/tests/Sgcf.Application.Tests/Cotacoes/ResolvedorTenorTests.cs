using FluentAssertions;

using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Services;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Precedência e coexistência do tenor de prazo — SPEC S40 §4.1, §4.2.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResolvedorTenorTests
{
    [Fact]
    public void Valor_e_unidade_explicitos_derivam_dias()
    {
        ResolvedorTenor.Resultado r = ResolvedorTenor.Resolver(
            ModalidadeContrato.Lei4131, prazoMaximoValor: 60, prazoMaximoUnidade: UnidadePrazo.Meses, prazoMaximoDias: null);

        r.Valor.Should().Be(60);
        r.Unidade.Should().Be(UnidadePrazo.Meses);
        r.Dias.Should().Be(1800);
        r.Alerta.Should().BeNull();
    }

    [Fact]
    public void Unidade_ausente_usa_default_da_modalidade()
    {
        ResolvedorTenor.Resolver(ModalidadeContrato.Lei4131, 60, null, null).Unidade.Should().Be(UnidadePrazo.Meses);
        ResolvedorTenor.Resolver(ModalidadeContrato.Finimp, 180, null, null).Unidade.Should().Be(UnidadePrazo.Dias);
    }

    [Theory]
    [InlineData(ModalidadeContrato.Finimp, UnidadePrazo.Dias)]
    [InlineData(ModalidadeContrato.Refinimp, UnidadePrazo.Dias)]
    [InlineData(ModalidadeContrato.Lei4131, UnidadePrazo.Meses)]
    [InlineData(ModalidadeContrato.Nce, UnidadePrazo.Meses)]
    [InlineData(ModalidadeContrato.CapitalDeGiro, UnidadePrazo.Meses)]
    [InlineData(ModalidadeContrato.Fgi, UnidadePrazo.Meses)]
    public void UnidadeDefault_por_modalidade(ModalidadeContrato modalidade, UnidadePrazo esperada)
    {
        ResolvedorTenor.UnidadeDefault(modalidade).Should().Be(esperada);
    }

    [Fact]
    public void Caminho_legado_so_dias_assume_unidade_dias()
    {
        ResolvedorTenor.Resultado r = ResolvedorTenor.Resolver(
            ModalidadeContrato.Finimp, prazoMaximoValor: null, prazoMaximoUnidade: null, prazoMaximoDias: 180);

        r.Valor.Should().Be(180);
        r.Unidade.Should().Be(UnidadePrazo.Dias);
        r.Dias.Should().Be(180);
        r.Alerta.Should().BeNull();
    }

    [Fact]
    public void Tenor_prevalece_e_emite_alerta_quando_dias_diverge()
    {
        ResolvedorTenor.Resultado r = ResolvedorTenor.Resolver(
            ModalidadeContrato.Lei4131, prazoMaximoValor: 24, prazoMaximoUnidade: UnidadePrazo.Meses, prazoMaximoDias: 999);

        r.Dias.Should().Be(720, "o par estruturado é a fonte de verdade");
        r.Alerta.Should().NotBeNull();
        r.Alerta!.Codigo.Should().Be("prazo-recalculado");
        r.Alerta.Severidade.Should().Be(SeveridadeAlertaCotacao.Aviso);
    }

    [Fact]
    public void Sem_nenhum_prazo_lanca()
    {
        var act = () => ResolvedorTenor.Resolver(ModalidadeContrato.Finimp, null, null, null);
        act.Should().Throw<ArgumentException>();
    }
}
