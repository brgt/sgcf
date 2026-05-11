using System.Globalization;
using ClosedXML.Excel;
using Sgcf.Application.Contratos;

namespace Sgcf.Api.Export;

/// <summary>
/// Gera o arquivo Excel (XLSX) da tabela completa de um contrato usando ClosedXML.
/// Organiza os dados em abas: Identificação, Cronograma, Garantias e Histórico.
/// </summary>
public sealed class TabelaCompletaExcelExporter
{
    /// <summary>
    /// Gera o XLSX com os blocos da tabela completa distribuídos em abas.
    /// </summary>
    public static byte[] Gerar(TabelaCompletaDto dto)
    {
        using XLWorkbook workbook = new();

        AdicionarAbaIdentificacao(workbook, dto);
        AdicionarAbaCronograma(workbook, dto);
        AdicionarAbaGarantias(workbook, dto);
        AdicionarAbaHistorico(workbook, dto);

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AdicionarAbaIdentificacao(XLWorkbook wb, TabelaCompletaDto dto)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Identificação");

        ws.Cell(1, 1).Value = "Campo";
        ws.Cell(1, 2).Value = "Valor";
        EstilizarCabecalho(ws.Row(1));

        int linha = 2;
        AdicionarLinha(ws, linha++, "Banco", dto.Identificacao.Banco);
        AdicionarLinha(ws, linha++, "Modalidade", dto.Identificacao.Modalidade);
        AdicionarLinha(ws, linha++, "Nº Contrato Externo", dto.Identificacao.NumeroContratoExterno);
        AdicionarLinha(ws, linha++, "Data Contratação", dto.Identificacao.DataContratacao.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        AdicionarLinha(ws, linha++, "Data Vencimento", dto.Identificacao.DataVencimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        AdicionarLinha(ws, linha++, "Status", dto.Identificacao.Status);

        linha++;
        AdicionarLinha(ws, linha++, "Principal Original", dto.ValoresPrincipais.ValorPrincipalOriginal);
        AdicionarLinha(ws, linha++, "Moeda", dto.ValoresPrincipais.MoedaOriginal);

        linha++;
        AdicionarLinha(ws, linha++, "Taxa a.a. (%)", dto.Encargos.TaxaAaPct);
        AdicionarLinha(ws, linha++, "Base de Cálculo", dto.Encargos.BaseCalculo);
        if (dto.Encargos.AliqIrrfPct.HasValue)
        {
            AdicionarLinha(ws, linha++, "IRRF (%)", dto.Encargos.AliqIrrfPct.Value);
        }
        if (dto.Encargos.AliqIofPct.HasValue)
        {
            AdicionarLinha(ws, linha++, "IOF (%)", dto.Encargos.AliqIofPct.Value);
        }

        linha++;
        AdicionarLinha(ws, linha++, "Saldo Principal", dto.ResumoFinanceiro.SaldoPrincipalAberto);
        AdicionarLinha(ws, linha++, "Saldo Total Devedor", dto.ResumoFinanceiro.SaldoTotalDevedor);
        AdicionarLinha(ws, linha++, "Adimplência (%)", dto.ResumoFinanceiro.PctAdimplencia);
        AdicionarLinha(ws, linha++, "Parcelas Pagas", dto.ResumoFinanceiro.EventosPagos);
        AdicionarLinha(ws, linha++, "Total de Eventos", dto.ResumoFinanceiro.TotalEventos);

        if (dto.CotacaoAplicada is not null)
        {
            linha++;
            AdicionarLinha(ws, linha++, "Tipo Cotação", dto.CotacaoAplicada.TipoCotacao);
            AdicionarLinha(ws, linha++, "Valor Cotação", dto.CotacaoAplicada.ValorCotacao);
        }

        ws.Column(1).Width = 30;
        ws.Column(2).Width = 25;
        ws.SheetView.FreezeRows(1);
    }

    private static void AdicionarAbaCronograma(XLWorkbook wb, TabelaCompletaDto dto)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Cronograma");

        string[] cabecalhos = ["Nº Evento", "Tipo", "Data Prevista", "Valor", "Moeda", "Saldo Após", "Status", "Data Pagamento Efetivo", "Valor Pago"];
        for (int i = 0; i < cabecalhos.Length; i++)
        {
            ws.Cell(1, i + 1).Value = cabecalhos[i];
        }
        EstilizarCabecalho(ws.Row(1));

        int linha = 2;
        foreach (EventoCronogramaDto evento in dto.Cronograma)
        {
            ws.Cell(linha, 1).Value = evento.NumeroEvento;
            ws.Cell(linha, 2).Value = evento.Tipo;
            ws.Cell(linha, 3).Value = evento.DataPrevista.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            ws.Cell(linha, 4).Value = evento.Valor;
            ws.Cell(linha, 5).Value = evento.Moeda;

            if (evento.SaldoDevedorApos.HasValue)
            {
                ws.Cell(linha, 6).Value = evento.SaldoDevedorApos.Value;
            }
            else
            {
                ws.Cell(linha, 6).Value = "-";
            }

            if (evento.DataPagamentoEfetivo.HasValue)
            {
                ws.Cell(linha, 8).Value = evento.DataPagamentoEfetivo.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            else
            {
                ws.Cell(linha, 8).Value = "-";
            }

            ws.Cell(linha, 7).Value = evento.Status;

            if (evento.ValorPagamentoEfetivo.HasValue)
            {
                ws.Cell(linha, 9).Value = evento.ValorPagamentoEfetivo.Value;
            }
            else
            {
                ws.Cell(linha, 9).Value = "-";
            }

            linha++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void AdicionarAbaGarantias(XLWorkbook wb, TabelaCompletaDto dto)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Garantias");

        string[] cabecalhos = ["Tipo", "Valor BRL", "% Principal", "Status", "Data Constituição"];
        for (int i = 0; i < cabecalhos.Length; i++)
        {
            ws.Cell(1, i + 1).Value = cabecalhos[i];
        }
        EstilizarCabecalho(ws.Row(1));

        int linha = 2;
        foreach (GarantiaResumoDto g in dto.Garantias)
        {
            ws.Cell(linha, 1).Value = g.Tipo;
            ws.Cell(linha, 2).Value = g.ValorBrl;
            if (g.PercentualPrincipalPct.HasValue)
            {
                ws.Cell(linha, 3).Value = g.PercentualPrincipalPct.Value;
            }
            else
            {
                ws.Cell(linha, 3).Value = "-";
            }
            ws.Cell(linha, 4).Value = g.Status;
            ws.Cell(linha, 5).Value = g.DataConstituicao.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            linha++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void AdicionarAbaHistorico(XLWorkbook wb, TabelaCompletaDto dto)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Histórico");

        string[] cabecalhos = ["Nº Evento", "Tipo", "Data Pagamento", "Valor Pago (Moeda Orig.)", "Valor Pago BRL", "Taxa Câmbio"];
        for (int i = 0; i < cabecalhos.Length; i++)
        {
            ws.Cell(1, i + 1).Value = cabecalhos[i];
        }
        EstilizarCabecalho(ws.Row(1));

        int linha = 2;
        foreach (PagamentoEfetivoDto p in dto.HistoricoPagamentos)
        {
            ws.Cell(linha, 1).Value = p.NumeroEvento;
            ws.Cell(linha, 2).Value = p.Tipo;
            ws.Cell(linha, 3).Value = p.DataPagamentoEfetivo.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            ws.Cell(linha, 4).Value = p.ValorPagoMoedaOriginal;

            if (p.ValorPagoBrl.HasValue)
            {
                ws.Cell(linha, 5).Value = p.ValorPagoBrl.Value;
            }
            else
            {
                ws.Cell(linha, 5).Value = "-";
            }

            if (p.TaxaCambioPagamento.HasValue)
            {
                ws.Cell(linha, 6).Value = p.TaxaCambioPagamento.Value;
            }
            else
            {
                ws.Cell(linha, 6).Value = "-";
            }

            linha++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void EstilizarCabecalho(IXLRow row)
    {
        row.Style.Font.Bold = true;
        row.Style.Fill.BackgroundColor = XLColor.LightGray;
        row.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void AdicionarLinha(IXLWorksheet ws, int linha, string campo, object valor)
    {
        ws.Cell(linha, 1).Value = campo;
        ws.Cell(linha, 2).Value = valor?.ToString() ?? string.Empty;
    }
}
