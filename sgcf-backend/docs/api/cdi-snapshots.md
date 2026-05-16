# CDI Snapshots API

**Base route:** `/api/v1/cdi-snapshots`

Gerencia snapshots diários da taxa CDI cadastrados manualmente no MVP. A taxa CDI é usada pelo cálculo de **economia ajustada** na conversão de uma [Cotação](./cotacoes.md) em contrato, equalizando o VPL de fluxos com prazos distintos (SPEC §5.2, §5.3, §13 decisão 2).

> Integração automática com a base ANBIMA está prevista para ondas futuras. Hoje, um administrador deve cadastrar o snapshot do dia útil de conversão da cotação.

---

## Endpoints

### Criar Snapshot

```
POST /api/v1/cdi-snapshots
Autorização: Admin
```

**Request Body:**

```json
{
  "data": "2026-05-16",
  "cdiAaPercentual": 10.75
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `data` | date | Sim | Dia útil de referência |
| `cdiAaPercentual` | decimal | Sim | Taxa CDI em % a.a. (> 0). Ex.: `10.75` para 10,75% |

**Responses:**
- `201 Created` — [CdiSnapshotDto](#cdisnapshotdto)
- `400 Bad Request`
- `409 Conflict` — Já existe snapshot para a data informada

---

### Listar Snapshots

```
GET /api/v1/cdi-snapshots
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `desde` | date | Data mínima (`YYYY-MM-DD`). Padrão: `ate − 29 dias` |
| `ate` | date | Data máxima (`YYYY-MM-DD`). Padrão: hoje (timezone `America/Sao_Paulo`) |

Resultado retorna em ordem crescente por `data`. Sem parâmetros, retorna os últimos 30 dias.

**Response 200 OK:** [CdiSnapshotDto](#cdisnapshotdto)`[]`

---

## Schemas

### CdiSnapshotDto

```json
{
  "id": "guid",
  "data": "YYYY-MM-DD",
  "cdiAaPercentual": 10.75,
  "createdAt": "DateTimeOffset"
}
```

---

## Uso

O snapshot é consultado por `CalculadoraEconomia` quando uma cotação aceita é convertida em contrato. A `dataReferenciaCdi` registrada em `EconomiaNegociacao` é a data do snapshot efetivamente utilizado. Se o snapshot da data exata não existir, o serviço busca o snapshot mais recente anterior à data de conversão.

---

## Referências

- [SPEC do módulo Cotações §5.2, §5.3](../specs/cotacoes/SPEC.md)
- [Cotações API](./cotacoes.md)
- [Coleção Bruno — 12-CdiSnapshots](./collections/sgcf-api/12-CdiSnapshots/)
