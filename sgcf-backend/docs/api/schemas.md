# Schemas Compartilhados

Tipos, enums e DTOs usados em múltiplos endpoints da SGCF API.

---

## Enums

### Moeda

| Valor | Descrição |
|-------|-----------|
| `BRL` | Real Brasileiro |
| `USD` | Dólar Americano |
| `EUR` | Euro |
| `JPY` | Iene Japonês |
| `CNY` | Yuan Chinês |

---

### ModalidadeContrato

| Valor | Descrição |
|-------|-----------|
| `FINIMP` | Financiamento à Importação |
| `REFINIMP` | Refinanciamento de Importação |
| `LEI4131` | Captação via Lei 4.131 |
| `NCE` | Nota de Crédito à Exportação |
| `BALCAOCAIXA` | Captação Balcão/Caixa |
| `FGI` | Fundo de Garantia para Investimentos |

---

### StatusContrato

| Valor | Descrição |
|-------|-----------|
| `ATIVO` | Contrato em vigor |
| `LIQUIDADO` | Pago integralmente |
| `VENCIDO` | Prazo expirado sem pagamento |
| `INADIMPLENTE` | Em atraso |
| `CANCELADO` | Cancelado antes do vencimento |
| `REFINANCIADOPARCIAL` | Parcialmente refinanciado |
| `REFINANCIADOTOTAL` | Totalmente refinanciado |

---

### TipoGarantia

| Valor | Descrição |
|-------|-----------|
| `CDB` | Certificado de Depósito Bancário |
| `SBLC` | Stand-by Letter of Credit |
| `AVAL` | Aval de sócio/empresa |
| `ALIENACAO` | Alienação fiduciária |
| `DUPLICATAS` | Caução de duplicatas |
| `RECEBIVEIS` | Cessão de recebíveis |
| `BOLETO` | Caução de boletos |
| `FGI` | Garantia do Fundo de Garantia p/ Investimentos |

---

### TipoHedge

| Valor | Descrição |
|-------|-----------|
| `FORWARD` | Contrato a termo de câmbio |
| `PUT` | Opção de venda |
| `CALL` | Opção de compra |

---

### TipoAntecipacao

| Valor | Descrição |
|-------|-----------|
| `TOTAL` | Liquidação total do contrato |
| `PARCIAL` | Amortização parcial |

---

## DTOs

### ContratoDto

```json
{
  "id": "guid",
  "numeroExterno": "string",
  "codigoInterno": "string",
  "bancoId": "guid",
  "modalidade": "FINIMP | REFINIMP | LEI4131 | NCE | BALCAOCAIXA | FGI",
  "moeda": "BRL | USD | EUR | JPY | CNY",
  "valorPrincipal": "decimal",
  "dataContratacao": "YYYY-MM-DD",
  "dataVencimento": "YYYY-MM-DD",
  "taxaAa": "decimal",
  "baseCalculo": "string",
  "status": "ATIVO | LIQUIDADO | ...",
  "temHedge": "bool",
  "temGarantia": "bool",
  "temAlerta": "bool",
  "observacoes": "string | null",
  "contratoPaiId": "guid | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### BancoDto

```json
{
  "id": "guid",
  "codigoCompe": "string",
  "razaoSocial": "string",
  "apelido": "string",
  "aceitaLiquidacaoTotal": "bool",
  "aceitaLiquidacaoParcial": "bool",
  "exigeAnuenciaExpressa": "bool",
  "exigeParcelaInteira": "bool",
  "avisoPrevioMinDiasUteis": "int",
  "padraoAntecipacao": "string",
  "valorMinimoParcialPct": "decimal | null",
  "breakFundingFeePct": "decimal | null",
  "tlaPctSobreSaldo": "decimal | null",
  "tlaPctPorMesRemanescente": "decimal | null",
  "observacoesAntecipacao": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### GarantiaDto

```json
{
  "id": "guid",
  "contratoId": "guid",
  "tipo": "CDB | SBLC | AVAL | ...",
  "valorBrl": "decimal",
  "dataConstituicao": "YYYY-MM-DD",
  "dataLiberacaoPrevista": "YYYY-MM-DD | null",
  "observacoes": "string | null",
  "ativa": "bool",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### HedgeDto

```json
{
  "id": "guid",
  "contratoId": "guid",
  "tipo": "FORWARD | PUT | CALL",
  "contraparteId": "guid",
  "notionalMoedaOriginal": "decimal",
  "moedaBase": "BRL | USD | EUR | JPY | CNY",
  "dataContratacao": "YYYY-MM-DD",
  "dataVencimento": "YYYY-MM-DD",
  "strikeForward": "decimal | null",
  "strikePut": "decimal | null",
  "strikeCall": "decimal | null",
  "ativo": "bool",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### EventoCronogramaDto

```json
{
  "id": "guid",
  "contratoId": "guid",
  "numero": "short",
  "dataVencimento": "YYYY-MM-DD",
  "valorPrincipal": "decimal",
  "valorJuros": "decimal",
  "valorTotal": "decimal",
  "status": "string"
}
```

---

### PlanoContasDto

```json
{
  "id": "guid",
  "nome": "string",
  "natureza": "string",
  "codigoSapB1": "string | null",
  "ativo": "bool",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### ParametroCotacaoDto

```json
{
  "id": "guid",
  "tipoCotacao": "string",
  "ativo": "bool",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### MtmResultadoDto

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

---

### ResultadoSimulacaoDto

```json
{
  "contratoId": "guid",
  "tipoAntecipacao": "TOTAL | PARCIAL",
  "dataEfetiva": "YYYY-MM-DD",
  "valorPrincipalQuitado": "decimal",
  "jurosDevidos": "decimal",
  "breakFundingFee": "decimal",
  "indenizacaoBanco": "decimal",
  "totalPagamentoBrl": "decimal",
  "economia": "decimal",
  "tir": "decimal | null"
}
```

---

### Problem Details (Erros)

```json
{
  "type": "string (URI)",
  "title": "string",
  "status": "int",
  "detail": "string",
  "errors": {
    "campo": ["mensagem de erro"]
  }
}
```

---

## Payloads de Garantia por Tipo

### GarantiaCdbPayload
```json
{
  "banco": "string",
  "numeroAplicacao": "string",
  "dataVencimentoCdb": "YYYY-MM-DD"
}
```

### GarantiaSblcPayload
```json
{
  "bancoEmissor": "string",
  "numero": "string",
  "dataVencimentoSblc": "YYYY-MM-DD",
  "valorUsd": "decimal"
}
```

### GarantiaAvalPayload
```json
{
  "avalista": "string",
  "cpfCnpj": "string"
}
```

### GarantiaAlienacaoPayload
```json
{
  "descricaoBem": "string",
  "registroCartorio": "string | null"
}
```

### GarantiaDuplicatasPayload
```json
{
  "quantidadeDuplicatas": "int",
  "valorFaceTotal": "decimal"
}
```

### GarantiaRecebiveisPayload
```json
{
  "cedente": "string",
  "valorCedido": "decimal"
}
```

### GarantiaBoletoPayload
```json
{
  "quantidadeBoletos": "int",
  "valorFaceTotal": "decimal"
}
```

### GarantiaFgiPayload
```json
{
  "numeroContrato": "string",
  "percentualCobertura": "decimal"
}
```
