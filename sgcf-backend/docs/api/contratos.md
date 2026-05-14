# Contratos API

**Base route:** `/api/v1/contratos`

Gerencia contratos de captação/financiamento externo. É o recurso central do SGCF — todos os demais recursos (hedges, garantias, cronograma) são subordinados a um contrato.

---

## Endpoints

### Listar Contratos

```
GET /api/v1/contratos
Autorização: Leitura
```

Retorna contratos paginados com suporte a múltiplos filtros.

**Query Parameters:**

| Parâmetro | Tipo | Padrão | Descrição |
|-----------|------|--------|-----------|
| `q` | string | — | Busca por `NumeroExterno` ou `CodigoInterno` |
| `bancoId` | guid | — | Filtra por banco |
| `modalidade` | string | — | Ver enum [ModalidadeContrato](./schemas.md#modalidadecontrato) |
| `moeda` | string | — | Ver enum [Moeda](./schemas.md#moeda) |
| `status` | string | — | Ver enum [StatusContrato](./schemas.md#statuscontrato) |
| `vencDe` | date | — | Vencimento a partir de (`YYYY-MM-DD`) |
| `vencAte` | date | — | Vencimento até (`YYYY-MM-DD`) |
| `valorMin` | decimal | — | Valor principal mínimo |
| `valorMax` | decimal | — | Valor principal máximo |
| `temHedge` | bool | — | Possui hedge ativo |
| `temGarantia` | bool | — | Possui garantia ativa |
| `temAlerta` | bool | — | Possui alertas |
| `page` | int | `1` | Página (base 1) |
| `pageSize` | int | `25` | Itens por página (máx. 100) |
| `sort` | string | `DataVencimento` | `DataVencimento` \| `DataContratacao` \| `ValorPrincipal` \| `NumeroExterno` |
| `dir` | string | `asc` | `asc` \| `desc` |

**Response 200 OK:**
```json
{
  "items": [ContratoDto],
  "total": 150,
  "page": 1,
  "pageSize": 25
}
```

---

### Buscar Contrato por ID

```
GET /api/v1/contratos/{id}
Autorização: Leitura
```

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do contrato |

**Responses:**
- `200 OK` — [ContratoDto](./schemas.md#contradto)
- `404 Not Found` — Contrato não encontrado

---

### Criar Contrato

```
POST /api/v1/contratos
Autorização: Escrita
Idempotency-Key: recomendado
```

**Request Body:**

```json
{
  "numeroExterno": "string (obrigatório)",
  "bancoId": "guid (obrigatório)",
  "modalidade": "FINIMP | REFINIMP | LEI4131 | NCE | BALCAOCAIXA | FGI",
  "moeda": "BRL | USD | EUR | JPY | CNY",
  "valorPrincipal": "decimal > 0 (obrigatório)",
  "dataContratacao": "YYYY-MM-DD (obrigatório)",
  "dataVencimento": "YYYY-MM-DD (obrigatório, > dataContratacao)",
  "taxaAa": "decimal > 0 (obrigatório)",
  "baseCalculo": "string (obrigatório)",
  "contratoPaiId": "guid (opcional, para refinanciamentos)",
  "observacoes": "string (opcional)",
  "finimpDetail": { ... },
  "lei4131Detail": { ... },
  "refinimpDetail": { ... },
  "nceDetail": { ... },
  "balcaoCaixaDetail": { ... },
  "fgiDetail": { ... }
}
```

> **Campos condicionais:** O objeto de detalhe correspondente à `modalidade` é **obrigatório**. Exemplo: se `modalidade = "FINIMP"`, o campo `finimpDetail` é obrigatório.

**finimpDetail (obrigatório para FINIMP):**
```json
{
  "rofNumero": "string",
  "rofDataEmissao": "YYYY-MM-DD",
  "exportadorNome": "string",
  "exportadorPais": "string",
  "produtoImportado": "string",
  "faturaReferencia": "string",
  "incoterm": "string",
  "breakFundingFeePercentual": "decimal",
  "temMarketFlex": "bool"
}
```

**lei4131Detail (obrigatório para LEI4131):**
```json
{
  "sblcNumero": "string",
  "sblcBancoEmissor": "string",
  "sblcValorUsd": "decimal",
  "temMarketFlex": "bool",
  "breakFundingFeePercentual": "decimal"
}
```

**refinimpDetail (obrigatório para REFINIMP):**
```json
{
  "contratoMaeId": "guid",
  "percentualRefinanciado": "decimal"
}
```

**Responses:**
- `201 Created` — [ContratoDto](./schemas.md#contradto) + `Location: /api/v1/contratos/{id}`
- `400 Bad Request` — Validação falhou
- `403 Forbidden` — Role insuficiente

---

### Atualizar Contrato (Parcial)

```
PATCH /api/v1/contratos/{id}
Autorização: Escrita
```

Atualização parcial — apenas os campos enviados com valor não-nulo são aplicados. Campos omitidos ou `null` permanecem inalterados.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do contrato |

**Request Body (todos os campos opcionais):**

```json
{
  "numeroExterno": "string | null",
  "taxaAa": "decimal | null",
  "dataVencimento": "YYYY-MM-DD | null",
  "observacoes": "string | null",
  "baseCalculo": "Dias252 | Dias360 | Dias365 | null",
  "periodicidade": "Bullet | Mensal | Bimestral | Trimestral | Semestral | Anual | null",
  "estruturaAmortizacao": "Bullet | Price | Sac | Customizada | null",
  "quantidadeParcelas": "int | null",
  "dataPrimeiroVencimento": "YYYY-MM-DD | null",
  "convencaoDataNaoUtil": "Following | ModifiedFollowing | Preceding | NoAdjustment | null"
}
```

**Validações:**
- `dataVencimento` deve ser posterior a `dataContratacao` original.
- `dataPrimeiroVencimento` deve ser posterior a `dataContratacao` original.
- `quantidadeParcelas` deve ser ≥ 1.
- Enums são validados por nome (case-insensitive).

**Responses:**
- `200 OK` — [ContratoDto](./schemas.md#contradto) atualizado
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Contrato não encontrado
- `403 Forbidden` — Role insuficiente

---

### Excluir Contrato

```
DELETE /api/v1/contratos/{id}
Autorização: Gerencial
```

**Responses:**
- `204 No Content` — Excluído com sucesso
- `404 Not Found` — Contrato não encontrado
- `403 Forbidden` — Role insuficiente

---

### Tabela Completa (Demonstrativo)

```
GET /api/v1/contratos/{id}/tabela-completa
Autorização: Leitura
```

Retorna o demonstrativo financeiro completo do contrato, com opção de exportar em PDF ou XLSX.

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `dataReferencia` | date | Data base para cálculo (padrão: hoje) |
| `cotacao` | decimal | Taxa de câmbio a usar (sobrescreve PTAX) |
| `formato` | string | `json` (padrão) \| `pdf` \| `xlsx` |

**Responses:**
- `200 OK` — `TabelaCompletaDto` (JSON) ou arquivo binário (PDF/XLSX)
- `400 Bad Request` — Parâmetros inválidos
- `404 Not Found` — Contrato não encontrado

> Exportações em PDF e XLSX são registradas no log de auditoria.

---

### Gerar Cronograma de Pagamentos

```
POST /api/v1/contratos/{id}/gerar-cronograma
Autorização: Escrita
```

Calcula e persiste o cronograma de parcelas do contrato com base nos parâmetros fiscais/operacionais.

**Request Body:**
```json
{
  "aliqIrrfPct": "decimal (opcional)",
  "aliqIofCambioPct": "decimal (opcional)",
  "tarifaRofBrl": "decimal (opcional)",
  "tarifaCadempBrl": "decimal (opcional)",
  "taxaFgiAaPct": "decimal (opcional)"
}
```

**Responses:**
- `200 OK` — `EventoCronogramaDto[]` (ver [schemas.md](./schemas.md#eventocronogramadto))
- `404 Not Found` — Contrato não encontrado
- `422 Unprocessable Entity` — Regra de negócio violada

---

### Importar Cronograma Manual

```
POST /api/v1/contratos/{id}/importar-cronograma
Autorização: Escrita
```

Substitui o cronograma calculado por um cronograma manual (informado parcela a parcela).

**Request Body:** `ParcelaManualRequest[]`
```json
[
  {
    "numero": "short (obrigatório)",
    "dataVencimento": "YYYY-MM-DD (obrigatório)",
    "valorPrincipal": "decimal (obrigatório)",
    "valorJuros": "decimal (obrigatório)"
  }
]
```

**Responses:**
- `200 OK` — `EventoCronogramaDto[]`
- `400 Bad Request` — Formato inválido
- `404 Not Found` — Contrato não encontrado
- `422 Unprocessable Entity` — Operação inválida para o status do contrato

---

### Simular Antecipação

```
POST /api/v1/contratos/{id}/simular-antecipacao
Autorização: Leitura
Idempotency-Key: recomendado
```

Calcula o custo de liquidação antecipada (total ou parcial) de um contrato.

**Request Body:**
```json
{
  "tipoAntecipacao": "TOTAL | PARCIAL (obrigatório)",
  "dataEfetiva": "YYYY-MM-DD (obrigatório)",
  "valorPrincipalAQuitarMoedaOriginal": "decimal (obrigatório se PARCIAL)",
  "taxaMercadoAtualAa": "decimal (opcional)",
  "indenizacaoBancoMoedaOriginal": "decimal (opcional)",
  "salvarSimulacao": "bool (padrão: true)"
}
```

**Responses:**
- `200 OK` — [ResultadoSimulacaoDto](./schemas.md#resultadosimulacaodto)
- `400 Bad Request` — Tipo inválido
- `404 Not Found` — Contrato não encontrado
- `422 Unprocessable Entity` — Operação inválida (ex.: contrato já liquidado)

---

## Garantias do Contrato

### Listar Garantias

```
GET /api/v1/contratos/{id}/garantias
Autorização: Leitura
```

**Responses:**
- `200 OK` — `GarantiaDto[]` (ver [schemas.md](./schemas.md#garantiadto))
- `404 Not Found` — Contrato não encontrado

---

### Indicadores de Garantias

```
GET /api/v1/contratos/{id}/garantias/indicadores
Autorização: Leitura
```

Retorna índices e cobertura agregada das garantias do contrato.

**Response 200 OK:**
```json
{
  "contratoId": "guid",
  "valorTotalGarantiasBrl": "decimal",
  "coberturaRatioPct": "decimal",
  "garantiasAtivas": "int",
  "alertas": ["string"]
}
```

---

### Adicionar Garantia

```
POST /api/v1/contratos/{id}/garantias
Autorização: Escrita
Idempotency-Key: recomendado
```

**Request Body:**
```json
{
  "tipo": "CDB | SBLC | AVAL | ALIENACAO | DUPLICATAS | RECEBIVEIS | BOLETO | FGI",
  "valorBrl": "decimal > 0 (obrigatório)",
  "dataConstituicao": "YYYY-MM-DD (obrigatório)",
  "dataLiberacaoPrevista": "YYYY-MM-DD (opcional)",
  "observacoes": "string (opcional)",
  "cdb": "GarantiaCdbPayload (obrigatório se tipo=CDB)",
  "sblc": "GarantiaSblcPayload (obrigatório se tipo=SBLC)",
  "aval": "GarantiaAvalPayload (obrigatório se tipo=AVAL)",
  "alienacao": "GarantiaAlienacaoPayload (obrigatório se tipo=ALIENACAO)",
  "duplicatas": "GarantiaDuplicatasPayload (obrigatório se tipo=DUPLICATAS)",
  "recebiveis": "GarantiaRecebiveisPayload (obrigatório se tipo=RECEBIVEIS)",
  "boleto": "GarantiaBoletoPayload (obrigatório se tipo=BOLETO)",
  "fgiDetail": "GarantiaFgiPayload (obrigatório se tipo=FGI)"
}
```

Ver payloads completos em [schemas.md](./schemas.md#payloads-de-garantia-por-tipo).

**Responses:**
- `201 Created` — [GarantiaDto](./schemas.md#garantiadto)
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Contrato não encontrado
- `422 Unprocessable Entity` — Regra de negócio violada

---

### Cancelar Garantia

```
DELETE /api/v1/contratos/{id}/garantias/{garantiaId}
Autorização: Gerencial
```

**Responses:**
- `204 No Content` — Cancelada com sucesso
- `404 Not Found` — Contrato ou garantia não encontrados
- `403 Forbidden` — Role insuficiente

---

## Hedges do Contrato

### Listar Hedges do Contrato

```
GET /api/v1/contratos/{id}/hedges
Autorização: Leitura
```

**Responses:**
- `200 OK` — `HedgeDto[]` (ver [schemas.md](./schemas.md#hedgedto))
- `404 Not Found` — Contrato não encontrado

---

### Adicionar Hedge ao Contrato

```
POST /api/v1/contratos/{id}/hedges
Autorização: Escrita
```

**Request Body:**
```json
{
  "tipo": "FORWARD | PUT | CALL (obrigatório)",
  "contraparteId": "guid (obrigatório)",
  "notionalMoedaOriginal": "decimal > 0 (obrigatório)",
  "moedaBase": "BRL | USD | EUR | JPY | CNY (obrigatório)",
  "dataContratacao": "YYYY-MM-DD (obrigatório)",
  "dataVencimento": "YYYY-MM-DD (obrigatório, > dataContratacao)",
  "strikeForward": "decimal (obrigatório se tipo=FORWARD)",
  "strikePut": "decimal (obrigatório se tipo=PUT)",
  "strikeCall": "decimal (obrigatório se tipo=CALL)"
}
```

**Responses:**
- `201 Created` — [HedgeDto](./schemas.md#hedgedto)
- `400 Bad Request` — Validação falhou
- `404 Not Found` — Contrato não encontrado
- `422 Unprocessable Entity` — Regra de negócio violada
- `403 Forbidden` — Role insuficiente
