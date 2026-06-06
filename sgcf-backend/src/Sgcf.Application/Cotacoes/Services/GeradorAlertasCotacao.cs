using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Services;

/// <summary>
/// Gera alertas de validação suave (não bloqueantes) sobre os campos de domínio da cotação.
/// Função pura — SPEC S40 §4.5, §4.6. Os códigos são estáveis (contrato com o front-end).
/// </summary>
public static class GeradorAlertasCotacao
{
    private static readonly ModalidadeContrato[] AplicamCarencia =
        [ModalidadeContrato.Lei4131, ModalidadeContrato.Nce, ModalidadeContrato.CapitalDeGiro, ModalidadeContrato.Fgi];

    /// <summary>
    /// Acrescenta a <paramref name="alertas"/> os avisos suaves dos campos de domínio informados
    /// (carência ignorada em modalidade não aplicável; indexador com tipo sem campo numérico coerente).
    /// </summary>
    public static void AdicionarAlertasCamposDominio(
        List<AlertaDto> alertas,
        ModalidadeContrato modalidade,
        int? carenciaMeses,
        IndexadorBase? indexador)
    {
        if (carenciaMeses is not null && !AplicamCarencia.Contains(modalidade))
        {
            alertas.Add(new AlertaDto(
                CodigosAlerta.CarenciaIgnorada,
                "carenciaMeses",
                SeveridadeAlertaCotacao.Info,
                $"carenciaMeses não se aplica à modalidade {modalidade}; o valor informado foi ignorado."));
        }

        if (indexador is not null && !indexador.EhCoerente())
        {
            alertas.Add(new AlertaDto(
                CodigosAlerta.IndexadorIncoerente,
                "indexadorBase",
                SeveridadeAlertaCotacao.Aviso,
                $"indexadorBase.tipo={indexador.Tipo} foi informado sem o campo numérico coerente; mantido como recebido."));
        }
    }

    /// <summary>
    /// Faixa máxima de prazo "esperada" (provisória) por modalidade, em dias. Não é teto rígido:
    /// exceder gera apenas alerta informativo. SPEC S40 §4.4 (política definitiva pendente).
    /// </summary>
    private static int FaixaMaximaEsperadaDias(ModalidadeContrato modalidade) => modalidade switch
    {
        // FGI: 24–84 meses típico; 120 meses é exceção legítima.
        ModalidadeContrato.Fgi => 84 * 30,
        // Demais: prazos de médio/longo prazo comuns; ~10 anos como referência ampla.
        _ => 3650,
    };

    /// <summary>
    /// Acrescenta alerta suave quando o prazo derivado excede a faixa esperada da modalidade. SPEC S40 §4.4.
    /// </summary>
    public static void AdicionarAlertaFaixaPrazo(
        List<AlertaDto> alertas,
        ModalidadeContrato modalidade,
        int prazoMaximoDias)
    {
        int maxEsperado = FaixaMaximaEsperadaDias(modalidade);
        if (prazoMaximoDias > maxEsperado)
        {
            alertas.Add(new AlertaDto(
                CodigosAlerta.PrazoForaDaFaixaEsperada,
                "prazoMaximoValor",
                SeveridadeAlertaCotacao.Info,
                $"prazoMaximoDias={prazoMaximoDias} excede a faixa esperada (~{maxEsperado} dias) para {modalidade}; " +
                "verifique se é intencional."));
        }
    }
}
