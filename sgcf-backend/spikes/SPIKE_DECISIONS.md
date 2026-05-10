# Spike Decision Record — MCP + A2A on .NET 10/11

**Date:** 2026-05-10  
**Author:** welysson  
**Status:** Draft — pending production sign-off  
**Spikes:** `spikes/mcp-poc/`, `spikes/a2a-poc/`

---

## 1 — MCP (Model Context Protocol)

### 1.1 — Official .NET SDK availability

**Verdict: YES — official SDK exists.**

| Package | Version | Publisher | Source |
|---------|---------|-----------|--------|
| `ModelContextProtocol` | 1.3.0 | Model Context Protocol / LF Projects, LLC | github.com/modelcontextprotocol/csharp-sdk |
| `ModelContextProtocol.Core` | 1.3.0 | same | same |
| `ModelContextProtocol.AspNetCore` | 1.3.0 | same | same |

The SDK targets net8.0 / netstandard2.0 and is compatible with net10.0 (tested in spike).
It supports the `[McpServerTool]` attribute pattern for tool registration, automatic JSON Schema
generation from C# types, Streamable HTTP transport, and stdio transport.

**No Anthropic-branded .NET package exists.** The SDK is maintained by the MCP open-source
project under the Linux Foundation umbrella.

### 1.2 — Spike findings

The POC in `spikes/mcp-poc/` proves:
- `AddMcpServer().WithHttpTransport().WithTools<T>()` registers a fully functional MCP server
  in ~30 lines of setup code.
- Tool methods annotated with `[McpServerTool]` are auto-discovered; JSON Schema is derived
  from parameter types and `[Description]` attributes.
- Health check and MCP endpoint coexist on the same ASP.NET Core host.

### 1.3 — Recommended approach for `src/Sgcf.Mcp/`

1. **Add packages** (add to `Directory.Packages.props` once using Central Package Management):
   ```xml
   <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="1.3.0" />
   ```

2. **Project type**: Change `Sgcf.Mcp.csproj` from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Web`
   (required by `WithHttpTransport()`).

3. **Tool classes**: Create one class per Application use-case group:
   ```
   Sgcf.Mcp/Tools/DividaTools.cs      → get_posicao_divida, get_posicao_por_moeda
   Sgcf.Mcp/Tools/ContratoTools.cs    → buscar_contratos, detalhe_contrato
   Sgcf.Mcp/Tools/HedgeTools.cs       → mtm_hedge, simulacao_cambial
   ```
   Each tool method receives an injected `IMediator` and calls the appropriate query/command.

4. **Auth**: Register JWT Bearer before `AddMcpServer()`:
   ```csharp
   builder.Services.AddAuthentication().AddJwtBearer(options => {
       options.Authority = builder.Configuration["Auth:Authority"];
       // JWKS fetched automatically from Authority
   });
   app.UseAuthentication();
   app.UseAuthorization();
   ```

5. **Transport decision**:
   - HTTP transport (`WithHttpTransport()`) for web-hosted servers (Kubernetes, Cloud Run).
   - Stdio transport (`WithStdioServerTransport()`) for local Claude Desktop integration.
   - Use a feature flag / configuration switch to select at startup.

6. **Estimated effort**: ~2 days (tools + auth + tests). SDK eliminates JSON-RPC boilerplate.

---

## 2 — A2A (Agent-to-Agent Protocol)

### 2.1 — Official .NET SDK availability

**Verdict: NO — no official Google .NET A2A SDK exists as of 2026-05-10.**

Researched:
- NuGet search for "agent2agent", "a2a dotnet", "google a2a" — no results.
- `https://github.com/google-a2a/a2a-samples` — Python and Node.js samples only.
- The A2A spec (v0.2) is a JSON/HTTP protocol; implementation requires manual work.

**Estimated effort for production `Sgcf.A2a/` adapter**: ~3-4 days.

### 2.2 — Current A2A spec version

**v0.2** (latest stable as of 2026-05-10).  
Spec: https://google.github.io/A2A/  
Key endpoints defined by spec:
- `GET /.well-known/agent.json` — Agent Card
- `POST /` (or configurable path) — task submission (JSON-RPC `tasks/send`)
- Streaming via SSE: `tasks/sendSubscribe`
- Push notifications: webhook callbacks

The spike implements a simplified but spec-compliant subset (sync `tasks/send` only).

### 2.3 — State persistence recommendation

| Option | Pro | Con | Recommendation |
|--------|-----|-----|----------------|
| In-memory `ConcurrentDictionary` | Zero deps, instant | Lost on restart, no clustering | Dev/spike only |
| Postgres `a2a_tasks` table | Durable, queryable, existing Postgres | EF migration needed | **MVP production** |
| Redis | Fast, TTL-based cleanup | Extra dep, less queryable | Future optimization |

**Recommendation**: Use a Postgres `a2a_tasks` table via EF Core for MVP.
Schema (simplified):
```sql
CREATE TABLE a2a_tasks (
    id          TEXT PRIMARY KEY,
    state       TEXT NOT NULL DEFAULT 'submitted',
    input_text  TEXT,
    result_json JSONB,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

This table must live in `Sgcf.Infrastructure` (EF migration + entity config).
`Sgcf.A2a` accesses it via an `IA2aTaskRepository` interface defined in `Sgcf.Application`.

### 2.4 — Recommended approach for `src/Sgcf.A2a/`

1. **No external SDK**: implement protocol shapes as C# records (as in spike).
2. **Agent Card**: serve as a static resource or build from `IConfiguration`.
3. **Skill routing**: map incoming task text to `IMediator` queries (keyword or embedded model classification).
4. **State machine**: `submitted -> working -> completed | failed` managed by a background `IHostedService`.
5. **Auth**: same pattern as MCP — `AddAuthentication().AddJwtBearer()`.

---

## 3 — Shared Authentication

Both `Sgcf.Mcp` and `Sgcf.A2a` must validate the same tokens as `Sgcf.Api`.

**Implementation pattern** (identical for both projects):

```csharp
// In Program.cs of Sgcf.Mcp and Sgcf.A2a:
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Same authority as Sgcf.Api — JWKS fetched automatically
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience  = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization();

// After app.Build():
app.UseAuthentication();
app.UseAuthorization();
```

No custom JWKS code is needed. ASP.NET Core's JWT middleware fetches the
JWKS document from `{Authority}/.well-known/openid-configuration` automatically.

The `Auth:Authority` and `Auth:Audience` values should come from a shared
`appsettings.json` section or environment variables. Both spikes omit auth
intentionally — it is the first thing to add before any non-spike deployment.

---

## 4 — Framework Version Note

The project `Directory.Build.props` specifies `net10.0`. The requested `net11.0`
does not yet exist (.NET 11 SDK is expected November 2026).

Both spike csproj files override `TargetFramework` to `net10.0` and document
the pending upgrade. All production code should upgrade to `net11.0` once the
SDK ships by updating `Directory.Build.props`.

---

## 5 — Summary Table

| Concern | MCP | A2A |
|---------|-----|-----|
| Official .NET SDK | Yes — `ModelContextProtocol.AspNetCore` v1.3.0 | No — manual implementation |
| Effort (production) | ~2 days | ~3-4 days |
| Transport | HTTP + SSE (Streamable HTTP) | HTTP + optional SSE |
| State | Stateless (per-call) | Stateful (task lifecycle) |
| Auth | JWT Bearer (shared) | JWT Bearer (shared) |
| State persistence | N/A | Postgres `a2a_tasks` for MVP |
| Target framework | net10.0 now; net11.0 on GA | same |
