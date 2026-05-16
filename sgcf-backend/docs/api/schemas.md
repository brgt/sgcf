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
| `Ativo` | Contrato em vigor |
| `Liquidado` | Pago integralmente |
| `Vencido` | Prazo expirado sem pagamento |
| `Inadimplente` | Em atraso |
| `Cancelado` | Cancelado antes do vencimento |
| `RefinanciadoParcial` | Parcialmente refinanciado (< 100% do principal) |
| `RefinanciadoTotal` | Totalmente refinanciado (≥ 100% do principal) |

---

### Periodicidade

| Valor | Descrição |
|-------|-----------|
| `Bullet` | Pagamento único no vencimento (padrão) |
| `Mensal` | Parcelas mensais |
| `Bimestral` | Parcelas bimestrais |
| `Trimestral` | Parcelas trimestrais |
| `Semestral` | Parcelas semestrais |
| `Anual` | Parcelas anuais |

---

### EstruturaAmortizacao

| Valor | Descrição |
|-------|-----------|
| `Bullet` | Principal único no vencimento (padrão) |
| `Price` | Parcelas iguais — sistema francês |
| `Sac` | Amortização constante — parcelas decrescentes |
| `Customizada` | Parcelas manuais via importação |

---

### AnchorDiaMes

| Valor | Descrição |
|-------|-----------|
| `DiaContratacao` | Vencimento no mesmo dia do mês da contratação (padrão) |
| `DiaFixo` | Vencimento em dia fixo do mês (requer `anchorDiaFixo` 1–31) |
| `UltimoDiaMes` | Vencimento sempre no último dia útil do mês |

---

### ConvencaoDataNaoUtil

| Valor | Descrição |
|-------|-----------|
| `Following` | Move para o próximo dia útil (padrão) |
| `ModifiedFollowing` | Próximo dia útil, sem cruzar o mês |
| `Preceding` | Move para o dia útil anterior |
| `NoAdjustment` | Mantém a data original sem ajuste |

---

### EscopoFeriado

| Valor | Descrição |
|-------|-----------|
| `Nacional` | Feriado nacional — afeta o motor de cronograma |
| `Estadual` | Feriado estadual — registrado, não afeta cronograma no MVP |
| `Municipal` | Feriado municipal — registrado, não afeta cronograma no MVP |

---

### TipoFeriado

| Valor | Descrição |
|-------|-----------|
| `FixoCalendario` | Data fixa todo ano (ex.: 1° de janeiro) |
| `MovelCalendario` | Data variável calculada (ex.: Carnaval, Páscoa) |
| `Pontual` | Feriado de ocorrência única |

---

### FonteFeriado

| Valor | Descrição |
|-------|-----------|
| `Manual` | Criado manualmente via API |
| `Anbima` | Ingerido automaticamente da base ANBIMA |

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

### StatusCotacao

| Valor | Descrição |
|-------|-----------|
| `Rascunho` | Cotação recém criada; editável |
| `EmCaptacao` | Enviada aos bancos; aceita propostas |
| `Comparada` | Captação encerrada; habilita aceitação |
| `Aceita` | Proposta vencedora aceita; aguardando conversão |
| `Convertida` | Contrato gerado (estado final) |
| `Recusada` | Cotação cancelada com motivo (estado final) |

---

### StatusProposta

| Valor | Descrição |
|-------|-----------|
| `Recebida` | Proposta cadastrada; editável e elegível para aceitação |
| `Aceita` | Proposta vencedora (única por cotação) |
| `Recusada` | Descartada explicitamente |
| `Expirada` | `dataValidadeMercado` ultrapassada |

---

## DTOs

### ContratoDto

```json
{
  "id": "guid",
  "numeroExterno": "string",
  "codigoInterno": "string | null",
  "bancoId": "guid",
  "modalidade": "Finimp | Refinimp | Lei4131 | Nce | BalcaoCaixa | Fgi",
  "moeda": "Brl | Usd | Eur | Jpy | Cny",
  "valorPrincipal": "decimal",
  "dataContratacao": "YYYY-MM-DD",
  "dataVencimento": "YYYY-MM-DD",
  "taxaAa": "decimal",
  "baseCalculo": "Dias252 | Dias360 | Dias365",
  "periodicidade": "Bullet | Mensal | Bimestral | Trimestral | Semestral | Anual",
  "estruturaAmortizacao": "Bullet | Price | Sac | Customizada",
  "quantidadeParcelas": "int",
  "dataPrimeiroVencimento": "YYYY-MM-DD",
  "anchorDiaMes": "DiaContratacao | DiaFixo | UltimoDiaMes",
  "anchorDiaFixo": "int (1–31) | null",
  "periodicidadeJuros": "Bullet | Mensal | ... | null",
  "convencaoDataNaoUtil": "Following | ModifiedFollowing | Preceding | NoAdjustment",
  "status": "Ativo | Liquidado | Vencido | Inadimplente | Cancelado | RefinanciadoParcial | RefinanciadoTotal",
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

### FeriadoDto

```json
{
  "id": "guid",
  "data": "YYYY-MM-DD",
  "descricao": "string",
  "abrangencia": "Nacional | Estadual | Municipal",
  "tipo": "FixoCalendario | MovelCalendario | Pontual",
  "fonte": "Manual | Anbima",
  "createdAt": "DateTimeOffset (ISO 8601)"
}
```

---

### LancamentoContabilDto

```json
{
  "id": "guid",
  "contratoId": "guid",
  "planoContaId": "guid",
  "data": "YYYY-MM-DD",
  "origem": "string",
  "valor": "decimal",
  "moeda": "string",
  "descricao": "string",
  "createdAt": "DateTimeOffset"
}
```

> `planoContaId` corresponde ao `contaId` usado no path do endpoint (`/plano-contas/{contaId}/lancamentos`).

---

### AuditEventoDto

```json
{
  "id": "long (bigserial)",
  "occurredAt": "DateTimeOffset (ISO 8601, UTC)",
  "actorSub": "string — claim 'sub' do JWT ou 'system' para jobs",
  "actorRole": "string — role do autor",
  "source": "rest | mcp | a2a | job",
  "entity": "string — nome da entidade C# (ex.: 'Contrato')",
  "entityId": "guid | null",
  "operation": "CREATE | UPDATE | DELETE",
  "diffJson": "string JSON | null — estrutura { before: {...}, after: {...} }",
  "requestId": "guid — correlation ID da requisição"
}
```

> `ipHash` é intencionalmente omitido da resposta para proteção de privacidade (LGPD).

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

---

## DTOs — Cotações

### CotacaoDto

```json
{
  "id": "guid",
  "codigoInterno": "string",
  "modalidade": "Finimp",
  "valorAlvoBrl": "decimal",
  "prazoMaximoDias": "int",
  "dataAbertura": "YYYY-MM-DD",
  "dataPtaxReferencia": "YYYY-MM-DD",
  "ptaxUsadaUsdBrl": "decimal",
  "status": "Rascunho | EmCaptacao | Comparada | Aceita | Convertida | Recusada",
  "propostaAceitaId": "guid | null",
  "contratoGeradoId": "guid | null",
  "aceitaPor": "string | null",
  "dataAceitacao": "DateTimeOffset | null",
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset",
  "bancosAlvo": "guid[]",
  "propostas": "PropostaDto[]"
}
```

### PropostaDto

```json
{
  "id": "guid",
  "cotacaoId": "guid",
  "bancoId": "guid",
  "moedaOriginal": "Brl | Usd | Eur | Jpy | Cny",
  "valorOferecidoMoedaOriginal": "decimal",
  "taxaAaPercentual": "decimal",
  "iofPercentual": "decimal",
  "spreadAaPercentual": "decimal",
  "prazoDias": "int",
  "estruturaAmortizacao": "Bullet | Price | Sac",
  "periodicidadeJuros": "Bullet | Mensal | Bimestral | Trimestral | Semestral | Anual",
  "exigeNdf": "bool",
  "custoNdfAaPercentual": "decimal | null",
  "garantiaExigida": "string",
  "valorGarantiaExigidaBrl": "decimal",
  "garantiaEhCdbCativo": "bool",
  "rendimentoCdbAaPercentual": "decimal | null",
  "cetCalculadoAaPercentual": "decimal | null",
  "valorTotalEstimadoBrl": "decimal | null",
  "dataCaptura": "YYYY-MM-DD",
  "dataValidadeMercado": "YYYY-MM-DD | null",
  "status": "Recebida | Aceita | Recusada | Expirada",
  "motivoRecusa": "string | null"
}
```

### ComparativoDto

```json
{
  "propostaId": "guid",
  "bancoId": "guid",
  "moedaOriginal": "string",
  "prazoDias": "int",
  "taxaNominalAaPercentual": "decimal",
  "cetAaPercentual": "decimal",
  "custoTotalEquivalenteBrl": "decimal",
  "exigeNdf": "bool",
  "garantiaExigida": "string",
  "valorGarantiaExigidaBrl": "decimal",
  "status": "string"
}
```

### EconomiaNegociacaoDto

```json
{
  "id": "guid",
  "cotacaoId": "guid",
  "contratoId": "guid",
  "cetPropostaAaPercentual": "decimal",
  "cetContratoAaPercentual": "decimal",
  "economiaBrl": "decimal",
  "economiaAjustadaCdiBrl": "decimal",
  "dataReferenciaCdi": "YYYY-MM-DD",
  "createdAt": "DateTimeOffset"
}
```

### EconomiaPeriodoDto

```json
{
  "porMes": [
    {
      "ano": "int",
      "mes": "int",
      "quantidadeOperacoes": "int",
      "economiaBrutaBrl": "decimal",
      "economiaAjustadaCdiBrl": "decimal"
    }
  ],
  "porBanco": [
    {
      "bancoId": "guid",
      "quantidadeOperacoes": "int",
      "economiaBrutaBrl": "decimal",
      "economiaAjustadaCdiBrl": "decimal"
    }
  ],
  "totalEconomiaBrutaBrl": "decimal",
  "totalEconomiaAjustadaCdiBrl": "decimal",
  "totalOperacoes": "int"
}
```

### LimiteBancoDto

```json
{
  "id": "guid",
  "bancoId": "guid",
  "modalidade": "Finimp | Lei4131 | Refinimp | Nce | BalcaoCaixa | Fgi",
  "valorLimiteBrl": "decimal",
  "valorUtilizadoBrl": "decimal",
  "valorDisponivelBrl": "decimal",
  "dataVigenciaInicio": "YYYY-MM-DD",
  "dataVigenciaFim": "YYYY-MM-DD | null",
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

### CdiSnapshotDto

```json
{
  "id": "guid",
  "data": "YYYY-MM-DD",
  "cdiAaPercentual": "decimal",
  "createdAt": "DateTimeOffset"
}
```
