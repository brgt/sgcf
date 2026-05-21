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

### Quadro da Dívida

```
GET /api/v1/painel/quadro-divida
Autorização: Leitura
```

Retorna, para um ano civil, o saldo da carteira de captações mês a mês com breakdown por banco, totais consolidados e variação anual. Reproduz a lógica da aba `Quadro_da_Divida` da planilha de Endividamento.

Quando `cenarioId` é informado, a projeção incorpora as captações hipotéticas do cenário de simulação sobre os dados reais da carteira (AD-9). O campo `cenarioAplicado` do DTO de resposta indica os metadados do cenário aplicado.

> **Restrição MVP (Q9):** apenas o ano corrente do servidor é suportado. Solicitar outro ano retorna `409 Conflict`.

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `ano` | int | Não | Ano civil de referência (ex.: `2026`). Quando omitido, usa o ano corrente do servidor (timezone `America/Sao_Paulo`). |
| `cenarioId` | guid | Não | Id de um cenário de simulação. Quando informado, incorpora as captações hipotéticas do cenário na projeção mensal. |

**Responses:**
- `200 OK` — `QuadroDividaDto`
- `400 Bad Request` — Ano fora do intervalo válido (2020–2050)
- `404 Not Found` — `cenarioId` informado não existe ou foi deletado
- `409 Conflict` — Ano informado diferente do ano corrente (restrição MVP Q9)

**Response 200 OK:**

```json
{
  "ano": 2026,
  "dataReferencia": "2026-05-19",
  "snapshotInicial": {
    "bancos": [
      {
        "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "bancoApelido": "Caixa",
        "bancoCodigoCompe": "104",
        "saldoBrl": 15000000.00,
        "quantidadeContratosAtivos": 3
      }
    ],
    "saldoTotalBrl": 15000000.00,
    "dataReferencia": "2026-05-19"
  },
  "projecao": {
    "meses": [
      {
        "ano": 2026,
        "mes": 1,
        "bancos": [
          {
            "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "bancoApelido": "Caixa",
            "saldoInicio": 15000000.00,
            "saldoFim": 14500000.00,
            "totalAmortizacaoNoMes": 500000.00,
            "totalCaptacaoNoMes": 0.00,
            "sharePercentual": 100.00
          }
        ],
        "saldoTotalInicio": 15000000.00,
        "saldoTotalFim": 14500000.00,
        "totalAmortizacaoMes": 500000.00,
        "totalCaptacaoMes": 0.00
      }
    ]
  },
  "sumario": {
    "saldoTotalInicioAno": 15000000.00,
    "saldoTotalFimAno": 9000000.00,
    "totalAmortizacaoNoAno": 6000000.00,
    "totalCaptacaoNoAno": 0.00,
    "variacaoAnualPercentual": -40.00
  },
  "alertas": [],
  "cenarioAplicado": null
}
```

**Campos da resposta:**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `ano` | int | Ano civil consultado |
| `dataReferencia` | date | Data em que o snapshot inicial foi calculado (hoje) |
| `snapshotInicial` | `SaldoPorBancoAtualDto` | Saldo atual por banco — base da projeção |
| `snapshotInicial.bancos[]` | array | Lista de bancos com saldo na carteira |
| `snapshotInicial.bancos[].bancoId` | guid | Identificador do banco |
| `snapshotInicial.bancos[].bancoApelido` | string | Apelido do banco (AD-10) |
| `snapshotInicial.bancos[].bancoCodigoCompe` | string | Código COMPE do banco |
| `snapshotInicial.bancos[].saldoBrl` | decimal | Saldo total em BRL (conversão via spot/PTAX corrente) |
| `snapshotInicial.bancos[].quantidadeContratosAtivos` | int | Contratos ativos do banco |
| `snapshotInicial.saldoTotalBrl` | decimal | Soma de `saldoBrl` de todos os bancos |
| `projecao.meses[]` | array | Exatamente 12 entradas — índice 0 = janeiro, índice 11 = dezembro |
| `projecao.meses[].ano` | int | Ano ao qual o mês pertence |
| `projecao.meses[].mes` | int | Número do mês (1–12) |
| `projecao.meses[].bancos[]` | array | Posição de cada banco no mês (inclui apenas bancos com saldo ou eventos) |
| `projecao.meses[].bancos[].saldoInicio` | decimal | Saldo em BRL no início do mês |
| `projecao.meses[].bancos[].saldoFim` | decimal | Saldo em BRL no fim do mês após eventos |
| `projecao.meses[].bancos[].totalAmortizacaoNoMes` | decimal | Soma das amortizações de principal do banco no mês em BRL |
| `projecao.meses[].bancos[].totalCaptacaoNoMes` | decimal | Soma das captações do banco no mês em BRL |
| `projecao.meses[].bancos[].sharePercentual` | decimal | Percentual do banco no saldo total de fechamento do mês |
| `projecao.meses[].saldoTotalInicio` | decimal | Soma de `saldoInicio` de todos os bancos do mês |
| `projecao.meses[].saldoTotalFim` | decimal | Soma de `saldoFim` de todos os bancos do mês |
| `projecao.meses[].totalAmortizacaoMes` | decimal | Total de amortizações de principal no mês em BRL |
| `projecao.meses[].totalCaptacaoMes` | decimal | Total de captações no mês em BRL |
| `sumario.saldoTotalInicioAno` | decimal | Saldo total no início do ano (= `saldoTotalInicio` do mês 1) |
| `sumario.saldoTotalFimAno` | decimal | Saldo total no fim do ano (= `saldoTotalFim` do mês 12) |
| `sumario.totalAmortizacaoNoAno` | decimal | Soma de todas as amortizações de principal no ano |
| `sumario.totalCaptacaoNoAno` | decimal | Soma de todas as captações no ano |
| `sumario.variacaoAnualPercentual` | decimal | `(SaldoFimAno − SaldoInicioAno) / SaldoInicioAno × 100`. Zero quando `SaldoInicioAno = 0` |
| `alertas[]` | string[] | Alertas contextuais. Populado quando o tetão mensal configurável é ultrapassado (Task 3.4) |
| `cenarioAplicado` | objeto\|null | Metadados do cenário aplicado. `null` quando `cenarioId` não foi informado |
| `cenarioAplicado.id` | guid | Identificador do cenário |
| `cenarioAplicado.nome` | string | Nome do cenário |
| `cenarioAplicado.status` | string | `Rascunho`, `Ativo` ou `Arquivado` |
| `cenarioAplicado.anoBase` | int | Ano-base das simulações do cenário |
| `cenarioAplicado.quantidadeSimulacoes` | int | Quantidade de captações hipotéticas no cenário |

> **Atalho por cenário:** o endpoint `GET /api/v1/simulacoes/cenarios/{id}/quadro-divida[?ano=YYYY]` é equivalente a chamar este endpoint com `cenarioId={id}`, usando `cenario.AnoBase` como ano padrão. Consulte [Simulações API](./simulacoes.md#atalho-quadro-da-dívida).

---

### VencimentoItemDto — Campos de Banco (AD-10)

O DTO `VencimentoItemDto`, retornado por `GET /api/v1/painel/vencimentos`, foi estendido com dois campos adicionais que identificam o banco credor do contrato:

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `bancoId` | guid | Identificador do banco credor |
| `bancoApelido` | string | Apelido do banco credor |

Esses campos permitem ao frontend agrupar vencimentos por banco sem realizar lookups adicionais.

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

---

## Endpoints do Cockpit Multi-Persona (Fases 1 e 2)

Os endpoints abaixo foram adicionados para suportar o cockpit multi-persona (CFO, Gerente Financeiro, Gerente de Tesouraria). Eles seguem o envelope `{ data, meta }` definido no ADR-019. Para os indicadores que o frontend resolve com os endpoints existentes sem novo código de backend, consulte o [Cockpit FE Guide](./cockpit-fe-guide.md).

### Fase 1 — Cockpit CFO

#### Breakdown da Dívida por Modalidade

```
GET /api/v1/painel/divida/breakdown-modalidade
Autorização: Leitura
```

Agrega contratos `Ativo` por `ModalidadeContrato`, convertendo para BRL com a estratégia spot → PTAX D-1. Implementado na Task 1.1 (GAP-CKP-01).

**Response 200 OK:** `EnvelopeResponse<BreakdownModalidadeDto>`

```json
{
  "data": {
    "dataHoraCalculo": "2026-05-21T14:00:00Z",
    "totalBrl": 45000000.00,
    "itens": [
      { "modalidade": "Finimp", "valorBrl": 28000000.00, "percentualPct": 62.2, "quantidadeContratos": 7 },
      { "modalidade": "Lei4131", "valorBrl": 12000000.00, "percentualPct": 26.7, "quantidadeContratos": 3 },
      { "modalidade": "Fgi", "valorBrl": 5000000.00, "percentualPct": 11.1, "quantidadeContratos": 4 }
    ]
  },
  "meta": {
    "dataHoraCalculo": "2026-05-21T14:00:00Z",
    "fontesConsultadas": [ { "fonte": "contratos", "status": "OK", "registros": 14 } ],
    "completude": "COMPLETO"
  }
}
```

> `Σ itens[].valorBrl` == `GET /api/v1/painel/divida` → `dividaBrutaBrl` (consistência garantida).

---

#### Curva de Vencimentos Multi-Ano

```
GET /api/v1/painel/vencimentos/horizonte
Autorização: Leitura
```

Retorna vencimentos projetados de parcelas ativas com granularidade configurável. Implementado na Task 1.2 (GAP-CKP-03).

**Query Parameters:**

| Parâmetro | Tipo | Padrão | Descrição |
|-----------|------|--------|-----------|
| `meses` | int | `36` | Horizonte em meses: `12`, `24`, `36` ou `60` |
| `granularidade` | string | `mes` | `mes`, `trimestre` ou `ano` |
| `bancoId` | guid | — | Opcional |
| `modalidade` | string | — | Opcional |
| `moeda` | string | — | Opcional |

**Response 200 OK:** `EnvelopeResponse<CurvaVencimentosDto>` com buckets etiquetados como `YYYY-MM`, `YYYY-Qx` ou `YYYY`.

---

#### Estrutura de Capital

```
GET /api/v1/painel/estrutura-capital
Autorização: Leitura
```

Calcula Dívida/PL e ICR (EBITDA / Despesa Financeira) para o cockpit do CFO. Requer que dados contábeis estejam cadastrados via `POST /painel/dados-contabeis`. Implementado na Task 1.3 (GAP-CKP-04).

**Response 200 OK:** `EnvelopeResponse<EstruturaCapitalDto>`

```json
{
  "data": {
    "dividaTotalBrl": 45000000.00,
    "patrimonioLiquidoBrl": 32000000.00,
    "dividaSobrePatrimonio": 1.41,
    "ebitdaUltimos12mBrl": 18000000.00,
    "despesaFinanceira12mBrl": 3200000.00,
    "icr": 5.63,
    "alertas": []
  },
  "meta": { "completude": "COMPLETO", ... }
}
```

> Quando dados contábeis não estão cadastrados, `meta.completude = "PARCIAL"` e `alertas` contém `"DADOS_CONTABEIS_AUSENTES"`.

---

#### Registrar Dados Contábeis

```
POST /api/v1/painel/dados-contabeis
Autorização: Auditoria
```

Cria ou atualiza dados contábeis mensais (Patrimônio Líquido e Despesa Financeira). Semanticamente idêntico ao `POST /painel/ebitda`. Implementado na Task 1.3.

**Request Body:**

```json
{
  "ano": 2026,
  "mes": 5,
  "patrimonioLiquidoBrl": 32000000.00,
  "despesaFinanceiraBrl": 3200000.00
}
```

**Responses:**
- `204 No Content` — Registrado com sucesso
- `400 Bad Request` — Parâmetros inválidos
- `403 Forbidden` — Role insuficiente

---

### Fase 2 — Cockpit Gerente Financeiro

#### Inadimplência Agregada (em progresso — Task 2.1)

```
GET /api/v1/painel/inadimplencia
Autorização: Leitura
```

Retorna dias de atraso médio (ponderado por valor em mora) e distribuição por bucket de atraso. Implementado na Task 2.1 (GAP-CKP-07).

**Status:** em desenvolvimento (Sprint 4).

**Response 200 OK (prevista):** `EnvelopeResponse<InadimplenciaDto>`

```json
{
  "data": {
    "totalEmMoraBrl": 3200000.00,
    "diasAtrasoMedio": 22.4,
    "buckets": [
      { "faixa": "1-15", "quantidadeContratos": 3, "valorBrl": 800000.00 },
      { "faixa": "16-30", "quantidadeContratos": 2, "valorBrl": 1200000.00 },
      { "faixa": "31-60", "quantidadeContratos": 1, "valorBrl": 900000.00 },
      { "faixa": "60+",   "quantidadeContratos": 1, "valorBrl": 300000.00 }
    ]
  },
  "meta": { "completude": "COMPLETO", ... }
}
```
