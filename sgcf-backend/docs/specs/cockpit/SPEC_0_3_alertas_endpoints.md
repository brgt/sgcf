# SPEC — Task 0.3 — Endpoints REST de Alertas

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 0.3
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Tasks 0.1 (envelope) e 0.2 (agregado `Alerta`)

---

## 1. Objetivo

Expor o agregado `Alerta` via REST para alimentar a faixa de alertas do cockpit (UX §5.2) e o badge de contadores no header. Endpoints respeitam visibilidade por perfil e envelope `{ data, meta }`.

---

## 2. Endpoints

| Método | Path | Auth Policy | Idempotente |
|--------|------|-------------|-------------|
| GET | `/api/v1/alertas` | `Policies.Leitura` | sim |
| GET | `/api/v1/alertas/contadores` | `Policies.Leitura` | sim |
| GET | `/api/v1/alertas/{id}` | `Policies.Leitura` | sim |
| POST | `/api/v1/alertas/{id}/dispensar` | `Policies.Leitura` | sim |
| POST | `/api/v1/alertas/{id}/marcar-como-lido` | `Policies.Leitura` | sim |

---

## 3. DTOs

```csharp
namespace Sgcf.Application.Alertas;

public sealed record AlertaDto(
    Guid Id,
    string Categoria,
    string Severidade,
    string Titulo,
    string Descricao,
    OrigemAlertaDto Origem,
    AcaoRecomendadaDto? Acao,
    IReadOnlyList<string> PerfisVisiveis,
    string Status,
    Instant CriadoEm,
    Instant? ExpiraEm,
    bool Expirado);

public sealed record OrigemAlertaDto(string Tipo, Guid Id);
public sealed record AcaoRecomendadaDto(string Rotulo, string Rota);

public sealed record ContadoresAlertaDto(int Critico, int Atencao, int Informativo);
```

`Expirado` é campo derivado: `ExpiraEm.HasValue && ExpiraEm.Value < agora`.

---

## 4. Contratos

### 4.1 `GET /api/v1/alertas`

**Query params:**

| Param | Tipo | Default | Observação |
|-------|------|---------|------------|
| `perfil` | string | derivado da claim do usuário | Sobrescrever só com claim de `Admin`. |
| `severidade` | string opcional | — | `CRITICO`, `ATENCAO`, `INFORMATIVO` |
| `categoria` | string opcional | — | uma das categorias do domínio |
| `status` | string opcional | `ABERTO,LIDO` | csv. Default exclui `DISPENSADO` |
| `page` | int | 1 | |
| `pageSize` | int | 25 | máx 100 |

**Response 200:**

```json
{
  "data": {
    "items": [
      {
        "id": "alt-uuid",
        "severidade": "CRITICO",
        "categoria": "COVENANT",
        "titulo": "Dívida/EBITDA acima do limite contratual",
        "descricao": "Banco XYZ — contrato CT-2026-018 — limite 3,5x, atual 3,72x",
        "origem": { "tipo": "CONTRATO", "id": "ct-uuid" },
        "acao": {
          "rotulo": "Renegociar covenant",
          "rota": "/app/finance/contratos/ct-uuid?tab=covenants"
        },
        "perfisVisiveis": ["CFO", "FINANCEIRO"],
        "status": "ABERTO",
        "criadoEm": "2026-05-19T08:00:00Z",
        "expiraEm": null,
        "expirado": false
      }
    ],
    "total": 42,
    "page": 1,
    "pageSize": 25
  },
  "meta": {
    "dataHoraCalculo": "2026-05-19T14:32:00Z",
    "fontesConsultadas": [{ "fonte": "alertas", "status": "OK", "registros": 42 }],
    "completude": "COMPLETO"
  }
}
```

**Ordenação default:** `severidade DESC, criadoEm DESC` (críticos no topo, mais recentes primeiro).

### 4.2 `GET /api/v1/alertas/contadores`

**Response 200:**

```json
{
  "data": { "critico": 3, "atencao": 8, "informativo": 12 },
  "meta": { "dataHoraCalculo": "2026-05-19T14:32:00Z", "fontesConsultadas": [], "completude": "COMPLETO" }
}
```

Conta apenas alertas com `Status in (ABERTO, LIDO)` visíveis ao perfil do usuário. **P95 menor que 200 ms** — usar query agregada `SELECT severidade, COUNT(*)` indexada.

### 4.3 `GET /api/v1/alertas/{id}`

Detalhe completo. Retorna 404 se o alerta não existe ou não é visível ao perfil.

### 4.4 `POST /api/v1/alertas/{id}/dispensar`

**Sem body** (ou body `{}`). Header `Idempotency-Key` opcional.

**Response 204 No Content.** Mudanças de estado:

- `ABERTO → DISPENSADO`: ok.
- `LIDO → DISPENSADO`: ok.
- `DISPENSADO → DISPENSADO`: idempotente, retorna 204 sem alteração.

`DispensadoPor` recebe `User.Identity.Name`; `DispensadoEm` recebe `clock.GetCurrentInstant()`.

**Auditoria:** registra evento `AlertaDispensado` em `AuditLog` (`Entity: "Alerta"`, `EntityId: alerta.Id`, `Detalhes: { dispensadoPor, status_antes }`).

### 4.5 `POST /api/v1/alertas/{id}/marcar-como-lido`

Idempotente. `ABERTO → LIDO`. `DISPENSADO` permanece (não regride).

---

## 5. Erros

| Status | Cenário | ProblemDetails |
|--------|---------|----------------|
| 400 | `pageSize > 100` | `detail: "pageSize máximo é 100"` |
| 401 | Sem token | padrão JWT |
| 403 | Token sem `Policies.Leitura` | padrão |
| 404 | Alerta inexistente ou não visível ao perfil | `detail: "Alerta não encontrado"` |
| 422 | Tentativa de operação inválida (futuro) | RFC 7807 |

**Nota de segurança:** 404 é usado tanto para inexistente quanto para invisível ao perfil — evita enumeração.

---

## 6. Autorização e Filtragem por Perfil

```csharp
private PerfilCockpit ExtrairPerfil(ClaimsPrincipal user)
{
    string? perfilClaim = user.FindFirstValue("perfil");
    return Enum.TryParse<PerfilCockpit>(perfilClaim, true, out var p)
        ? p
        : PerfilCockpit.FINANCEIRO; // default conservador
}
```

Filtro SQL aplicado pelo repositório:

```sql
WHERE perfis_visiveis && ARRAY[@perfil]::smallint[]
  AND status <> 3 -- DISPENSADO, salvo quando explicitamente pedido
```

---

## 7. Cache

`GET /alertas/contadores` recebe `Cache-Control: max-age=30, private` e header `ETag` baseado no `MAX(criado_em)`:

```csharp
[HttpGet("contadores")]
public async Task<IActionResult> GetContadores(CancellationToken ct)
{
    var resultado = await mediator.Send(new GetContadoresAlertasQuery(_perfilUsuario), ct);
    var etag = ComputarEtag(resultado);
    if (Request.Headers.IfNoneMatch.Equals(etag)) return StatusCode(304);

    Response.Headers.CacheControl = "max-age=30, private";
    Response.Headers.ETag = etag;
    return Ok(resultado);
}
```

`GET /alertas` **não** cacheado (paginação + filtros tornam ETag pouco efetivo).

---

## 8. MediatR — Commands e Queries

```csharp
public sealed record ListAlertasQuery(AlertaFilter Filtro)
    : IRequest<EnvelopeResponse<PagedResult<AlertaDto>>>;

public sealed record GetAlertaQuery(Guid Id, PerfilCockpit Perfil)
    : IRequest<EnvelopeResponse<AlertaDto>>;

public sealed record GetContadoresAlertasQuery(PerfilCockpit Perfil)
    : IRequest<EnvelopeResponse<ContadoresAlertaDto>>;

public sealed record DispensarAlertaCommand(Guid Id, string Usuario) : IRequest<Unit>;
public sealed record MarcarAlertaComoLidoCommand(Guid Id) : IRequest<Unit>;
```

---

## 9. Critérios de Aceite

- [ ] `AlertasController` registrado em `/api/v1/alertas`.
- [ ] 5 endpoints listados na §2 implementados.
- [ ] Envelope `{ data, meta }` em todas as respostas 200.
- [ ] Filtragem por perfil aplicada via cláusula SQL (não em memória).
- [ ] Idempotência confirmada em `dispensar` e `marcar-como-lido`.
- [ ] `ETag` + `Cache-Control` em `/contadores`.
- [ ] `AuditLog` registra dispensa.
- [ ] Testes E2E cobrindo: lista paginada, filtros, contadores, dispensa, marcar-como-lido, 404 por perfil incompatível.

---

## 10. Performance

| Endpoint | SLA P95 | Estratégia |
|----------|---------|------------|
| `GET /alertas` | 500 ms | Índice GIN em `perfis_visiveis`; paginação SQL |
| `GET /alertas/contadores` | 200 ms | Aggregate query + ETag/304 |
| `GET /alertas/{id}` | 100 ms | Lookup PK |
| `POST /alertas/{id}/dispensar` | 200 ms | Single update + audit insert |

Teste de carga: 50 RPS por 60 s em `/contadores` mantém P95 < 200 ms.

---

## 11. Boundaries específicas

### 11.1 Always do
- Sobrescrever `perfilUsuario` apenas com claim de `Admin`.
- Registrar `dispensar` em `AuditLog`.
- Retornar 404 (não 403) para alerta de outro perfil.

### 11.2 Ask first
- Adicionar endpoint de reabertura (`POST /alertas/{id}/reabrir`) — fora do escopo do MVP.
- Permitir exposição de alerta a usuários sem claim `Leitura`.

### 11.3 Never do
- Retornar lista completa de alertas sem filtro de perfil.
- Cachear `GET /alertas` (paginação + filtros tornam o cache pouco útil e gera risco de vazamento entre perfis).
- Expor `chaveIdempotencia` no DTO (informação interna).

---

## 12. Arquivos esperados

- `src/Sgcf.Api/Controllers/AlertasController.cs`
- `src/Sgcf.Application/Alertas/AlertaDto.cs`
- `src/Sgcf.Application/Alertas/Queries/ListAlertasQuery.cs` + Handler
- `src/Sgcf.Application/Alertas/Queries/GetAlertaQuery.cs` + Handler
- `src/Sgcf.Application/Alertas/Queries/GetContadoresAlertasQuery.cs` + Handler
- `src/Sgcf.Application/Alertas/Commands/DispensarAlertaCommand.cs` + Handler
- `src/Sgcf.Application/Alertas/Commands/MarcarAlertaComoLidoCommand.cs` + Handler
- `tests/Sgcf.Api.IntegrationTests/AlertasControllerTests.cs`
- `tests/Sgcf.Application.Tests/Alertas/ListAlertasQueryHandlerTests.cs`
