# SPIKE — A2A Agent POC (`a2a-poc`)

Proves that .NET 10/11 can serve a valid A2A Agent Card and handle task execution.

No official Google A2A .NET SDK exists as of 2026-05-10. Protocol is implemented
manually from the A2A spec v0.2: https://google.github.io/A2A/

## How to run

```bash
BASE="/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend"
dotnet run --project "$BASE/spikes/a2a-poc/a2a-poc.csproj"
```

Server listens on **http://localhost:5001**.

## Endpoints

| Endpoint                    | Purpose |
|-----------------------------|---------|
| `GET  /.well-known/agent.json` | A2A Agent Card (discovery) |
| `POST /a2a/tasks`           | Submit a task (synchronous) |
| `GET  /a2a/tasks/{id}`      | Poll task status / result |
| `GET  /health`              | Health probe |

## How to test with curl

### 1 — Fetch the Agent Card

```bash
curl -s http://localhost:5001/.well-known/agent.json | jq .
```

Expected: JSON with `schemaVersion: "0.2"`, skills array, capabilities.

### 2 — Submit a task

```bash
curl -s -X POST http://localhost:5001/a2a/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "id": "task-001",
    "message": {
      "role": "user",
      "parts": [{ "text": "Qual é a posição de dívida hoje?" }]
    }
  }' | jq .
```

Expected: task with `status.state = "completed"` and `result.parts[0].text` containing debt JSON.

### 3 — Poll task status

```bash
curl -s http://localhost:5001/a2a/tasks/task-001 | jq .
```

Expected: same completed task (served from in-memory store).

### 4 — Health check

```bash
curl -s http://localhost:5001/health
```

## Key implementation decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| SDK | None (manual) | No official .NET A2A SDK exists |
| State store | `ConcurrentDictionary` (in-memory) | MVP only; replace with Postgres for durability |
| Execution mode | Synchronous | Simplest for spike; streaming adds complexity |
| Serialization | `System.Text.Json` with camelCase policy | Standard for .NET; no extra dependencies |

## Production gaps (for `src/Sgcf.A2a/`)

- [ ] **Authentication**: Add `AddAuthentication().AddJwtBearer()` with shared JWKS endpoint (Bearer scheme advertised in Agent Card)
- [ ] **Durable state**: Replace in-memory store with Postgres `a2a_tasks` table; use EF Core (via Infrastructure project)
- [ ] **Streaming**: Implement SSE streaming when `capabilities.streaming = true`
- [ ] **Push notifications**: Implement webhook callbacks for long-running tasks
- [ ] **Real skill dispatch**: Replace `DummySkillHandler` with `IMediator.Send(...)` calls
- [ ] **Error handling**: Map Application exceptions to A2A `status.state = "failed"` + error message
- [ ] **State transitions**: Emit `submitted -> working -> completed/failed` via background worker
- [ ] **SDK watch**: Monitor https://github.com/google-a2a/a2a-samples for a .NET SDK
- [ ] **net11.0**: Bump `TargetFramework` when .NET 11 SDK ships (expected Nov 2026)
