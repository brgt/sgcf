// =============================================================================
// SPIKE: MCP Server — SGCF POC
// Proves .NET 10/11 can host a Model Context Protocol (MCP) server using the
// official C# SDK (ModelContextProtocol.AspNetCore v1.3.0).
//
// Transport: Streamable HTTP  (POST /mcp — JSON-RPC 2.0 over HTTP)
// Tool:      get_posicao_divida_dummy  — returns hardcoded debt position data
//
// NOT production-ready. Missing: auth, observability, real Application layer.
// =============================================================================

using SgcfMcpPoc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(b => b.AddConsole());

// ── MCP Server registration ──────────────────────────────────────────────────
// AddMcpServer() registers the MCP JSON-RPC dispatcher.
// WithHttpTransport() enables the Streamable HTTP transport (POST /mcp).
// WithTools<T>() discovers [McpServerTool] methods on the given class.
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name    = "sgcf-mcp-server",
            Version = "0.1.0-spike"
        };
    })
    .WithHttpTransport()
    .WithTools<SgcfTools>();

// ── Health check ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map /health before MCP so it doesn't go through the MCP dispatcher
app.MapHealthChecks("/health");

// Map the MCP endpoint — handles initialize, tools/list, tools/call, etc.
app.MapMcp("/mcp");

app.Run("http://localhost:5000");
