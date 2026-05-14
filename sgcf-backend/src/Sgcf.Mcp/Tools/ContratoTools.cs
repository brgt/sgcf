using System.ComponentModel;
using System.Text.Json;
using MediatR;
using ModelContextProtocol.Server;
using NodaTime.Text;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Queries;

namespace Sgcf.Mcp.Tools;

[McpServerToolType]
public sealed class ContratoTools(IMediator mediator)
{
    [McpServerTool(Name = "list_contratos")]
    [Description("Lista contratos de financiamento com suporte a filtros e paginação. Retorna uma página de resultados e o total de registros que satisfazem o filtro.")]
    public async Task<string> ListContratosAsync(
        [Description("Texto livre para busca em NumeroExterno e CodigoInterno (opcional).")] string? search,
        [Description("Número da página (base 1, padrão 1).")] int page,
        [Description("Tamanho da página, máximo 100 (padrão 25).")] int pageSize,
        CancellationToken cancellationToken)
    {
        ContratoFilter filter = new(Search: search, Page: page <= 0 ? 1 : page, PageSize: pageSize <= 0 ? 25 : pageSize);
        PagedResult<ContratoDto> result = await mediator.Send(new ListContratosQuery(filter), cancellationToken);
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(Name = "get_contrato")]
    [Description("Retorna detalhes completos de um contrato pelo seu UUID.")]
    public async Task<string> GetContratoAsync(
        [Description("UUID do contrato (ex: '3fa85f64-5717-4562-b3fc-2c963f66afa6').")] string contratoId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(contratoId, out Guid id))
        {
            return JsonSerializer.Serialize(new { error = "contratoId inválido — esperado UUID." }, McpJsonOptions.Default);
        }

        try
        {
            ContratoDto result = await mediator.Send(new GetContratoQuery(id), cancellationToken);
            return JsonSerializer.Serialize(result, McpJsonOptions.Default);
        }
        catch (KeyNotFoundException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
    }

    [McpServerTool(Name = "get_tabela_completa")]
    [Description("Retorna a tabela completa de um contrato: saldo devedor, cronograma de parcelas, garantias e histórico de pagamentos.")]
    public async Task<string> GetTabelaCompletaAsync(
        [Description("UUID do contrato.")] string contratoId,
        [Description("Data de referência no formato YYYY-MM-DD (opcional; padrão = hoje).")] string? dataReferencia,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(contratoId, out Guid id))
        {
            return JsonSerializer.Serialize(new { error = "contratoId inválido — esperado UUID." }, McpJsonOptions.Default);
        }

        NodaTime.LocalDate? dataRef = null;
        if (!string.IsNullOrEmpty(dataReferencia))
        {
            ParseResult<NodaTime.LocalDate> parsed = LocalDatePattern.Iso.Parse(dataReferencia);
            if (parsed.Success)
            {
                dataRef = parsed.Value;
            }
        }

        try
        {
            TabelaCompletaDto result = await mediator.Send(new GetTabelaCompletaQuery(id, dataRef), cancellationToken);
            return JsonSerializer.Serialize(result, McpJsonOptions.Default);
        }
        catch (KeyNotFoundException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
    }
}
