using FluentAssertions;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes unitários para o campo TaxaIndicativaAa em Proposta. GAP-CKP-19.
/// </summary>
public sealed class PropostaTaxaIndicativaTests
{
    [Fact]
    public void Criar_ComTaxaIndicativa_ArmazenaCorretamente()
    {
        Proposta proposta = PropostaFactory.CriarProposta(taxaIndicativaAa: 0.065m);

        proposta.TaxaIndicativaAa.Should().Be(0.065m);
    }

    [Fact]
    public void Criar_SemTaxaIndicativa_PropriedadeNula()
    {
        Proposta proposta = PropostaFactory.CriarProposta();

        proposta.TaxaIndicativaAa.Should().BeNull();
    }
}
