# SPIKE — MCP Server POC (`mcp-poc`)

Proves that .NET 10/11 can host a Model Context Protocol (MCP) server using the
**official C# SDK** (`ModelContextProtocol.AspNetCore` v1.3.0).

## How to run

```bash
BASE="/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend"
dotnet run --project "$BASE/spikes/mcp-poc/mcp-poc.csproj"
```

Server listens on **http://localhost:5000**.

## Endpoints

| Endpoint          | Purpose |
|-------------------|---------|
| `POST /mcp`       | MCP JSON-RPC 2.0 (initialize, tools/list, tools/call) |
| `GET  /health`    | Simple health probe |

## How to test with curl

### 1 — Health check

```bash
curl -s http://localhost:5000/health
# → {"status":"Healthy","totalDuration":"...","entries":{}}
```

### 2 — MCP `initialize`

```bash
curl -s -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": { "name": "curl-test", "version": "0.0.1" }
    }
  }'
```

Expected response includes `serverInfo.name = "sgcf-mcp-server"`.

### 3 — `tools/list`

```bash
curl -s -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "2",
    "method": "tools/list",
    "params": {}
  }'
```

Expected: tool `get_posicao_divida_dummy` with its JSON Schema input definition.

### 4 — `tools/call`

```bash
curl -s -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "3",
    "method": "tools/call",
    "params": {
      "name": "get_posicao_divida_dummy",
      "arguments": { "dataReferencia": "2026-05-10" }
    }
  }'
```

Expected: `content[0].type = "text"` with JSON debt position payload.

### 5 — Test with Claude Desktop (MCP Inspector)

Add this entry to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "sgcf-spike": {
      "command": "dotnet",
      "args": ["run", "--project", "<absolute-path>/spikes/mcp-poc/mcp-poc.csproj"],
      "transport": "http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

## Key SDK decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| SDK | `ModelContextProtocol.AspNetCore` v1.3.0 | Official LF Projects C# SDK; maintained at github.com/modelcontextprotocol/csharp-sdk |
| Transport | Streamable HTTP (`WithHttpTransport()`) | Standard for web-hosted servers; SSE streaming optional |
| Tool registration | `[McpServerTool]` attribute | SDK auto-generates JSON Schema from C# parameter types and `[Description]` |
| Namespace | `SgcfMcpPoc` | Isolated from production namespaces |

## Production gaps (for `src/Sgcf.Mcp/`)

- [ ] **Authentication**: Add `AddAuthentication().AddJwtBearer()` with the shared JWKS endpoint
- [ ] **Real tools**: Replace `DummySkillHandler` with `IMediator.Send(new GetPosicaoDividaQuery(...))`
- [ ] **Streaming**: Evaluate `WithStdioServerTransport()` for Claude Desktop vs HTTP for web agents
- [ ] **OpenTelemetry**: Add `AddOpenTelemetry()` tracing on MCP requests
- [ ] **Error mapping**: Map Application exceptions to MCP `isError: true` content
- [ ] **Tool schema precision**: Add `required` fields and `enum` constraints via `[JsonSchema]` attributes
- [ ] **net11.0**: Bump `TargetFramework` when .NET 11 SDK ships (expected Nov 2026)
