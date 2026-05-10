// =============================================================================
// MCP Tool implementations — separated from Program.cs because a file-scoped
// namespace cannot appear after top-level statements in the same compilation unit.
// =============================================================================

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SgcfMcpPoc;

/// <summary>
/// MCP tools exposed by the SGCF spike server.
/// In production, each method dispatches to IMediator and maps the result.
/// NOTE: Must be a non-static class — WithTools&lt;T&gt;() requires an instantiable type.
/// </summary>
[McpServerToolType]
public sealed class SgcfTools
{
    [McpServerTool(Name = "get_posicao_divida_dummy")]
    [Description("Retorna posição de dívida consolidada (SPIKE — dados fictícios).")]
    public string GetPosicaoDividaDummy(
        [Description("Data de referência no formato YYYY-MM-DD (opcional; padrão = hoje)")]
        string? dataReferencia = null)
    {
        // In production: IMediator.Send(new GetPosicaoDividaQuery(dataReferencia))
        // then map Application DTO → MCP tool result string.
        var data = dataReferencia ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        return $$"""
            {
              "data_referencia": "{{data}}",
              "divida_total_brl": 45000000.00,
              "moedas": [
                { "moeda": "USD", "principal": 5000000.00, "ptax": 5.30 },
                { "moeda": "EUR", "principal": 3200000.00, "ptax": 5.91 },
                { "moeda": "BRL", "principal": 18500000.00 }
              ],
              "fonte":  "SPIKE-POC",
              "aviso":  "Dados fictícios para teste de conectividade MCP"
            }
            """;
    }
}
