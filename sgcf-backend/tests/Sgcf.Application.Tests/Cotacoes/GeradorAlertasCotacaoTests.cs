using FluentAssertions;

using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Services;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Alertas suaves de faixa de prazo — SPEC S40 §4.4. Faixas são provisórias e não bloqueiam.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GeradorAlertasCotacaoTests
{
    [Theory]
    [InlineData(ModalidadeContrato.Fgi, 3600, true)]   // 120 meses > 84 meses esperado
    [InlineData(ModalidadeContrato.Fgi, 1800, false)]  // 60 meses dentro da faixa
    [InlineData(ModalidadeContrato.Finimp, 180, false)]
    [InlineData(ModalidadeContrato.Finimp, 4000, true)] // > ~10 anos
    public void Faixa_de_prazo_gera_alerta_apenas_quando_excede(
        ModalidadeContrato modalidade, int dias, bool esperaAlerta)
    {
        List<AlertaDto> alertas = [];

        GeradorAlertasCotacao.AdicionarAlertaFaixaPrazo(alertas, modalidade, dias);

        alertas.Any(a => a.Codigo == CodigosAlerta.PrazoForaDaFaixaEsperada).Should().Be(esperaAlerta);
    }
}
