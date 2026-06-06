using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Services;

/// <summary>
/// Resolve a precedência e a coexistência do tenor de prazo (SPEC S40 §4.1, §4.2).
/// Função pura: dado o que o cliente enviou, produz o par {valor, unidade} canônico e o dia derivado,
/// além de um eventual alerta de recálculo. Não acessa I/O.
/// </summary>
public static class ResolvedorTenor
{
    /// <param name="Valor">Valor do tenor persistido (intenção).</param>
    /// <param name="Unidade">Unidade do tenor persistido.</param>
    /// <param name="Dias">Dia canônico derivado (30/360).</param>
    /// <param name="Alerta">Alerta de recálculo, quando o cliente enviou dias divergentes do tenor.</param>
    public sealed record Resultado(int Valor, UnidadePrazo Unidade, int Dias, AlertaDto? Alerta);

    /// <summary>Unidade default por modalidade — SPEC S40 §4.2.</summary>
    public static UnidadePrazo UnidadeDefault(ModalidadeContrato modalidade) => modalidade switch
    {
        ModalidadeContrato.Finimp or ModalidadeContrato.Refinimp => UnidadePrazo.Dias,
        _ => UnidadePrazo.Meses,
    };

    /// <summary>
    /// Aplica a ordem de precedência: o par {valor, unidade} prevalece sobre o dia legado;
    /// na ausência do valor, usa o dia como Dias; sem nenhum, lança (o validator já barra em POST).
    /// </summary>
    public static Resultado Resolver(
        ModalidadeContrato modalidade,
        int? prazoMaximoValor,
        UnidadePrazo? prazoMaximoUnidade,
        int? prazoMaximoDias)
    {
        if (prazoMaximoValor is { } valor)
        {
            UnidadePrazo unidade = prazoMaximoUnidade ?? UnidadeDefault(modalidade);
            int dias = Cotacao.DerivarPrazoMaximoDias(valor, unidade);

            AlertaDto? alerta = null;
            if (prazoMaximoDias is { } diasEnviado && diasEnviado != dias)
            {
                alerta = new AlertaDto(
                    CodigosAlerta.PrazoRecalculado,
                    "prazoMaximoValor",
                    SeveridadeAlertaCotacao.Aviso,
                    $"prazoMaximoDias enviado ({diasEnviado}) diverge do tenor {valor} {unidade}; " +
                    $"recalculado para {dias} dias (30/360).");
            }

            return new Resultado(valor, unidade, dias, alerta);
        }

        if (prazoMaximoDias is { } diasLegado)
        {
            int dias = Cotacao.DerivarPrazoMaximoDias(diasLegado, UnidadePrazo.Dias);
            return new Resultado(diasLegado, UnidadePrazo.Dias, dias, null);
        }

        throw new ArgumentException(
            "Prazo máximo é obrigatório: informe prazoMaximoValor (com unidade) ou prazoMaximoDias.");
    }
}
