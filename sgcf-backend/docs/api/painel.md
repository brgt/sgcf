# Painel API

**Base route:** `/api/v1/painel`

Fornece visões consolidadas da carteira de captações: dívida total, garantias, calendário de vencimentos e KPIs executivos.

---

## Endpoints

### Painel de Dívida Consolidada

```
GET /api/v1/painel/divida
Autorização: Leitura
```

Retorna a dívida bruta e líquida consolidada de toda a carteira, breakdowns por moeda e o ajuste de MTM dos hedges ativos.

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `bancoId` | guid | Filtra por banco |
| `modalidade` | string | Ver enum [ModalidadeContrato](./schemas.md#modalidadecontrato) |

**Response 200 OK:**
```json
{
  "dataHoraCalculo": "2026-03-15T14:30:00Z",
  "tipoCotacao": "PTAX",
  "breakdownPorMoeda": [
    {
      "moeda": "USD",
      "saldoMoedaOriginal": 3000000.00,
      "cotacaoAplicada": 5.8732,
      "saldoBrl": 17619600.00,
      "quantidadeContratos": 5
    },
    {
      "moeda": "EUR",
      "saldoMoedaOriginal": 500000.00,
      "cotacaoAplicada": 6.3210,
      "saldoBrl": 3160500.00,
      "quantidadeContratos": 2
    }
  ],
  "dividaBrutaBrl": 20780100.00,
  "ajusteMtm": {
    "mtmAReceberBrl": 125000.00,
    "mtmAPagarBrl": 0.00,
    "mtmLiquidoBrl": 125000.00
  },
  "dividaLiquidaPosHedgeBrl": 20655100.00,
  "alertas": [
    "Contrato FINIMP-2025-003 vence em 7 dias sem hedge ativo."
  ]
}
```

**Responses:**
- `200 OK` — `PainelDividaDto`
- `401 Unauthorized`

---

### Painel de Garantias

```
GET /api/v1/painel/garantias
Autorização: Leitura
```

Retorna visão consolidada de todas as garantias ativas da carteira, com breakdowns por tipo e alertas de cobertura.

**Response 200 OK:** `PainelGarantiasDto`

```json
{
  "totalGarantiasBrl": "decimal",
  "coberturaPct": "decimal",
  "breakdownPorTipo": [
    {
      "tipo": "CDB | SBLC | AVAL | ...",
      "valorBrl": "decimal",
      "quantidade": "int"
    }
  ],
  "garantiasAVencer30Dias": "int",
  "alertas": ["string"]
}
```

---

### Calendário de Vencimentos

```
GET /api/v1/painel/vencimentos
Autorização: Leitura
```

Retorna o calendário de vencimentos de parcelas abertas para um ano específico, agrupadas por mês, com detalhamento diário por contrato. Valores em BRL convertidos via spot ou PTAX.

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `ano` | int | **Sim** | Ano de referência (ex.: `2026`) |
| `bancoId` | guid | Não | Filtra por banco |
| `modalidade` | string | Não | Ver enum [ModalidadeContrato](./schemas.md#modalidadecontrato) |
| `moeda` | string | Não | Ver enum [Moeda](./schemas.md#moeda) |
| `cdiAnualPct` | decimal | Não | CDI anual em % (ex.: `14.75`). Quando informado, preenche os campos `jurosBrlProjetado` e `totalJurosBrlProjetado` para contratos indexados ao CDI cujos juros foram importados como zero. |

**Responses:**
- `200 OK` — `CalendarioVencimentosDto`
- `400 Bad Request` — Parâmetro `ano` ausente

**Response 200 OK:**
```json
{
  "ano": 2026,
  "taxaCdiUsadaPct": 14.75,
  "totalAnoBrl": 666666.68,
  "meses": [
    {
      "ano": 2026,
      "mes": 3,
      "totalPrincipalBrl": 0.00,
      "totalJurosBrl": 0.00,
      "totalBrl": 0.00,
      "quantidadeParcelas": 1,
      "totalJurosBrlProjetado": 92419.44,
      "parcelas": [
        {
          "data": "2026-03-26",
          "contratoId": "019e21cc-102f-79c0-b2c1-48ad8fef9d86",
          "numeroContrato": "CEF-CCB-14.4266.737.0000158",
          "principalBrl": 0.00,
          "jurosBrl": 0.00,
          "totalBrl": 0.00,
          "jurosBrlProjetado": 92419.44
        }
      ]
    }
  ]
}
```

**Campos da resposta:**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `ano` | int | Ano consultado |
| `taxaCdiUsadaPct` | decimal\|null | CDI informado na query. `null` se `cdiAnualPct` não foi passado |
| `totalAnoBrl` | decimal | Soma de `totalBrl` de todos os meses |
| `meses[].ano` | int | Ano do mês |
| `meses[].mes` | int | Número do mês (1–12) |
| `meses[].totalPrincipalBrl` | decimal | Soma de principal do mês em BRL |
| `meses[].totalJurosBrl` | decimal | Soma de juros reais do mês em BRL |
| `meses[].totalBrl` | decimal | `totalPrincipalBrl + totalJurosBrl` |
| `meses[].quantidadeParcelas` | int | Número de parcelas no mês |
| `meses[].totalJurosBrlProjetado` | decimal\|null | Soma dos juros projetados pelo CDI. `null` se não solicitado |
| `meses[].parcelas[].data` | string | Data exata no formato `YYYY-MM-DD` |
| `meses[].parcelas[].contratoId` | guid | UUID do contrato |
| `meses[].parcelas[].numeroContrato` | string | Número externo do contrato |
| `meses[].parcelas[].principalBrl` | decimal | Principal em BRL |
| `meses[].parcelas[].jurosBrl` | decimal | Juros reais em BRL (0 para contratos CDI importados sem taxa) |
| `meses[].parcelas[].totalBrl` | decimal | `principalBrl + jurosBrl` |
| `meses[].parcelas[].jurosBrlProjetado` | decimal\|null | Juros projetados via CDI flat. Fórmula: `saldo × ((1 + (cdi + spread) / 100)^(dias / base) − 1)`. `null` se não solicitado |

> **Sobre `jurosBrlProjetado`:** aplicado somente a contratos CDI cujos eventos de juros foram importados com `valorJuros = 0` (taxa flutuante desconhecida na data de importação). O spread do contrato (`taxaAa`) é somado ao CDI informado para calcular a taxa efetiva. A base de cálculo (252/360/365) vem do cadastro do contrato.

---

### KPIs Executivos

```
GET /api/v1/painel/kpis
Autorização: Executivo
```

Retorna os principais indicadores financeiros da carteira para o dashboard executivo.

> Requer role: `tesouraria`, `gerente`, `diretor` ou `admin`.

**Response 200 OK:**
```json
{
  "dividaTotalBrl": 20780100.00,
  "dividaLiquidaBrl": 20655100.00,
  "dividaEbitda": 3.2,
  "sharePorBanco": [
    {
      "bancoId": "guid",
      "valorBrl": 12000000.00,
      "percentualPct": 57.75
    }
  ],
  "custoMedioPonderadoAaPct": 5.73,
  "prazoMedioRemanescenteDias": 412,
  "comparativo": {
    "dividaTotalBrlMesAnterior": 21500000.00,
    "dividaLiquidaBrlMesAnterior": 21250000.00,
    "variacaoDividaTotalPct": -3.35,
    "variacaoDividaLiquidaPct": -2.80
  }
}
```

| Campo | Descrição |
|-------|-----------|
| `dividaEbitda` | Razão Dívida Líquida / EBITDA. `null` se EBITDA não cadastrado |
| `custoMedioPonderadoAaPct` | Custo médio ponderado da carteira ao ano |
| `prazoMedioRemanescenteDias` | Prazo médio ponderado até vencimento |
| `comparativo` | Comparação com mês anterior. `null` se não houver dado anterior |

**Responses:**
- `200 OK` — `KpiDto`
- `403 Forbidden` — Role insuficiente

---

### Registrar EBITDA Mensal

```
POST /api/v1/painel/ebitda
Autorização: Auditoria
```

Cria ou atualiza o EBITDA de um mês específico. Usado para calcular o índice Dívida/EBITDA nos KPIs.

> Requer role: `contabilidade`, `auditor` ou `admin`.

**Request Body:**
```json
{
  "ano": 2026,
  "mes": 3,
  "valorBrl": 6450000.00
}
```

| Campo | Tipo | Validação |
|-------|------|-----------|
| `ano` | int | Obrigatório |
| `mes` | int | Obrigatório, 1–12 |
| `valorBrl` | decimal | Obrigatório |

**Responses:**
- `204 No Content` — Registrado com sucesso
- `400 Bad Request` — Mês inválido ou valor inválido
- `403 Forbidden` — Role insuficiente
