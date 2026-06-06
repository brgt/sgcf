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
                "carencia-ignorada",
                "carenciaMeses",
                SeveridadeAlertaCotacao.Info,
                $"carenciaMeses não se aplica à modalidade {modalidade}; o valor informado foi ignorado."));
        }

        if (indexador is not null && !indexador.EhCoerente())
        {
            alertas.Add(new AlertaDto(
                "indexador-incoerente",
                "indexadorBase",
                SeveridadeAlertaCotacao.Aviso,
                $"indexadorBase.tipo={indexador.Tipo} foi informado sem o campo numérico coerente; mantido como recebido."));
        }
    }
}
