using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sgcf.Application.Contratos;

namespace Sgcf.Api.Export;

/// <summary>
/// Gera o PDF da tabela completa de um contrato usando QuestPDF.
/// Inclui marca d'água diagonal com usuário e data/hora da exportação.
/// </summary>
public sealed class TabelaCompletaPdfExporter
{
    private static readonly string[] CabecalhosCronograma = ["Nº", "Vencimento", "Tipo", "Valor", "Status"];

    /// <summary>
    /// Gera o PDF com os 8 blocos da tabela completa.
    /// </summary>
    /// <param name="dto">Dados da tabela completa.</param>
    /// <param name="usuario">Nome do usuário que solicitou a exportação (para a marca d'água).</param>
    /// <param name="dataHora">Data e hora da exportação (ISO 8601).</param>
    /// <returns>Bytes do arquivo PDF.</returns>
    public static byte[] Gerar(TabelaCompletaDto dto, string usuario, string dataHora)
    {
        Document documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                page.Header().Element(header =>
                    RenderizarCabecalho(header, dto));

                page.Content().Element(content =>
                    RenderizarConteudo(content, dto));

                page.Foreground().Element(foreground =>
                    RenderizarMarcaDagua(foreground, usuario, dataHora));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static void RenderizarCabecalho(IContainer container, TabelaCompletaDto dto)
    {
        container.Column(col =>
        {
            col.Item().Text($"TABELA COMPLETA — {dto.Identificacao.Banco}")
                .Bold().FontSize(14);
            col.Item().Text($"Contrato: {dto.Identificacao.NumeroContratoExterno} | Modalidade: {dto.Identificacao.Modalidade}")
                .FontSize(10);
            col.Item().PaddingBottom(6).LineHorizontal(1);
        });
    }

    private static void RenderizarConteudo(IContainer container, TabelaCompletaDto dto)
    {
        container.Column(col =>
        {
            // Bloco A — Identificação
            col.Item().Section("A — Identificação").Text(text =>
            {
                text.Line($"Banco: {dto.Identificacao.Banco}");
                text.Line($"Modalidade: {dto.Identificacao.Modalidade}");
                text.Line($"Contratação: {dto.Identificacao.DataContratacao:dd/MM/yyyy}");
                text.Line($"Vencimento: {dto.Identificacao.DataVencimento:dd/MM/yyyy}");
                text.Line($"Status: {dto.Identificacao.Status}");
            });

            col.Item().PaddingVertical(4);

            // Bloco B — Valores Principais
            col.Item().Section("B — Valores Principais").Text(text =>
            {
                text.Line($"Principal Original: {dto.ValoresPrincipais.ValorPrincipalOriginal:N2} {dto.ValoresPrincipais.MoedaOriginal}");
            });

            col.Item().PaddingVertical(4);

            // Bloco C — Encargos
            col.Item().Section("C — Encargos Financeiros").Text(text =>
            {
                text.Line($"Taxa a.a.: {dto.Encargos.TaxaAaPct:N4}%");
                text.Line($"Base de Cálculo: {dto.Encargos.BaseCalculo}");
                if (dto.Encargos.AliqIrrfPct.HasValue)
                {
                    text.Line($"IRRF: {dto.Encargos.AliqIrrfPct:N2}%");
                }
                if (dto.Encargos.AliqIofPct.HasValue)
                {
                    text.Line($"IOF: {dto.Encargos.AliqIofPct:N2}%");
                }
            });

            col.Item().PaddingVertical(4);

            // Bloco D — Resumo Financeiro
            col.Item().Section("D — Resumo Financeiro").Text(text =>
            {
                text.Line($"Saldo Principal: {dto.ResumoFinanceiro.SaldoPrincipalAberto:N2} {dto.ResumoFinanceiro.Moeda}");
                text.Line($"Saldo Total Devedor: {dto.ResumoFinanceiro.SaldoTotalDevedor:N2} {dto.ResumoFinanceiro.Moeda}");
                text.Line($"Adimplência: {dto.ResumoFinanceiro.PctAdimplencia:N1}%");
                text.Line($"Parcelas: {dto.ResumoFinanceiro.EventosPagos} pagas / {dto.ResumoFinanceiro.TotalEventos} total");
            });

            col.Item().PaddingVertical(4);

            // Bloco E — Cronograma (tabela resumida)
            col.Item().Section("E — Cronograma").Element(cronEl => RenderizarTabelaCronograma(cronEl, dto));

            col.Item().PaddingVertical(4);

            // Bloco Garantias
            if (dto.Garantias.Count > 0)
            {
                col.Item().Section("Garantias").Text(text =>
                {
                    foreach (GarantiaResumoDto g in dto.Garantias)
                    {
                        string pct = g.PercentualPrincipalPct.HasValue
                            ? $" ({g.PercentualPrincipalPct.Value:N1}%)"
                            : string.Empty;
                        text.Line($"• {g.Tipo} — R$ {g.ValorBrl:N2}{pct} — {g.Status}");
                    }
                });
                col.Item().PaddingVertical(4);
            }

            // Bloco H — Histórico
            if (dto.HistoricoPagamentos.Count > 0)
            {
                col.Item().Section("H — Pagamentos Realizados").Text(text =>
                {
                    foreach (PagamentoEfetivoDto p in dto.HistoricoPagamentos)
                    {
                        text.Line($"Evento {p.NumeroEvento} — {p.DataPagamentoEfetivo:dd/MM/yyyy} — {p.ValorPagoMoedaOriginal:N2}");
                    }
                });
            }
        });
    }

    private static void RenderizarTabelaCronograma(IContainer container, TabelaCompletaDto dto)
    {
        if (dto.Cronograma.Count == 0)
        {
            container.Text("Sem parcelas no cronograma.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(40);  // Nº
                columns.RelativeColumn(2);   // Data
                columns.ConstantColumn(60);  // Tipo
                columns.RelativeColumn(3);   // Valor
                columns.RelativeColumn(2);   // Status
            });

            // Cabeçalho
            table.Header(header =>
            {
                foreach (string cab in CabecalhosCronograma)
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(cab).Bold();
                }
            });

            // Até 50 parcelas para manter o PDF razoável
            IEnumerable<EventoCronogramaDto> parcelas = dto.Cronograma.Take(50);
            bool alternado = false;

            foreach (EventoCronogramaDto evento in parcelas)
            {
                string fundo = alternado ? Colors.Grey.Lighten4 : Colors.White;
                alternado = !alternado;

                table.Cell().Background(fundo).Padding(2).Text(evento.NumeroEvento.ToString(CultureInfo.InvariantCulture));
                table.Cell().Background(fundo).Padding(2).Text(evento.DataPrevista.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                table.Cell().Background(fundo).Padding(2).Text(evento.Tipo);
                table.Cell().Background(fundo).Padding(2).Text($"{evento.Valor:N2} {evento.Moeda}");
                table.Cell().Background(fundo).Padding(2).Text(evento.Status);
            }

            if (dto.Cronograma.Count > 50)
            {
                table.Cell().ColumnSpan(5).Padding(4).Text(
                    $"... e mais {dto.Cronograma.Count - 50} parcelas (veja o XLSX para detalhes completos).")
                    .Italic();
            }
        });
    }

    /// <summary>
    /// Renderiza a marca d'água de auditoria usando somente a API fluent do QuestPDF.
    /// QuestPDF 2024+ não expõe SKCanvas diretamente — usamos um texto rotacionado centralizado.
    /// </summary>
    private static void RenderizarMarcaDagua(IContainer container, string usuario, string dataHora)
    {
        string texto = $"Exportado por {usuario} em {dataHora}";

        container
            .AlignCenter()
            .AlignMiddle()
            .Rotate(-45)
            .Text(texto)
            .FontSize(22)
            .FontColor(Colors.Grey.Lighten1)
            .Italic();
    }
}
