# Plano de Contas API

**Base route:** `/api/v1/plano-contas`

Gerencia o plano de contas contábil utilizado para classificação dos lançamentos financeiros do SGCF. As contas são sincronizadas com o SAP Business One via campo `codigoSapB1`.

---

## Endpoints

### Listar Contas

```
GET /api/v1/plano-contas
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `search` | string | Busca por nome ou código da conta |
| `ativo` | bool | Filtra por status ativo/inativo |

**Response 200 OK:** `PlanoContasDto[]`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nome": "Variação Cambial Ativa",
    "natureza": "CREDORA",
    "codigoSapB1": "6.01.01.001",
    "ativo": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T00:00:00Z"
  }
]
```

---

### Buscar Conta por ID

```
GET /api/v1/plano-contas/{id}
Autorização: Leitura
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID da conta |

**Responses:**
- `200 OK` — [PlanoContasDto](./schemas.md#planocontasdto)
- `404 Not Found` — Conta não encontrada

---

### Criar Conta

```
POST /api/v1/plano-contas
Autorização: Auditoria
```

**Request Body:**
```json
{
  "nome": "Despesas com IOF Câmbio",
  "natureza": "DEVEDORA",
  "codigoSapB1": "6.02.01.005"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `nome` | string | Sim | Nome descritivo da conta |
| `natureza` | string | Sim | `DEVEDORA` \| `CREDORA` |
| `codigoSapB1` | string | Não | Código da conta no SAP B1 |

**Responses:**
- `201 Created` — [PlanoContasDto](./schemas.md#planocontasdto)
- `400 Bad Request` — Validação falhou
- `403 Forbidden` — Role insuficiente

---

### Atualizar Conta

```
PUT /api/v1/plano-contas/{id}
Autorização: Auditoria
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID da conta |

**Request Body:**
```json
{
  "nome": "Despesas com IOF Câmbio — Finimp",
  "natureza": "DEVEDORA",
  "codigoSapB1": "6.02.01.005"
}
```

**Responses:**
- `200 OK` — [PlanoContasDto](./schemas.md#planocontasdto) atualizado
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Conta não encontrada
- `403 Forbidden` — Role insuficiente

---

### Criar Lançamento Contábil

```
POST /api/v1/plano-contas/{contaId}/lancamentos
Autorização: Escrita
```

Registra um lançamento contábil associado a um contrato e a uma conta do plano de contas.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `contaId` | guid | ID da conta no plano de contas |

**Request Body:**

```json
{
  "contratoId": "guid (obrigatório)",
  "data": "YYYY-MM-DD (obrigatório)",
  "origem": "string — máx. 50 chars (obrigatório)",
  "valorDecimal": "decimal > 0 (obrigatório)",
  "moeda": "Brl | Usd | Eur | Jpy | Cny (obrigatório)",
  "descricao": "string (obrigatório)"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `contratoId` | guid | Sim | Contrato ao qual o lançamento se refere |
| `data` | date | Sim | Data de competência do lançamento |
| `origem` | string | Sim | Identificador da origem (ex.: `CRONOGRAMA`, `IOF`, `HEDGE`) |
| `valorDecimal` | decimal | Sim | Valor positivo do lançamento na moeda especificada |
| `moeda` | string | Sim | Moeda do valor |
| `descricao` | string | Sim | Descrição legível do lançamento |

**Responses:**
- `201 Created` — [LancamentoContabilDto](./schemas.md#lancamentocontabildto)
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Conta ou contrato não encontrados
- `403 Forbidden` — Role insuficiente

---

### Listar Lançamentos da Conta

```
GET /api/v1/plano-contas/{contaId}/lancamentos
Autorização: Auditoria
```

Retorna todos os lançamentos registrados para uma conta contábil específica, ordenados por data decrescente.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `contaId` | guid | ID da conta no plano de contas |

**Response 200 OK:** `LancamentoContabilDto[]` (ver [schemas.md](./schemas.md#lancamentocontabildto))

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "contratoId": "019e21cc-102f-79c0-b2c1-48ad8fef9d86",
    "planoContaId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "data": "2026-05-14",
    "origem": "CRONOGRAMA",
    "valor": 12345.67,
    "moeda": "Usd",
    "descricao": "Juros parcela 3/12",
    "createdAt": "2026-05-14T18:54:00Z"
  }
]
```

**Responses:**
- `200 OK` — `LancamentoContabilDto[]`
- `404 Not Found` — Conta não encontrada
- `403 Forbidden` — Role insuficiente

---

## Natureza das Contas

| Valor | Descrição |
|-------|-----------|
| `DEVEDORA` | Conta de saldo normal devedor (despesas, ativos) |
| `CREDORA` | Conta de saldo normal credor (receitas, passivos) |

---

## Integração com SAP B1

O campo `codigoSapB1` é utilizado como chave de mapeamento quando lançamentos contábeis gerados pelo SGCF são exportados para o SAP Business One. Contas sem esse código não serão incluídas na exportação automática.
