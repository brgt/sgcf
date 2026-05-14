# Simulador API

**Base route:** `/api/v1/simulador`

Fornece simulações financeiras sobre a carteira consolidada. Os resultados **não persistem** no banco de dados (exceto quando explicitamente indicado).

> Todos os endpoints requerem role `Executivo` (tesouraria, gerente, diretor ou admin).

---

## Endpoints

### Simular Cenário Cambial

```
POST /api/v1/simulador/cenario-cambial
Autorização: Executivo
```

Calcula o impacto na dívida total consolidada dado um choque nas taxas de câmbio. Retorna automaticamente quatro cenários: o personalizado e três padronizados.

**Request Body:**
```json
{
  "deltaUsdPct": -5.0,
  "deltaEurPct": 0.0,
  "deltaJpyPct": 0.0,
  "deltaCnyPct": 0.0
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `deltaUsdPct` | decimal (opcional) | Variação % do USD vs. cotação atual |
| `deltaEurPct` | decimal (opcional) | Variação % do EUR vs. cotação atual |
| `deltaJpyPct` | decimal (opcional) | Variação % do JPY vs. cotação atual |
| `deltaCnyPct` | decimal (opcional) | Variação % do CNY vs. cotação atual |

Campos omitidos assumem variação `0%` (cotação atual).

**Response 200 OK:** `ResultadoCenarioCambialDto`

```json
{
  "cenarios": [
    {
      "nome": "Personalizado",
      "deltas": {
        "usdPct": -5.0,
        "eurPct": 0.0,
        "jpyPct": 0.0,
        "cnyPct": 0.0
      },
      "dividaBrutaBrl": 19750000.00,
      "dividaLiquidaBrl": 19625000.00,
      "variacaoBrutaPct": -5.0,
      "variacaoLiquidaPct": -5.0
    },
    {
      "nome": "Pessimista",
      "deltas": { "usdPct": 10.0, "eurPct": 10.0, "jpyPct": 10.0, "cnyPct": 10.0 },
      "dividaBrutaBrl": 22858110.00,
      "dividaLiquidaBrl": 22710310.00,
      "variacaoBrutaPct": 10.0,
      "variacaoLiquidaPct": 10.0
    },
    {
      "nome": "Base",
      "deltas": { "usdPct": 0.0, "eurPct": 0.0, "jpyPct": 0.0, "cnyPct": 0.0 },
      "dividaBrutaBrl": 20780100.00,
      "dividaLiquidaBrl": 20655100.00,
      "variacaoBrutaPct": 0.0,
      "variacaoLiquidaPct": 0.0
    },
    {
      "nome": "Otimista",
      "deltas": { "usdPct": -10.0, "eurPct": -10.0, "jpyPct": -10.0, "cnyPct": -10.0 },
      "dividaBrutaBrl": 18702090.00,
      "dividaLiquidaBrl": 18589590.00,
      "variacaoBrutaPct": -10.0,
      "variacaoLiquidaPct": -10.0
    }
  ],
  "dataHoraCalculo": "2026-03-15T14:30:00Z"
}
```

**Responses:**
- `200 OK` — `ResultadoCenarioCambialDto`
- `403 Forbidden` — Role insuficiente

---

### Simular Antecipação de Portfólio

```
POST /api/v1/simulador/antecipacao-portfolio
Autorização: Executivo
```

Dado um valor disponível em caixa, calcula e ranqueia os contratos que oferecem maior economia líquida em caso de liquidação antecipada.

**Request Body:**
```json
{
  "caixaDisponivelBrl": 5000000.00,
  "taxaCdiAa": 13.75
}
```

| Campo | Tipo | Validação | Descrição |
|-------|------|-----------|-----------|
| `caixaDisponivelBrl` | decimal | Obrigatório, > 0 | Valor disponível para antecipação |
| `taxaCdiAa` | decimal | Opcional | CDI anual atual (para calcular custo de oportunidade) |

**Response 200 OK:** `ResultadoAntecipacaoPortfolioDto`

```json
{
  "caixaDisponivelBrl": 5000000.00,
  "taxaCdiAa": 13.75,
  "rankingContratos": [
    {
      "posicao": 1,
      "contratoId": "guid",
      "numeroExterno": "FINIMP-2025-003",
      "custoAtualAaPct": 7.25,
      "valorNecessarioBrl": 1850000.00,
      "economiaLiquidaBrl": 245000.00,
      "tir": 14.2,
      "recomendacao": "Antecipar — custo acima do CDI"
    },
    {
      "posicao": 2,
      "contratoId": "guid",
      "numeroExterno": "LEI4131-2024-001",
      "custoAtualAaPct": 6.80,
      "valorNecessarioBrl": 2200000.00,
      "economiaLiquidaBrl": 185000.00,
      "tir": 13.9,
      "recomendacao": "Antecipar — custo acima do CDI"
    }
  ],
  "totalEconomiaPotencialBrl": 430000.00,
  "dataHoraCalculo": "2026-03-15T14:30:00Z"
}
```

> Retorna os **top 5 contratos** com maior economia líquida. Contratos com custo inferior ao CDI informado são marcados como "Manter".

**Responses:**
- `200 OK` — `ResultadoAntecipacaoPortfolioDto`
- `400 Bad Request` — `caixaDisponivelBrl <= 0`
- `403 Forbidden` — Role insuficiente

---

## Simulação por Contrato Individual

Para simular antecipação de um contrato específico, use:

```
POST /api/v1/contratos/{id}/simular-antecipacao
```

Ver [contratos.md — Simular Antecipação](./contratos.md#simular-antecipação).
