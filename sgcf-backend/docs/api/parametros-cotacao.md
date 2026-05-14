# Parâmetros de Cotação API

**Base route:** `/api/v1/parametros-cotacao`

Gerencia os tipos de cotação cambial disponíveis no sistema (ex.: PTAX, SPOT, CUPOM) e resolve qual tipo usar para cada combinação banco/modalidade.

---

## Endpoints

### Listar Parâmetros

```
GET /api/v1/parametros-cotacao
Autorização: Leitura
```

Retorna todos os tipos de cotação cadastrados.

**Response 200 OK:** `ParametroCotacaoDto[]`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tipoCotacao": "PTAX",
    "ativo": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T00:00:00Z"
  },
  {
    "id": "8b2c4d16-1a3e-4f87-9c5b-7d1e2f3a4b5c",
    "tipoCotacao": "SPOT",
    "ativo": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-03-10T08:15:00Z"
  }
]
```

---

### Buscar Parâmetro por ID

```
GET /api/v1/parametros-cotacao/{id}
Autorização: Leitura
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do parâmetro |

**Responses:**
- `200 OK` — [ParametroCotacaoDto](./schemas.md#parametrocotacaodto)
- `404 Not Found` — Parâmetro não encontrado

---

### Criar Parâmetro

```
POST /api/v1/parametros-cotacao
Autorização: Admin
```

**Request Body:**
```json
{
  "tipoCotacao": "CUPOM_CAMBIAL",
  "ativo": true
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `tipoCotacao` | string | Sim | Identificador do tipo de cotação |
| `ativo` | bool | Sim | Se o tipo está disponível para uso |

**Responses:**
- `201 Created` — [ParametroCotacaoDto](./schemas.md#parametrocotacaodto)
- `400 Bad Request` — Validação falhou
- `403 Forbidden` — Role insuficiente

---

### Atualizar Parâmetro

```
PUT /api/v1/parametros-cotacao/{id}
Autorização: Admin
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do parâmetro |

**Request Body:**
```json
{
  "tipoCotacao": "CUPOM_CAMBIAL",
  "ativo": false
}
```

**Responses:**
- `200 OK` — [ParametroCotacaoDto](./schemas.md#parametrocotacaodto) atualizado
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Parâmetro não encontrado
- `403 Forbidden` — Role insuficiente

---

### Resolver Tipo de Cotação

```
GET /api/v1/parametros-cotacao/resolve
Autorização: Leitura
```

Retorna qual tipo de cotação deve ser aplicado para uma determinada combinação de banco e modalidade de contrato. Útil para que o front-end exiba o tipo correto antes de criar ou calcular um contrato.

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `bancoId` | guid | Sim | ID do banco |
| `modalidade` | string | Sim | Ver enum [ModalidadeContrato](./schemas.md#modalidadecontrato) |

**Response 200 OK:**
```json
{
  "tipoCotacao": "PTAX"
}
```

**Responses:**
- `200 OK` — `ResolveTipoCotacaoResponse`
- `400 Bad Request` — `modalidade` inválida ou `bancoId` ausente

---

## Tipos de Cotação Padrão

| Tipo | Descrição |
|------|-----------|
| `PTAX` | Taxa de câmbio oficial do Banco Central (PTAX de fechamento) |
| `SPOT` | Taxa negociada no mercado interbancário no dia |
| `CUPOM_CAMBIAL` | Cotação baseada no cupom cambial (usada em alguns hedges) |

> Novos tipos podem ser cadastrados via `POST /api/v1/parametros-cotacao`. A resolução automática por banco/modalidade é configurada internamente e pode exigir regras adicionais no back-end.
