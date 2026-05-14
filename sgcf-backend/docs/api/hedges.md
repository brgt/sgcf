# Hedges API

**Base route:** `/api/v1/hedges`

Gerencia hedges cambiais de forma standalone (independente do contrato ao qual estão associados). Para **criar** ou **listar** hedges de um contrato específico, use os sub-recursos em [contratos.md](./contratos.md#hedges-do-contrato).

---

## Endpoints

### Calcular Mark-to-Market (MTM)

```
GET /api/v1/hedges/{id}/mtm
Autorização: Leitura
```

Calcula o valor presente de mercado (MTM) do hedge com base na cotação atual.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do hedge |

**Response 200 OK:**
```json
{
  "hedgeId": "guid",
  "tipo": "FORWARD | PUT | CALL",
  "notionalMoedaOriginal": "decimal",
  "mtmBrl": "decimal",
  "mtmMoedaOriginal": "decimal",
  "dataCalculo": "DateTimeOffset"
}
```

**Responses:**
- `200 OK` — [MtmResultadoDto](./schemas.md#mtmresultadodto)
- `404 Not Found` — Hedge não encontrado
- `422 Unprocessable Entity` — Não é possível calcular MTM (ex.: dados de cotação indisponíveis)

---

### Cancelar Hedge

```
DELETE /api/v1/hedges/{id}
Autorização: Gerencial
```

Cancela (desativa) um hedge existente. O hedge permanece no histórico com status inativo.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do hedge |

**Responses:**
- `204 No Content` — Cancelado com sucesso
- `404 Not Found` — Hedge não encontrado
- `403 Forbidden` — Role insuficiente

---

## Relacionamento com Contratos

```
Contrato (1) ──── (*) Hedge
```

- Hedges são criados via `POST /api/v1/contratos/{id}/hedges`
- Hedges são listados via `GET /api/v1/contratos/{id}/hedges`
- MTM e cancelamento são operações standalone via `/api/v1/hedges/{id}/...`

---

## Tipos de Hedge

| Tipo | Descrição | Strike obrigatório |
|------|-----------|-------------------|
| `FORWARD` | Contrato a termo — obriga a compra/venda na data futura | `strikeForward` |
| `PUT` | Opção de venda — direito de vender à taxa acordada | `strikePut` |
| `CALL` | Opção de compra — direito de comprar à taxa acordada | `strikeCall` |
