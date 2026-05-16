# Limites de Banco API

**Base route:** `/api/v1/limites-banco`

Gerencia os limites operacionais que cada banco concede para cada modalidade de captação. O `valorDisponivelBrl` é validado no momento de adicionar bancos-alvo a uma [Cotação](./cotacoes.md) e atualizado quando uma cotação é convertida em contrato (SPEC §3.2 regra 8, §4.1).

---

## Endpoints

### Listar Limites

```
GET /api/v1/limites-banco
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `bancoId` | guid | Filtra por banco |
| `modalidade` | string | Filtra por [ModalidadeContrato](./schemas.md#modalidadecontrato) |

**Response 200 OK:** [LimiteBancoDto](#limitebancodto)`[]`

---

### Criar Limite

```
POST /api/v1/limites-banco
Autorização: Admin
```

**Request Body:**

```json
{
  "bancoId": "guid",
  "modalidade": "Finimp",
  "valorLimiteBrl": 50000000.00,
  "dataVigenciaInicio": "2026-01-01",
  "dataVigenciaFim": "2026-12-31",
  "observacoes": "string | null"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `bancoId` | guid | Sim | Banco titular do limite |
| `modalidade` | string | Sim | Modalidade ao qual o limite se aplica |
| `valorLimiteBrl` | decimal | Sim | > 0 |
| `dataVigenciaInicio` | date | Sim | — |
| `dataVigenciaFim` | date | Não | Limite vigente indefinidamente se omitido |
| `observacoes` | string | Não | — |

**Responses:**
- `201 Created` — [LimiteBancoDto](#limitebancodto)
- `400 Bad Request`
- `409 Conflict` — Já existe limite ativo para o par banco/modalidade no período informado

---

### Atualizar Limite

```
PATCH /api/v1/limites-banco/{id}
Autorização: Admin
```

Atualiza apenas o valor do limite. Para alterar vigência, crie um novo limite.

**Request Body:**

```json
{ "novoValorLimiteBrl": 75000000.00 }
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `novoValorLimiteBrl` | decimal | Sim | > 0 |

**Responses:**
- `200 OK` — [LimiteBancoDto](#limitebancodto)
- `400 Bad Request`
- `404 Not Found`

---

## Schemas

### LimiteBancoDto

```json
{
  "id": "guid",
  "bancoId": "guid",
  "modalidade": "Finimp | Lei4131 | Refinimp | Nce | BalcaoCaixa | Fgi",
  "valorLimiteBrl": 50000000.00,
  "valorUtilizadoBrl": 12000000.00,
  "valorDisponivelBrl": 38000000.00,
  "dataVigenciaInicio": "YYYY-MM-DD",
  "dataVigenciaFim": "YYYY-MM-DD | null",
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

> `valorDisponivelBrl = valorLimiteBrl − valorUtilizadoBrl`. O `valorUtilizadoBrl` é mantido pela API: incrementado em `POST /api/v1/cotacoes/{id}/converter-em-contrato` e decrementado quando um contrato derivado é liquidado/cancelado.

---

## Regras de Validação

1. Não permite criar limite cujo período de vigência se sobreponha a outro já existente para o mesmo par `bancoId` × `modalidade`.
2. `valorLimiteBrl` não pode ser reduzido abaixo do `valorUtilizadoBrl` corrente.
3. Cotação só aceita banco-alvo se `valorAlvoBrl ≤ valorDisponivelBrl` na modalidade da cotação.

---

## Referências

- [SPEC do módulo Cotações §3.2](../specs/cotacoes/SPEC.md)
- [Cotações API](./cotacoes.md) — consumidor do limite
- [Coleção Bruno — 11-LimitesBanco](./collections/sgcf-api/11-LimitesBanco/)
