using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Sgcf.Mcp.Tools;

[McpServerToolType]
public sealed class RagTools
{
    // CA1861: extracted to avoid repeated allocation of the inline array on every call.
    private static readonly string NotAvailableJson = JsonSerializer.Serialize(
        new
        {
            disponivel = false,
            mensagem = "A camada RAG (busca vetorial em cláusulas contratuais) ainda não está disponível. Será habilitada na Fase 7B.6 com pgvector + Gemini embeddings.",
            fase_prevista = "7B.6"
        },
        McpJsonOptions.Default);

    [McpServerTool(Name = "buscar_clausula_contratual")]
    [Description("Busca cláusulas contratuais por similaridade semântica. ATENÇÃO: funcionalidade RAG ainda não disponível nesta versão.")]
    public string BuscarClausulaContratual(
        [Description("Texto da consulta (ex: 'vencimento antecipado', 'garantia exigível').")] string consulta)
    {
        return NotAvailableJson;
    }

    [McpServerTool(Name = "comparar_clausulas")]
    [Description("Compara cláusulas de dois contratos por categoria. ATENÇÃO: funcionalidade RAG ainda não disponível nesta versão.")]
    public string CompararClausulas(
        [Description("UUID do primeiro contrato.")] string contratoIdA,
        [Description("UUID do segundo contrato.")] string contratoIdB,
        [Description("Categoria de cláusula a comparar (ex: 'garantias', 'inadimplência').")] string? categoria)
    {
        return NotAvailableJson;
    }
}
