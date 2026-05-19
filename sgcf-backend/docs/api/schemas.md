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

Usado em garantias de contratos (`GarantiaDto`) e em garantias exigidas por limites de banco (`GarantiaExigidaLimiteDto`).

| Valor (string) | Int | Descrição |
|----------------|-----|-----------|
| `CdbCativo` | 1 | CDB cativo no banco credor |
| `Sblc` | 2 | Stand-by Letter of Credit |
| `Aval` | 3 | Aval de sócio/empresa |
| `AlienacaoFiduciaria` | 4 | Alienação fiduciária de bem |
| `Duplicatas` | 5 | Caução de duplicatas |
| `RecebiveisCartao` | 6 | Cessão de recebíveis de cartão |
| `BoletoBancario` | 7 | Caução de boletos bancários |
| `Fgi` | 8 | Cobertura pelo Fundo de Garantia para Investimentos |

> A API serializa e aceita o nome textual (ex.: `"CdbCativo"`). Valores case-insensitive na entrada.

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
  "updatedAt": "DateTimeOffset",
  "garantiasExigidas": [GarantiaExigidaLimiteDto],
  "historico": [LimiteBancoHistoricoDto]
}
```

> `garantiasExigidas` e `historico` estão presentes em todas as respostas de `LimiteBancoDto`. O `historico` é ordenado por `registradoEm` crescente.

---

### GarantiaExigidaLimiteDto

Projeção de uma garantia exigida pelo banco para liberar uma linha de crédito.

```json
{
  "id": "guid",
  "tipo": "CdbCativo | Sblc | Aval | AlienacaoFiduciaria | Duplicatas | RecebiveisCartao | BoletoBancario | Fgi",
  "percentualSobreLimite": "decimal | null",
  "valorFixoBrl": "decimal | null",
  "obrigatoria": "bool",
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `id` | guid | Identificador da garantia |
| `tipo` | string | Nome do enum `TipoGarantia` |
| `percentualSobreLimite` | decimal? | Percentual sobre o valor do limite; (0, 100]; exclusivo com `valorFixoBrl` |
| `valorFixoBrl` | decimal? | Valor fixo em BRL; exclusivo com `percentualSobreLimite`; null para `Aval` |
| `obrigatoria` | bool | `true` = banco exige; `false` = banco negocia |
| `observacoes` | string? | Texto livre |
| `createdAt` | DateTimeOffset | Instante de criação (UTC) |
| `updatedAt` | DateTimeOffset | Instante da última atualização (UTC) |

---

### LimiteBancoHistoricoDto

Entrada de histórico do valor concedido pelo banco. Gerada automaticamente pelo sistema a cada criação ou alteração do `valorLimiteBrl`.

```json
{
  "id": "guid",
  "limiteBancoId": "guid",
  "valorAnteriorBrl": "decimal | null",
  "valorNovoBrl": "decimal",
  "registradoEm": "DateTimeOffset",
  "observacoes": "string | null"
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `id` | guid | Identificador da entrada de histórico |
| `limiteBancoId` | guid | Limite ao qual esta entrada pertence |
| `valorAnteriorBrl` | decimal? | Valor anterior em BRL; `null` na entrada de criação do limite |
| `valorNovoBrl` | decimal | Novo valor do limite em BRL |
| `registradoEm` | DateTimeOffset | Instante em que a alteração ocorreu (UTC) |
| `observacoes` | string? | Texto livre; preenchido automaticamente com `"Criação do limite"` na entrada inicial |

---

### CriarGarantiaExigidaLimiteRequest

Estrutura de entrada para declarar uma garantia ao criar (`POST`) ou atualizar (`PATCH`) um limite. Não carrega identidade — o sistema atribui o `id` ao persistir.

```json
{
  "tipo": "CdbCativo",
  "percentualSobreLimite": 20.0,
  "valorFixoBrl": null,
  "obrigatoria": true,
  "observacoes": "string | null"
}
```

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `tipo` | string | — | Nome do enum `TipoGarantia` (case-insensitive). Obrigatório. |
| `percentualSobreLimite` | decimal? | `null` | Percentual sobre o limite; (0, 100]; exclusivo com `valorFixoBrl` |
| `valorFixoBrl` | decimal? | `null` | Valor fixo em BRL; > 0; exclusivo com `percentualSobreLimite` |
| `obrigatoria` | bool | `true` | `true` = banco exige; `false` = banco negocia |
| `observacoes` | string? | `null` | Texto livre |

### CdiSnapshotDto

```json
{
  "id": "guid",
  "data": "YYYY-MM-DD",
  "cdiAaPercentual": "decimal",
  "createdAt": "DateTimeOffset"
}
```

---

## DTOs — Painel — Quadro da Dívida

### QuadroDividaDto

Resultado completo do Quadro da Dívida para um ano civil. Retornado por `GET /api/v1/painel/quadro-divida` e `GET /api/v1/simulacoes/cenarios/{id}/quadro-divida`.

```json
{
  "ano": "int",
  "dataReferencia": "YYYY-MM-DD",
  "snapshotInicial": "SaldoPorBancoAtualDto",
  "projecao": "QuadroDividaProjecaoDto",
  "sumario": "QuadroDividaSumarioDto",
  "alertas": "string[]",
  "cenarioAplicado": "CenarioAplicadoDto | null"
}
```

---

### SaldoPorBancoAtualDto

Saldo atual da carteira agrupado por banco, convertido para BRL.

```json
{
  "bancos": [
    {
      "bancoId": "guid",
      "bancoApelido": "string",
      "bancoCodigoCompe": "string",
      "saldoBrl": "decimal",
      "quantidadeContratosAtivos": "int"
    }
  ],
  "saldoTotalBrl": "decimal",
  "dataReferencia": "YYYY-MM-DD"
}
```

---

### QuadroDividaProjecaoDto

Container dos 12 meses projetados.

```json
{
  "meses": "MesProjecaoDto[12]"
}
```

---

### MesProjecaoDto

Projeção de um único mês calendário com breakdown por banco.

```json
{
  "ano": "int",
  "mes": "int (1–12)",
  "bancos": "SaldoBancoMesDto[]",
  "saldoTotalInicio": "decimal",
  "saldoTotalFim": "decimal",
  "totalAmortizacaoMes": "decimal",
  "totalCaptacaoMes": "decimal"
}
```

| Campo | Descrição |
|-------|-----------|
| `meses` | Exatamente 12 entradas; índice 0 = janeiro, índice 11 = dezembro |
| `bancos` | Inclui apenas bancos com saldo ou eventos no mês |
| `saldoTotalInicio` | Soma de `saldoInicio` de todos os bancos do mês |
| `saldoTotalFim` | Soma de `saldoFim` de todos os bancos do mês |
| `totalAmortizacaoMes` | Total de amortizações de principal no mês em BRL |
| `totalCaptacaoMes` | Total de captações no mês em BRL |

---

### SaldoBancoMesDto

Posição de um banco específico dentro de um mês projetado.

```json
{
  "bancoId": "guid",
  "bancoApelido": "string",
  "saldoInicio": "decimal",
  "saldoFim": "decimal",
  "totalAmortizacaoNoMes": "decimal",
  "totalCaptacaoNoMes": "decimal",
  "sharePercentual": "decimal"
}
```

| Campo | Descrição |
|-------|-----------|
| `sharePercentual` | Percentual do banco no `saldoTotalFim` do mês. Soma de todos os bancos = 100 (tolerância 0,01 pp) |

---

### QuadroDividaSumarioDto

Totais anuais agregados da projeção.

```json
{
  "saldoTotalInicioAno": "decimal",
  "saldoTotalFimAno": "decimal",
  "totalAmortizacaoNoAno": "decimal",
  "totalCaptacaoNoAno": "decimal",
  "variacaoAnualPercentual": "decimal"
}
```

| Campo | Descrição |
|-------|-----------|
| `saldoTotalInicioAno` | Saldo total no início do ano (= `saldoTotalInicio` do mês 1) |
| `saldoTotalFimAno` | Saldo total no fim do ano (= `saldoTotalFim` do mês 12) |
| `variacaoAnualPercentual` | `(SaldoFimAno − SaldoInicioAno) / SaldoInicioAno × 100`. Zero quando `SaldoInicioAno = 0` |

---

### CenarioAplicadoDto

Metadados do cenário de simulação aplicado na projeção do Quadro da Dívida. Presente somente quando `cenarioId` foi informado na query (AD-9).

```json
{
  "id": "guid",
  "nome": "string",
  "status": "Rascunho | Ativo | Arquivado",
  "anoBase": "int",
  "quantidadeSimulacoes": "int"
}
```

---

## DTOs — Simulações

### CenarioSimulacaoDto

DTO completo do cenário incluindo simulações filhas. Ver [Simulações API](./simulacoes.md#cenariossimulacaodto).

```json
{
  "id": "guid",
  "nome": "string",
  "descricao": "string | null",
  "anoBase": "int",
  "status": "Rascunho | Ativo | Arquivado",
  "criadoPor": "string",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset",
  "simulacoes": "SimulacaoContratacaoDto[]"
}
```

---

### CenarioSimulacaoResumoDto

DTO resumido para listagens (sem simulações filhas).

```json
{
  "id": "guid",
  "nome": "string",
  "status": "Rascunho | Ativo | Arquivado",
  "anoBase": "int",
  "qtdeSimulacoes": "int",
  "criadoPor": "string",
  "updatedAt": "DateTimeOffset"
}
```

---

### SimulacaoContratacaoDto

DTO de uma captação hipotética dentro de um cenário. O campo `version` é usado como componente da chave de cache Redis (AD-3).

```json
{
  "id": "guid",
  "cenarioId": "guid",
  "bancoId": "guid",
  "modalidade": "string",
  "moeda": "string",
  "valorPrincipal": "decimal",
  "dataContratacaoPrevista": "YYYY-MM-DD",
  "dataPrimeiroVencimento": "YYYY-MM-DD",
  "tipoTaxa": "Fixa | CdiSpread",
  "taxaAa": "decimal | null",
  "spreadAa": "decimal | null",
  "baseCalculo": "string",
  "estruturaAmortizacao": "string",
  "periodicidade": "string",
  "quantidadeParcelas": "int",
  "anchorDiaMes": "string",
  "anchorDiaFixo": "int | null",
  "garantiaExigidaPrevista": "string | null",
  "observacoes": "string | null",
  "version": "int",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### CronogramaHipoteticoDto

Resultado do preview de cronograma hipotético (endpoint stateless).

```json
{
  "taxaEfetivaAaPercentual": "decimal",
  "quantidadeEventos": "int",
  "principalTotal": "decimal",
  "jurosTotal": "decimal",
  "eventos": "EventoCronogramaItemDto[]"
}
```

---

### EventoCronogramaItemDto

Item individual de evento no cronograma hipotético. Distinto de `EventoCronogramaDto` (que representa eventos de contratos reais).

```json
{
  "numero": "int",
  "tipo": "string",
  "data": "YYYY-MM-DD",
  "valor": "decimal",
  "saldoDevedorApos": "decimal | null"
}
```

---

## DTOs — Sistema

### ParametrosSistemaDto

Parâmetros globais de configuração do sistema.

```json
{
  "tetaoMensalCapacidadeBrl": "decimal | null"
}
```

| Campo | Descrição |
|-------|-----------|
| `tetaoMensalCapacidadeBrl` | Limite de movimentação mensal em BRL (captações + amortizações). `null` = sem limite configurado |

---

## Enums — Simulações

### StatusCenarioSimulacao

| Valor | Descrição |
|-------|-----------|
| `Rascunho` | Cenário em edição; mutável |
| `Ativo` | Cenário aprovado; ainda mutável |
| `Arquivado` | Cenário encerrado; imutável via API |

### TipoTaxa

| Valor | Descrição |
|-------|-----------|
| `Fixa` | Taxa fixa anual. Requer `taxaAa` preenchido |
| `CdiSpread` | CDI mais spread. Requer `spreadAa` e CDI de referência |
