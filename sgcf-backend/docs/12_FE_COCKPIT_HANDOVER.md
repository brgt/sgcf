# Frontend Cockpit Handover — Fases 4 and 5

**Version**: 1.0  
**Date**: 2026-05-21  
**Audience**: Frontend engineers implementing the multi-persona cockpit (CFO, Gerente Financeiro, Tesouraria)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Envelope Response Format](#2-envelope-response-format)
3. [Cockpit CFO — Existing Endpoints (Reference)](#3-cockpit-cfo--existing-endpoints-reference)
4. [Covenants API](#4-covenants-api)
5. [Documentos Contratuais API](#5-documentos-contratuais-api)
6. [Conformidade / Registros Regulatórios API](#6-conformidade--registros-regulatrios-api)
7. [Economia Tributária API](#7-economia-tributria-api)
8. [Produtividade da Equipe](#8-produtividade-da-equipe)
9. [Exportação Assíncrona API](#9-exportao-assncrona-api)
10. [Server-Sent Events](#10-server-sent-events)
11. [Error Handling](#11-error-handling)
12. [Changelog](#12-changelog)

---

## 1. Overview

This document is the integration reference for all new API endpoints introduced in **Fase 4** (covenants, documentos contratuais, conformidade, economia tributária) and **Fase 5** (exportação assíncrona, Server-Sent Events, produtividade da equipe).

### Base URL

```
https://<host>/api/v1/
```

All paths in this document are relative to that base (for example, `GET /api/v1/covenants/violacoes`).

### Authentication

Every protected endpoint requires a **Bearer JWT** in the `Authorization` header:

```
Authorization: Bearer <token>
```

The server defines six authorization policies. The relevant ones for the cockpit are:

| Policy constant | JWT role required | Intended persona |
|---|---|---|
| `Leitura` | `leitura` | All cockpit read operations |
| `Escrita` | `escrita` | Create / update operations |
| `Gerencial` | `gerencial` | Delete operations |
| `Executivo` | `executivo` | KPI endpoints (CFO view) |
| `Auditoria` | `auditoria` | Audit log and produtividade endpoints |
| `Admin` | `admin` | Internal admin actions (not for cockpit) |

> Roles are embedded in the JWT claims. The token provider is responsible for issuing the correct role. If a request is made with insufficient privileges, the API returns `403 Forbidden`.

### Global Response Envelope

Most analytical and list endpoints wrap their payload in a standard envelope. See [Section 2](#2-envelope-response-format) for the TypeScript type and details on which endpoints use it.

---

## 2. Envelope Response Format

Endpoints decorated with `[ProducesEnvelope]` on the server always return a JSON object in this shape:

```json
{
  "data": { ... },
  "meta": {
    "dataHoraCalculo": "2026-05-21T14:30:00Z",
    "fontesConsultadas": [
      { "fonte": "banco_de_dados", "status": "ok", "registros": 12 }
    ],
    "completude": "Completo"
  }
}
```

### TypeScript interfaces

```typescript
export interface EnvelopeResponse<T> {
  data: T;
  meta: EnvelopeMeta;
}

export interface EnvelopeMeta {
  /** UTC instant when the response was assembled. ISO-8601 string. */
  dataHoraCalculo: string;
  fontesConsultadas: FonteConsultada[];
  completude: Completude;
}

export interface FonteConsultada {
  /** Source identifier, e.g. "banco_de_dados", "cache_redis", "api_bcb". */
  fonte: string;
  /** Query outcome, e.g. "ok", "timeout", "cache_hit", "indisponivel". */
  status: string;
  /** Record count returned by this source, or null when not applicable. */
  registros: number | null;
}

export type Completude = "Completo" | "Parcial" | "Degradado";
```

**Completude semantics**:

| Value | Meaning |
|---|---|
| `Completo` | All sources responded successfully; data is complete. |
| `Parcial` | At least one source returned partial data; response is usable but may have gaps. |
| `Degradado` | A primary source failed; the response was built from a fallback (e.g. stale cache). Data may be outdated. |

> **Important**: Endpoints that do **not** carry `[ProducesEnvelope]` return their DTO directly (no wrapper). The relevant ones in this guide are noted explicitly per endpoint.

---

## 3. Cockpit CFO — Existing Endpoints (Reference)

The following endpoints were implemented before Fase 4 and are fully documented in [`docs/api/cockpit-fe-guide.md`](./api/cockpit-fe-guide.md). They are listed here only for route discoverability.

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/v1/painel/divida` | Consolidated debt panel with MTM adjustments and alerts |
| `GET` | `/api/v1/painel/garantias` | Active guarantees panel with distribution by type and bank |
| `GET` | `/api/v1/painel/vencimentos` | Installment due-date calendar for a given year |
| `GET` | `/api/v1/painel/kpis` | Executive KPIs (total debt, average cost, average term, bank share) |
| `GET` | `/api/v1/painel/quadro-divida` | Debt frame: current snapshot + month-by-month projection |
| `GET` | `/api/v1/painel/divida/breakdown-modalidade` | Active debt aggregated by contract modality |
| `GET` | `/api/v1/painel/vencimentos/horizonte` | Forward maturity curve in temporal buckets |
| `GET` | `/api/v1/painel/estrutura-capital` | Capital structure with ICR (EBITDA / Financial Expense) |
| `GET` | `/api/v1/painel/inadimplencia` | Delinquent contracts with average delay and BRL exposure |
| `GET` | `/api/v1/painel/tarifas-iof` | IOF and fee aggregation by bank and modality |
| `POST` | `/api/v1/painel/ebitda` | Upsert monthly EBITDA (write; requires `Auditoria` policy) |
| `POST` | `/api/v1/painel/dados-contabeis` | Upsert monthly accounting data (write; requires `Escrita` policy) |

Refer to the existing guide for full request/response shapes.

---

## 4. Covenants API

Base path: `/api/v1/contratos/{contratoId}/covenants`  
Additional monitor path: `/api/v1/covenants`

### Enums

```typescript
export type TipoCovenant = "Financeiro" | "NaoFinanceiro" | "Informacional";

export type StatusCovenant = "Pendente" | "Cumprido" | "Violado" | "EmCura" | "Dispensado";
```

### Response shape

```typescript
export interface CovenantDto {
  id: string;                          // UUID
  contratoId: string;                  // UUID
  descricao: string;
  tipo: TipoCovenant;
  status: StatusCovenant;
  periodicidadeVerificacaoMeses: number;
  proximaVerificacaoEm: string | null; // "yyyy-MM-dd"
  ultimaVerificacaoEm: string | null;  // "yyyy-MM-dd"
  observacaoVerificacao: string | null;
  limiteNumerico: number | null;
  valorApurado: number | null;
}
```

---

### `GET /api/v1/contratos/{contratoId}/covenants`

Lists all covenants for a contract.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<CovenantDto[]>` |

---

### `POST /api/v1/contratos/{contratoId}/covenants`

Creates a new covenant on a contract.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `201 Created` — `CovenantDto` (no envelope) |
| **Errors** | `400` — validation failure |

**Request body**:

```typescript
interface CreateCovenantRequest {
  descricao: string;
  tipo: TipoCovenant;
  periodicidadeVerificacaoMeses: number;
  proximaVerificacaoEm: string | null; // "yyyy-MM-dd"
  limiteNumerico: number | null;
}
```

---

### `PUT /api/v1/contratos/{contratoId}/covenants/{id}`

Updates a covenant's description, periodicity, and numeric limit.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `CovenantDto` (no envelope) |
| **Errors** | `400`, `404` |

**Request body**:

```typescript
interface UpdateCovenantRequest {
  descricao: string;
  periodicidadeVerificacaoMeses: number;
  proximaVerificacaoEm: string | null; // "yyyy-MM-dd"
  limiteNumerico: number | null;
}
```

---

### `POST /api/v1/contratos/{contratoId}/covenants/{id}/verificacoes`

Records a verification check on a covenant, updating its status.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `CovenantDto` (no envelope) |
| **Errors** | `400`, `404` |

**Request body**:

```typescript
interface VerificarCovenantRequest {
  dataVerificacao: string;          // "yyyy-MM-dd"
  novoStatus: StatusCovenant;
  proximaVerificacaoEm: string | null; // "yyyy-MM-dd"
  valorApurado: number | null;
  observacao: string | null;
}
```

---

### `DELETE /api/v1/contratos/{contratoId}/covenants/{id}`

Removes a covenant.

| | |
|---|---|
| **Auth** | `Gerencial` |
| **Response** | `204 No Content` |
| **Errors** | `404` |

---

### `GET /api/v1/covenants/violacoes`

Returns all covenants in `Violado` status across all contracts. Used by the Gerente Financeiro and Tesouraria cockpit panels.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<CovenantDto[]>` |

---

## 5. Documentos Contratuais API

Base path: `/api/v1/contratos/{contratoId}/documentos`

### Enums

```typescript
export type TipoDocumento =
  | "Contrato"
  | "Aditivo"
  | "Garantia"
  | "Procuracao"
  | "BoletoFatura"
  | "RelatorioCompliance"
  | "Outro";

export type StatusDocumento =
  | "Pendente"
  | "EmRevisao"
  | "Aprovado"
  | "Rejeitado"
  | "Expirado";
```

### Response shape

> Note: `tipo` and `status` are serialized as **strings** (not integers) in the response.

```typescript
export interface DocumentoContratualDto {
  id: string;                        // UUID
  contratoId: string;                // UUID
  tipo: string;                      // TipoDocumento string value
  status: string;                    // StatusDocumento string value
  nome: string;
  urlArmazenamento: string | null;
  dataEmissao: string | null;        // "yyyy-MM-dd"
  dataVencimento: string | null;     // "yyyy-MM-dd"
  observacao: string | null;
  criadoEm: string;                  // ISO-8601 UTC instant
  atualizadoEm: string;              // ISO-8601 UTC instant
}
```

---

### `GET /api/v1/contratos/{contratoId}/documentos`

Lists all contractual documents for a contract.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<DocumentoContratualDto[]>` |

---

### `POST /api/v1/contratos/{contratoId}/documentos`

Attaches a new document to a contract.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `201 Created` — `EnvelopeResponse<DocumentoContratualDto>` |
| **Errors** | `400` |

**Request body**:

```typescript
interface CreateDocumentoRequest {
  tipo: TipoDocumento;
  nome: string;
  dataEmissao: string | null;      // "yyyy-MM-dd"
  dataVencimento: string | null;   // "yyyy-MM-dd"
  urlArmazenamento: string | null;
  observacao: string | null;
}
```

---

### `PUT /api/v1/contratos/{contratoId}/documentos/{id}`

Updates document metadata (not the file content itself).

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `EnvelopeResponse<DocumentoContratualDto>` |
| **Errors** | `400`, `404` |

**Request body**:

```typescript
interface UpdateDocumentoRequest {
  nome: string;
  dataEmissao: string | null;      // "yyyy-MM-dd"
  dataVencimento: string | null;   // "yyyy-MM-dd"
  urlArmazenamento: string | null;
  observacao: string | null;
}
```

---

### `DELETE /api/v1/contratos/{contratoId}/documentos/{id}`

Removes a document record.

| | |
|---|---|
| **Auth** | `Gerencial` |
| **Response** | `204 No Content` |
| **Errors** | `404` |

---

### `POST /api/v1/contratos/{contratoId}/documentos/{id}/status`

Transitions a document to a new status (e.g. from `EmRevisao` to `Aprovado`).

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `EnvelopeResponse<DocumentoContratualDto>` |
| **Errors** | `400`, `404` |

**Request body**:

```typescript
interface AtualizarStatusDocumentoRequest {
  novoStatus: StatusDocumento;
  observacao: string | null;
}
```

---

## 6. Conformidade / Registros Regulatórios API

Base path: `/api/v1/contratos/{contratoId}/registros-regulatorios`  
Additional monitor path: `/api/v1/conformidade`

### Enums

```typescript
export type TipoRegistroRegulatorio = "RdeRof" | "Def" | "Siscoserv" | "Outro";

export type StatusRegistroRegulatorio =
  | "Pendente"
  | "EmAnalise"
  | "Registrado"
  | "Dispensado"
  | "Expirado";
```

### Response shape

> Note: `tipo` and `status` are serialized as **strings** in the response.

```typescript
export interface RegistroRegulatorioDto {
  id: string;                      // UUID
  contratoId: string;              // UUID
  tipo: string;                    // TipoRegistroRegulatorio string value
  status: string;                  // StatusRegistroRegulatorio string value
  numeroRegistro: string | null;
  dataRegistro: string | null;     // "yyyy-MM-dd"
  dataVencimento: string | null;   // "yyyy-MM-dd"
  observacao: string | null;
  criadoEm: string;                // ISO-8601 UTC instant
  atualizadoEm: string;            // ISO-8601 UTC instant
}
```

---

### `GET /api/v1/contratos/{contratoId}/registros-regulatorios`

Lists all regulatory records for a contract.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<RegistroRegulatorioDto[]>` |

---

### `POST /api/v1/contratos/{contratoId}/registros-regulatorios`

Creates a new regulatory record on a contract.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `201 Created` — `EnvelopeResponse<RegistroRegulatorioDto>` |
| **Errors** | `400` |

**Request body**:

```typescript
interface CreateRegistroRegulatorioRequest {
  tipo: TipoRegistroRegulatorio;
  dataVencimento: string | null;   // "yyyy-MM-dd"
  observacao: string | null;
}
```

---

### `PUT /api/v1/contratos/{contratoId}/registros-regulatorios/{id}`

Updates the due date and observation of a regulatory record.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `EnvelopeResponse<RegistroRegulatorioDto>` |
| **Errors** | `400`, `404` |

**Request body**:

```typescript
interface UpdateRegistroRegulatorioRequest {
  dataVencimento: string | null;   // "yyyy-MM-dd"
  observacao: string | null;
}
```

---

### `DELETE /api/v1/contratos/{contratoId}/registros-regulatorios/{id}`

Removes a regulatory record.

| | |
|---|---|
| **Auth** | `Gerencial` |
| **Response** | `204 No Content` |
| **Errors** | `404` |

---

### `POST /api/v1/contratos/{contratoId}/registros-regulatorios/{id}/numero`

Registers the official filing number once the document is submitted to the regulator.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `EnvelopeResponse<RegistroRegulatorioDto>` |
| **Errors** | `400`, `404` |

**Request body**:

```typescript
interface RegistrarNumeroRequest {
  numeroRegistro: string;
  dataRegistro: string;            // "yyyy-MM-dd"
  observacao: string | null;
}
```

---

### `POST /api/v1/contratos/{contratoId}/registros-regulatorios/{id}/status`

Transitions a regulatory record to a new status.

| | |
|---|---|
| **Auth** | `Escrita` |
| **Response** | `200 OK` — `EnvelopeResponse<RegistroRegulatorioDto>` |
| **Errors** | `400` (invalid transition), `404` |

**Request body**:

```typescript
interface AtualizarStatusRegistroRequest {
  novoStatus: StatusRegistroRegulatorio;
  observacao: string | null;
}
```

---

### `GET /api/v1/conformidade/pendentes`

Returns all regulatory records with `Pendente` or `EmAnalise` status across all contracts. Used by the compliance summary panel.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<RegistroRegulatorioDto[]>` |

---

## 7. Economia Tributária API

Base path: `/api/v1/painel`

This endpoint calculates the estimated tax benefit derived from interest savings equalized against CDI. The combined effective rate applied is 34% (IRPJ + CSLL).

### Response shape

```typescript
export interface EconomiaTributariaDto {
  deAno: number;
  deMes: number;
  ateAno: number;
  ateMes: number;
  /** Total gross interest savings in BRL. */
  totalEconomiaBrl: number;
  /** Interest savings equalized against CDI, in BRL. */
  totalEconomiaAjustadaCdiBrl: number;
  /** Estimated tax benefit: totalEconomiaAjustadaCdiBrl * 0.34. */
  beneficioTributarioEstimadoBrl: number;
  totalOperacoes: number;
  porBanco: EconomiaTributariaPorBancoDto[];
}

export interface EconomiaTributariaPorBancoDto {
  /** null when bancoId is not available on the proposal snapshot. */
  bancoId: string | null;
  economiaBrl: number;
  economiaAjustadaCdiBrl: number;
  beneficioTributarioEstimadoBrl: number;
  operacoes: number;
}
```

---

### `GET /api/v1/painel/economia-tributaria`

Returns accumulated tax economy for the requested period.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<EconomiaTributariaDto>` |

**Query parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deAno` | integer | yes | Start year (e.g. `2025`) |
| `deMes` | integer | yes | Start month, 1–12 |
| `ateAno` | integer | yes | End year (e.g. `2025`) |
| `ateMes` | integer | yes | End month, 1–12 |
| `bancoId` | UUID | no | Filter results to a specific lender bank |

**Example request**:

```
GET /api/v1/painel/economia-tributaria?deAno=2025&deMes=1&ateAno=2025&ateMes=12
```

---

## 8. Produtividade da Equipe

Base path: `/api/v1/painel`

This endpoint aggregates analyst activity from the AuditLog. It is intended for the Gerente Financeiro persona's team productivity view.

### Response shape

```typescript
export interface ProdutividadeAnalistaDto {
  /** JWT subject identifier of the analyst. */
  actorSub: string;
  /** JWT role of the analyst at the time of the operations. */
  actorRole: string;
  totalOperacoes: number;
  /**
   * Average SLA in minutes: time between the first and last operation
   * on the same entity, averaged across all entities with >= 2 operations.
   * null when no entity had multiple operations in the period.
   */
  slaMediaMinutos: number | null;
  porEntidade: ProdutividadePorEntidadeDto[];
}

export interface ProdutividadePorEntidadeDto {
  /** Entity type name, e.g. "Contrato", "Covenant", "Cotacao". */
  entidade: string;
  operacoes: number;
}
```

---

### `GET /api/v1/painel/produtividade`

Returns productivity metrics per analyst for the given period.

| | |
|---|---|
| **Auth** | `Auditoria` |
| **Response** | `200 OK` — `EnvelopeResponse<ProdutividadeAnalistaDto[]>` |
| **Errors** | `400` — invalid period |

**Query parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deAno` | integer | yes | Start year (e.g. `2026`) |
| `deMes` | integer | yes | Start month, 1–12 |
| `ateAno` | integer | yes | End year (e.g. `2026`) |
| `ateMes` | integer | yes | End month, 1–12 |

**Example request**:

```
GET /api/v1/painel/produtividade?deAno=2026&deMes=1&ateAno=2026&ateMes=5
```

---

## 9. Exportação Assíncrona API

Base path: `/api/v1/exportacoes`

Export jobs are processed asynchronously. After creating a job the client must poll for completion.

### Enums

```typescript
export type TipoExportacao =
  | "Contratos"
  | "FluxoCaixa"
  | "Covenants"
  | "Alertas"
  | "AuditLog"
  | "Personalizado";

export type StatusExportacao = "Pendente" | "Processando" | "Concluido" | "Falhou";
```

### Response shape

> Note: `tipo` and `status` are serialized as **strings** in the response.

```typescript
export interface ExportacaoJobDto {
  id: string;                      // UUID
  tipo: string;                    // TipoExportacao string value
  status: string;                  // StatusExportacao string value
  parametrosJson: string | null;   // JSON string with export parameters
  resultadoJson: string | null;    // populated when status === "Concluido"
  mensagemErro: string | null;     // populated when status === "Falhou"
  solicitadoPor: string;           // JWT subject of the requesting user
  criadoEm: string;                // ISO-8601 UTC instant
  iniciadoEm: string | null;       // ISO-8601 UTC instant
  concluidoEm: string | null;      // ISO-8601 UTC instant
}
```

---

### `POST /api/v1/exportacoes`

Enqueues a new export job. Returns immediately with `202 Accepted`.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `202 Accepted` — `EnvelopeResponse<ExportacaoJobDto>` |
| **Errors** | `400` |

**Request body**:

```typescript
interface CreateExportacaoRequest {
  tipo: TipoExportacao;
  /** Optional JSON string with export-specific parameters. */
  parametrosJson: string | null;
}
```

---

### `GET /api/v1/exportacoes/{id}`

Returns the current status and result of an export job.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Response** | `200 OK` — `EnvelopeResponse<ExportacaoJobDto>` |
| **Errors** | `404` |

---

### Polling pattern

After receiving `202 Accepted`, poll `GET /api/v1/exportacoes/{id}` until `status` is `"Concluido"` or `"Falhou"`. When `"Concluido"`, the exported payload is in `resultadoJson`.

```typescript
async function pollExportacao(
  id: string,
  token: string,
  intervalMs = 2000,
  timeoutMs = 120_000,
): Promise<ExportacaoJobDto> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const res = await fetch(`/api/v1/exportacoes/${id}`, {
      headers: { Authorization: `Bearer ${token}` },
    });

    if (!res.ok) throw new Error(`Polling failed: ${res.status}`);

    const envelope: EnvelopeResponse<ExportacaoJobDto> = await res.json();
    const job = envelope.data;

    if (job.status === "Concluido" || job.status === "Falhou") {
      return job;
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  throw new Error(`Export job ${id} did not complete within ${timeoutMs}ms`);
}
```

---

## 10. Server-Sent Events

Base path: `/api/v1/eventos`

The SSE stream delivers real-time domain events to connected clients. It is the preferred mechanism for live updates in the cockpit panels.

### Event payload shape

```typescript
export interface EventoSistemaDto {
  /** Event type identifier, e.g. "alerta.criado", "covenant.violado", "heartbeat". */
  tipo: string;
  /** Entity type affected, e.g. "Alerta", "Covenant". null for system events like heartbeat. */
  entidadeTipo: string | null;
  /** ID of the affected entity. null for system events. */
  entidadeId: string | null;
  /** Human-readable description of the event. */
  mensagem: string | null;
  /** UTC instant when the event occurred. ISO-8601 string. */
  ocorridoEm: string;
}
```

---

### `GET /api/v1/eventos/stream`

Opens an SSE stream. The server sends `data: {json}\n\n` frames for each event.

| | |
|---|---|
| **Auth** | `Leitura` |
| **Content-Type** | `text/event-stream` |
| **Keep-alive** | A `heartbeat` event is emitted every 30 seconds |

> **Note**: `POST /api/v1/eventos` (publish a manual event) is restricted to the `Admin` policy. It is intended for backend integration tests and is not meant to be called from the frontend.

---

### Connecting with the browser EventSource API

The browser's native `EventSource` does not support custom headers, so you must pass the JWT as a query parameter or use a library such as `@microsoft/fetch-event-source` that supports custom headers.

The example below uses `fetch` directly to support the `Authorization` header:

```typescript
import { fetchEventSource } from "@microsoft/fetch-event-source";

fetchEventSource("/api/v1/eventos/stream", {
  headers: { Authorization: `Bearer ${token}` },
  onmessage(event) {
    const dto: EventoSistemaDto = JSON.parse(event.data);
    handleEvento(dto);
  },
});
```

---

### React hook example

```typescript
import { useEffect, useRef } from "react";

export function useEventoStream(
  token: string,
  onEvento: (e: EventoSistemaDto) => void,
) {
  const onEventoRef = useRef(onEvento);
  onEventoRef.current = onEvento;

  useEffect(() => {
    let aborted = false;
    const ctrl = new AbortController();

    async function connect() {
      const { fetchEventSource } = await import("@microsoft/fetch-event-source");

      await fetchEventSource("/api/v1/eventos/stream", {
        headers: { Authorization: `Bearer ${token}` },
        signal: ctrl.signal,
        onmessage(event) {
          if (aborted) return;
          try {
            const dto: EventoSistemaDto = JSON.parse(event.data);
            // Silently ignore the keep-alive heartbeat if not needed in the UI.
            if (dto.tipo === "heartbeat") return;
            onEventoRef.current(dto);
          } catch {
            // Malformed frame — ignore.
          }
        },
        onerror(err) {
          // fetchEventSource retries automatically on network errors.
          // Rethrow only on unrecoverable errors to stop the loop.
          if (aborted) throw err;
        },
      });
    }

    connect();

    return () => {
      aborted = true;
      ctrl.abort();
    };
  }, [token]);
}
```

**Usage**:

```tsx
useEventoStream(token, (evento) => {
  if (evento.tipo === "covenant.violado") {
    refetchCovenants();
  }
});
```

**Heartbeat**: The server broadcasts a `{ tipo: "heartbeat", ... }` event every 30 seconds to keep the connection alive through proxies and load balancers. The hook above silently discards heartbeats; include them if you need to display a "live" indicator.

---

## 11. Error Handling

### Standard HTTP status codes

| Status | Meaning | When to expect it |
|---|---|---|
| `400 Bad Request` | Validation failure or malformed input | Missing required fields, invalid date format, invalid enum value |
| `401 Unauthorized` | No valid JWT or expired token | Absent or expired `Authorization` header |
| `403 Forbidden` | Valid JWT but insufficient policy | User role does not match the required policy for the endpoint |
| `404 Not Found` | Resource does not exist | Unknown UUID in path |
| `409 Conflict` | Domain conflict | Duplicate key, invalid state transition, unsupported operation for current entity state |
| `500 Internal Server Error` | Unhandled server error | Unexpected failures; report to backend team |

### Validation errors (400)

Validation failures return a problem details object. Inspect the `detail` field for a human-readable message:

```json
{
  "detail": "Data '2026-13-01' inválida. Use o formato yyyy-MM-dd."
}
```

Some endpoints use ASP.NET Core's default `ValidationProblemDetails` format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "errors": {
    "Descricao": ["The Descricao field is required."]
  }
}
```

### Domain conflicts (409)

Invalid state transitions (for example, attempting to move a `RegistroRegulatorio` from `Registrado` back to `Pendente`) return `409 Conflict` with:

```json
{
  "detail": "Transição de status inválida: Registrado -> Pendente."
}
```

---

## 12. Changelog

| Date | Version | Change |
|---|---|---|
| 2026-05-21 | 1.0 | Initial handover — Fases 4 and 5 endpoints (covenants, documentos, conformidade, economia tributária, produtividade, exportação, SSE) |
