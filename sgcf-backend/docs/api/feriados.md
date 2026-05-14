# Feriados API

**Base route:** `/api/v1/feriados`

Gerencia o calendário de feriados nacionais utilizado pelo motor de cronograma para ajuste de datas de vencimento conforme a convenção configurada no contrato (`ConvencaoDataNaoUtil`).

---

## Endpoints

### Listar Feriados

```
GET /api/v1/feriados
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `ano` | int | Filtra feriados do ano especificado |
| `escopo` | string | `Nacional` \| `Estadual` \| `Municipal` |

**Response 200 OK:** `FeriadoDto[]`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "data": "2026-01-01",
    "descricao": "Confraternização Universal",
    "abrangencia": "Nacional",
    "tipo": "FixoCalendario",
    "fonte": "Manual",
    "createdAt": "2026-01-01T00:00:00Z"
  }
]
```

---

### Criar Feriado

```
POST /api/v1/feriados
Autorização: Admin
```

Adiciona manualmente um feriado ao calendário. Use para corrigir lacunas ou registrar feriados estaduais/municipais não cobertos pela base nacional.

**Request Body:**

```json
{
  "data": "YYYY-MM-DD (obrigatório)",
  "descricao": "string (obrigatório)",
  "abrangencia": "Nacional | Estadual | Municipal (obrigatório)",
  "tipo": "FixoCalendario | MovelCalendario | Pontual (obrigatório)"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `data` | date | Sim | Data do feriado (`YYYY-MM-DD`) |
| `descricao` | string | Sim | Descrição legível (ex.: "Corpus Christi") |
| `abrangencia` | string | Sim | Escopo do feriado |
| `tipo` | string | Sim | Classificação do feriado |

Feriados criados via API recebem `fonte = Manual` automaticamente. Feriados ingeridos via job BCB recebem `fonte = Anbima`.

**Responses:**
- `201 Created` — [FeriadoDto](#feriadodto)
- `400 Bad Request` — Validação falhou
- `403 Forbidden` — Role insuficiente (`admin` requerido)

---

### Excluir Feriado

```
DELETE /api/v1/feriados/{id}
Autorização: Admin
```

Remove um feriado do calendário. Apenas feriados com `fonte = Manual` devem ser excluídos; feriados ANBIMA são mantidos para rastreabilidade.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do feriado |

**Responses:**
- `204 No Content` — Removido com sucesso
- `404 Not Found` — Feriado não encontrado
- `403 Forbidden` — Role insuficiente

---

## Schemas

### FeriadoDto

```json
{
  "id": "guid",
  "data": "YYYY-MM-DD",
  "descricao": "string",
  "abrangencia": "Nacional | Estadual | Municipal",
  "tipo": "FixoCalendario | MovelCalendario | Pontual",
  "fonte": "Manual | Anbima",
  "createdAt": "DateTimeOffset (ISO 8601)"
}
```

---

## Enums

### EscopoFeriado (abrangencia)

| Valor | Descrição |
|-------|-----------|
| `Nacional` | Feriado nacional (aplica-se a todos os contratos) |
| `Estadual` | Feriado estadual (registrado, mas não afeta cronograma no MVP) |
| `Municipal` | Feriado municipal (registrado, mas não afeta cronograma no MVP) |

> O motor de cronograma considera apenas feriados com `abrangencia = Nacional` no MVP.

### TipoFeriado (tipo)

| Valor | Descrição |
|-------|-----------|
| `FixoCalendario` | Data fixa todo ano (ex.: 1° de janeiro) |
| `MovelCalendario` | Data variável calculada (ex.: Carnaval, Páscoa) |
| `Pontual` | Feriado de ocorrência única |

### FonteFeriado (fonte)

| Valor | Descrição |
|-------|-----------|
| `Manual` | Criado manualmente via API |
| `Anbima` | Ingerido automaticamente da base ANBIMA |

---

## Impacto no Cronograma

Quando uma data de vencimento calculada cai em feriado nacional ou fim de semana, o motor aplica a convenção configurada no contrato:

| ConvencaoDataNaoUtil | Comportamento |
|----------------------|---------------|
| `Following` | Move para o próximo dia útil (padrão) |
| `ModifiedFollowing` | Próximo dia útil, sem atravessar mês |
| `Preceding` | Move para o dia útil anterior |
| `NoAdjustment` | Mantém a data original |
