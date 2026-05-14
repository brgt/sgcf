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

## Natureza das Contas

| Valor | Descrição |
|-------|-----------|
| `DEVEDORA` | Conta de saldo normal devedor (despesas, ativos) |
| `CREDORA` | Conta de saldo normal credor (receitas, passivos) |

---

## Integração com SAP B1

O campo `codigoSapB1` é utilizado como chave de mapeamento quando lançamentos contábeis gerados pelo SGCF são exportados para o SAP Business One. Contas sem esse código não serão incluídas na exportação automática.
