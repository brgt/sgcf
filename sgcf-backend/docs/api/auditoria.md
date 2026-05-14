# Auditoria API

**Base route:** `/audit`

Consulta o log de auditoria imutável do SGCF. Toda operação de escrita realizada via REST, MCP ou A2A é registrada automaticamente com diff JSON (before/after).

> Apenas usuários com role `auditor` ou `admin` têm acesso a estes endpoints.

---

## Endpoints

### Listar Eventos de Auditoria

```
GET /audit/eventos
Autorização: Auditoria
```

Retorna eventos paginados e filtrados do log de auditoria. A lista é ordenada por `occurredAt` decrescente (mais recentes primeiro).

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `entity` | string | Nome da entidade (ex.: `Contrato`, `Banco`, `Feriado`) |
| `entityId` | guid | UUID da entidade específica |
| `actorSub` | string | Identificador do autor da operação (claim `sub` do JWT) |
| `source` | string | Origem da chamada: `rest` \| `mcp` \| `a2a` \| `job` |
| `operation` | string | `CREATE` \| `UPDATE` \| `DELETE` |
| `de` | DateTimeOffset | Início do intervalo (ISO 8601, ex.: `2026-05-01T00:00:00Z`) |
| `ate` | DateTimeOffset | Fim do intervalo (ISO 8601) |
| `page` | int | Página (padrão: `1`) |
| `pageSize` | int | Itens por página (padrão: `50`, máx.: `200`) |

**Response 200 OK:** `PagedResult<AuditEventoDto>`

```json
{
  "items": [
    {
      "id": 12345,
      "occurredAt": "2026-05-14T18:54:00Z",
      "actorSub": "user-welysson",
      "actorRole": "tesouraria",
      "source": "rest",
      "entity": "Contrato",
      "entityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "operation": "CREATE",
      "diffJson": "{\"before\":null,\"after\":{\"NumeroExterno\":\"FINIMP-2026-001\",...}}",
      "requestId": "550e8400-e29b-41d4-a716-446655440000"
    }
  ],
  "total": 840,
  "page": 1,
  "pageSize": 50
}
```

**Responses:**
- `200 OK` — `PagedResult<AuditEventoDto>`
- `400 Bad Request` — Parâmetros inválidos (ex.: `page < 1`, `pageSize > 200`, `ate < de`)
- `401 Unauthorized` — Token ausente ou inválido
- `403 Forbidden` — Role insuficiente

---

## Schemas

### AuditEventoDto

```json
{
  "id": "long (bigserial)",
  "occurredAt": "DateTimeOffset (ISO 8601, UTC)",
  "actorSub": "string — claim 'sub' do JWT ou 'system' para jobs",
  "actorRole": "string — role do autor",
  "source": "rest | mcp | a2a | job",
  "entity": "string — nome da entidade C# (ex.: 'Contrato')",
  "entityId": "guid | null",
  "operation": "CREATE | UPDATE | DELETE",
  "diffJson": "string JSON | null — estrutura { before: {...}, after: {...} }",
  "requestId": "guid — correlation ID da requisição"
}
```

> `ipHash` é intencionalmente omitido da resposta para proteção de privacidade (LGPD).

### diffJson

O campo `diffJson` contém um objeto JSON serializado com dois campos opcionais:

```json
{
  "before": { "Campo": "valorAnterior", ... },
  "after":  { "Campo": "valorNovo", ... }
}
```

| Operação | `before` | `after` |
|----------|----------|---------|
| `CREATE` | `null` | Snapshot completo da entidade criada |
| `UPDATE` | Snapshot antes da alteração | Snapshot após a alteração |
| `DELETE` | Snapshot completo da entidade removida | `null` |

---

## Entidades auditadas

Todas as entidades que implementam `IAuditable` são auditadas automaticamente. Na versão atual:

| Entidade | Operações rastreadas |
|----------|----------------------|
| `Contrato` | CREATE, UPDATE, DELETE |
| `Banco` | CREATE, UPDATE |
| `Feriado` | CREATE, DELETE |
| `InstrumentoHedge` | CREATE, UPDATE (cancelamento) |
| `Garantia` | CREATE, UPDATE (cancelamento) |
| `EventoCronograma` | CREATE, UPDATE (baixa de pagamento) |
| `EbitdaMensal` | CREATE, UPDATE |
| `ParametroCotacao` | CREATE, UPDATE, DELETE |
| `PlanoContas` | CREATE, UPDATE |
| `LancamentoContabil` | CREATE |

---

## Sources

| Valor | Descrição |
|-------|-----------|
| `rest` | Chamada via REST API (`Sgcf.Api`) |
| `mcp` | Chamada via protocolo MCP (`Sgcf.Mcp`) |
| `a2a` | Chamada via protocolo A2A (`Sgcf.A2a`) |
| `job` | Operação executada por job em background (`Sgcf.Jobs`) |

---

## Retenção

O log de auditoria é **append-only** (nunca atualizado ou deletado). Retenção mínima de **5 anos** conforme §10.2 do SPEC.
