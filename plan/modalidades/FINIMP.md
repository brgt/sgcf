# FINIMP — Financiamento à Importação

**Perguntas do PO consolidadas neste documento**:
- Q1 — Data de vencimento única vs cronograma (FINIMP curto bullet vs FINIMP longo parcelado)
- Q3 — FINIMP até 720 dias com quitações semestrais
- Q8 — Liberação proporcional de garantia (CDB cativo)

> Para detalhes transversais, consultar:
> - `00_DiasUteis_Calendario.md` — convenção de dias úteis
> - `00_Cronograma_Estrutura.md` — periodicidade e Strategy de geração
> - `00_TaxasPosFixadas_Indexadores.md` — taxas fixas e pós-fixadas
> - `00_LiberacaoGarantia.md` — liberação proporcional de CDB cativo

---

## 1. Resumo executivo

FINIMP (Financiamento à Importação) é a modalidade de crédito que viabiliza pagamento de importações em moeda estrangeira (USD, EUR, JPY, CNY) com prazo de pagamento financiado pelo banco brasileiro junto a um banco recebedor no exterior. No SGCF é necessário tratar três variantes:

1. **FINIMP curto (até 360 dias) — bullet**: pagamento único de principal + juros no vencimento.
2. **FINIMP longo (até 720 dias) — bullet com juros periódicos ou amortização semestral**: principal pode ser parcelado em 2 a 4 quotas semestrais (padrão BB).
3. **REFINIMP** (refinanciamento): documento próprio `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:14,237-239`.

A garantia típica é CDB cativo equivalente a 30% do principal, com **liberação proporcional automática** após cada amortização (Pergunta 8). Esta política se aplica a contratos > 360 dias; em FINIMP curto bullet, a liberação acontece em um único evento na quitação.

---

## 2. Cenário de negócio

### 2.1 Características gerais

| Item | Valor / Comportamento |
|------|------------------------|
| Moedas suportadas | USD, EUR, JPY, CNY (catálogo) |
| Prazos comuns | 90, 180, 360, 540, 720 dias |
| Estrutura de amortização | Bullet (curto) ou Bullet com juros semestrais + principal semestral (longo) |
| Taxa | Fixa em moeda (LIBOR descontinuada → SOFR + spread em alguns) ou taxa nominal fixa do banco (BB 5,75% a.a. típico) |
| Tributação | IOF câmbio 0,38%, IRRF sobre juros (gross-up se contrato declara) |
| Documentação | ROF (Registro de Operação Financeira BACEN), Termo de Aceite, Nota Promissória |
| Garantia padrão | CDB cativo 30% do principal (BB), pode chegar a 50% em prazos longos ou tomadores médios |
| Câmbio | Cotação fechada (FX contratado) ou cotação corrente (refletindo no saldo BRL) |

### 2.2 Caso Pergunta 1 — Data única vs cronograma

**FINIMP curto bullet (180 dias)**:
- Contratação 14/05/2026, valor USD 500.000.
- Cronograma: 1 evento PRINCIPAL + 1 evento JUROS na mesma data, 10/11/2026 (D+180 ajustado para dia útil).
- Suportado hoje pelo `EventoCronograma`. A "Data de Vencimento" do wizard atual corresponde à `DataPrimeiroVencimento` com `Periodicidade = Bullet` e `QuantidadeParcelas = 1`.

**FINIMP longo parcelado (720 dias, 2 parcelas semestrais)** — Pergunta 3:
- Contratação 14/05/2026, valor USD 1.000.000.
- `Periodicidade = Semestral`, `QuantidadeParcelas = 4` (juros) + amortização de principal em 2 quotas semestrais distintas.
- Estrutura recomendada: `EstruturaAmortizacao = BulletComJurosPeriodicos`, com cronograma:

| # | Data prevista | Tipo | Valor |
|---|---------------|------|-------|
| 1 | 14/11/2026 (D+180) | JUROS | provisão semestral |
| 2 | 14/05/2027 (D+360) | PRINCIPAL | USD 500.000 |
| 2 | 14/05/2027 (D+360) | JUROS | provisão semestral |
| 3 | 16/11/2027* (D+540) | JUROS | provisão semestral |
| 4 | 14/05/2028 (D+720) | PRINCIPAL | USD 500.000 |
| 4 | 14/05/2028 (D+720) | JUROS | provisão semestral + IRRF gross-up se aplicável |

\* Datas ajustadas pela convenção `Following` quando recaem em dia não útil.

### 2.3 Caso Pergunta 3 — Detalhe operacional BB 720 dias

Análise dos contratos em `CONTRATOS_MODELOS/CONTRATO_FINIMP_BANCO_DO_BRASIL.pdf` e `plan/Analise_FINIMP_BB_vs_Itau.xlsx` mostra que o BB pratica três cronogramas habituais para 720 dias:

| Configuração | Eventos PRINCIPAL | Eventos JUROS |
|--------------|-------------------|---------------|
| 2 quotas semestrais (D+360, D+720) | 2 | 4 semestrais |
| 3 quotas semestrais (D+360, D+540, D+720) | 3 | 4 semestrais |
| 4 quotas semestrais (D+180, D+360, D+540, D+720) | 4 | 4 semestrais |

O sistema deve permitir **parametrizar a quantidade de quotas de principal** independentemente das quotas de juros (consequência de §5.2 D3 de `00_Cronograma_Estrutura.md`).

### 2.4 Caso Pergunta 8 — Liberação proporcional do CDB cativo

**CDB BB típico**:
- Principal contratado: USD 1.000.000 a cotação contratada R$ 5,00 → saldo BRL nocional R$ 5.000.000.
- CDB cativo exigido: 30% × R$ 5.000.000 = **R$ 1.500.000** depositado no banco como garantia.
- Política de liberação: `AutomaticaProporcional` com `BaseCambioGarantia = Contratada` (recomendado pelo BB para evitar volatilidade cambial em garantia).

**Após primeira amortização semestral** (USD 250.000):
- Saldo USD remanescente: 750.000.
- Saldo BRL nocional (cotação contratada): R$ 3.750.000.
- CDB exigido após: 30% × R$ 3.750.000 = R$ 1.125.000.
- Excedente liberável: R$ 1.500.000 − R$ 1.125.000 = **R$ 375.000**.
- Sistema gera `EventoLiberacaoGarantia` com `Origem = PagamentoParcela`.

> Em casos onde o banco usa cotação corrente (raro em BB), o saldo BRL oscila e pode gerar `EventoLiberacaoGarantia` com `Origem = RecalculoCambial`. Modelagem em `00_LiberacaoGarantia.md` §5.6.

---

## 3. Estado atual no sistema

| Aspecto | Estado | Local |
|---------|--------|-------|
| Modalidade `FINIMP` cadastrada | Sim | catálogo `Modalidade` |
| Entidade `FinimpDetail` polimórfica | Esperada (Anexo_B §3) | `Sgcf.Domain/Contratos/Detalhes/FinimpDetail.cs` (a verificar) |
| Campos ROF (`RofNumero`, `RofDataEmissao`) | Sim, em `FinimpDetail` | — |
| Cronograma parcelado bullet com juros | Não suportado pela Strategy atual (`BulletStrategy` gera 1 evento só) | `GerarCronogramaCommand` |
| Garantia CDB cativo | Cadastro estático | `Garantia.cs:1-80` + `CdbCativoDetail` |
| Liberação proporcional | Não implementada | — |
| Câmbio (`Contratada` vs `Corrente`) para garantia | Não tem campo | — |
| Suporte a REFINIMP (contrato-mãe) | Sim, campo `ContratoMaeId` | — |

---

## 4. GAPs específicos da modalidade

Adicionais aos GAPs transversais já listados nos `00_*.md`:

| # | GAP FINIMP | Severidade |
|---|-----------|-----------|
| F1 | Strategy `BulletComJurosPeriodicos` não implementada (necessária para 720 dias semestral) | Alta |
| F2 | Não há campo `QuantidadeQuotasPrincipal` separado de `QuantidadeQuotasJuros` no contrato | Alta |
| F3 | Anexo de Termo de Aceite (PDF) não tem campo no `FinimpDetail` | Média |
| F4 | Validação automática que ROF é obrigatório quando moeda ≠ BRL | Baixa |
| F5 | Cálculo de IRRF gross-up depende da residência fiscal do banco recebedor — não modelado | Média |
| F6 | Reconciliação de cotação fechada (FX contratado vs SISBACEN) | Média |

---

## 5. Proposta consolidada

### 5.1 Campos novos no `FinimpDetail`

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `QuantidadeQuotasPrincipal` | `int` (1..4 típico) | Independente de `QuantidadeParcelas` (total de eventos) |
| `BancoRecebedorExterior` | `string(120)` | Para apuração de IRRF |
| `JurisdicaoBancoRecebedor` | `string(60)` | ISO 3166 + nome |
| `TermoAceiteAnexoId` | `Guid?` | Aponta para arquivo no storage |
| `BaseCambioGarantia` | `enum` | `Contratada` ou `Corrente` |
| `TaxaCambioContratada` | `decimal?` | Quando `BaseCambioGarantia = Contratada` |

### 5.2 Comportamento do wizard para FINIMP

No Step 0 (modalidade) → ao escolher FINIMP, no Step 1 a seção "Estrutura de pagamento" exibe:

- Se prazo ≤ 360 dias: defaults `Bullet`, `1 parcela`. Campos `QuantidadeQuotasPrincipal` ocultos.
- Se prazo > 360 dias: defaults `BulletComJurosPeriodicos`, `Semestral`, mostra `QuantidadeQuotasPrincipal` (1..4) e `QuantidadeQuotasJuros` derivada do prazo.

Step 2 (Detalhes FINIMP) — ver wireframe em `00_Wizard_Fluxo_Cadastro.md` §3.3.

Step 2 também coleta `% exigido` do CDB cativo e `PoliticaLiberacao = AutomaticaProporcional` por default.

### 5.3 Geração de cronograma — Strategy `BulletComJurosPeriodicos`

```pseudo
input: dataContratacao, dataPrimeiroVencimentoJuros, qtdQuotasJuros,
       dataPrimeiroVencimentoPrincipal, qtdQuotasPrincipal, periodicidade

para i de 1 até qtdQuotasJuros:
    dataJ = dataPrimeiroVencimentoJuros + (i-1) * passo(periodicidade)
    dataJ = AjustarPorConvencao(dataJ)
    cria EventoCronograma(tipo=JUROS, dataPrevista=dataJ, ...)

para i de 1 até qtdQuotasPrincipal:
    dataP = dataPrimeiroVencimentoPrincipal + (i-1) * passo(periodicidade)
    dataP = AjustarPorConvencao(dataP)
    cria EventoCronograma(tipo=PRINCIPAL, dataPrevista=dataP, ...)
```

Em FINIMP padrão, `dataPrimeiroVencimentoPrincipal` = data da última quota de juros, e `qtdQuotasPrincipal` = 1 (bullet puro com juros semestrais). Para BB 4 quotas, ambos crescem.

### 5.4 Liberação de garantia

- `Garantia.Tipo = CDB_CATIVO`
- `Garantia.PercentualExigido = 0.30` (configurável)
- `Garantia.PoliticaLiberacao = AutomaticaProporcional`
- `Garantia.JanelaAprovacaoDias = 0` (libera no momento do pagamento) ou > 0 para janela de revisão
- A cada `EventoCronograma.Status = Pago` para `TipoEventoCronograma = PRINCIPAL`, dispara recálculo conforme `00_LiberacaoGarantia.md` §5.5.

### 5.5 IRRF sobre juros

Quando moeda ≠ BRL e banco recebedor está em jurisdição "comum" (não paraíso fiscal), IRRF padrão 15%. Se `irrf_gross_up = true`:

```
juros_devidos_brl   = juros_calculados * cotacao_pagamento
juros_com_gross_up  = juros_devidos_brl / (1 − 0.15)
irrf_pago           = juros_com_gross_up * 0.15
```

Campo já existe em `Contrato.IrrfGrossUp` (Anexo_B:76). `FinimpDetail` precisa armazenar `JurisdicaoBancoRecebedor` para permitir override (paraíso fiscal = 25%).

---

## 6. Impacto em APIs

- `POST /api/contratos` — quando `modalidade = FINIMP`, payload aceita campos da §5.1.
- `GET /api/contratos/{id}/cronograma/preview` — Strategy `BulletComJurosPeriodicos` ativada quando `qtdQuotasPrincipal ≥ 1` e `qtdQuotasJuros > qtdQuotasPrincipal`.
- `POST /api/contratos/{id}/anexos` — Termo de Aceite.

---

## 7. Critérios de aceite

- [ ] FINIMP curto bullet 180 dias: 2 eventos (PRINCIPAL + JUROS) gerados na mesma data
- [ ] FINIMP longo 720 dias, 2 quotas principal: cronograma gerado com 4 JUROS + 2 PRINCIPAL conforme §2.2
- [ ] FINIMP longo 720 dias, 4 quotas principal: cronograma com 4 JUROS + 4 PRINCIPAL
- [ ] Pagamento de uma quota PRINCIPAL dispara `EventoLiberacaoGarantia` proporcional sobre o CDB cativo
- [ ] Reproduz fielmente o exemplo numérico de §2.4 (CDB R$ 1,5MM → libera R$ 375k após primeira amortização)
- [ ] Cobertura: contrato BB com 4 quotas, contrato Itaú com 2 quotas, contrato Sicredi bullet 180 dias
- [ ] Termo de Aceite anexado e listado em `GET /api/contratos/{id}/anexos`
- [ ] Validação rejeita criação se `Moeda ≠ BRL` e `ROF` ausente
- [ ] IRRF gross-up calculado corretamente em jurisdição padrão (15%) e paraíso fiscal (25%)

---

## 8. Pontos em aberto

| # | Decisão | Recomendação |
|---|---------|--------------|
| D1 | Cotação fechada (FX contratado) — onde armazenar quando o banco fixa em dia ≠ contratação? | Campo `TaxaCambioContratada` + `DataFixacaoCambio` (default = `DataContratacao`) |
| D2 | Tratar antecipação parcial de FINIMP longo? | Sim, conforme `plan/Anexo_C_Regras_Antecipacao_Pagamento.md` — recalcula cronograma de juros futuros |
| D3 | Suporte a juros mensais em vez de semestrais (alguns bancos médios) | Sim, é apenas trocar `Periodicidade` da Strategy; sem custo adicional |
| D4 | Validar valor mínimo de contrato (USD 50k típico de mercado)? | Não — deixar configurável por banco |

---

## 9. Referências

- `CONTRATOS_MODELOS/CONTRATO_FINIMP_BANCO_DO_BRASIL.pdf`
- `CONTRATOS_MODELOS/CONTRATO_FINIMP_ITAU_TERMO-Original.pdf` e `CONTRATO_ITAU-_TERMO-Manifesto.pdf`
- `CONTRATOS_MODELOS/CONTRATO_FINIMP_SICREDI.pdf`
- `CONTRATOS_MODELOS/CONTRATO_REFINIMP_ITAU.pdf`
- `CONTRATOS_MODELOS/FINIMP ITAU - C416 - AGE1540542 - USD 232.976/`
- `plan/Analise_FINIMP_BB_vs_Itau.xlsx`
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:10-20, 237-239`
- `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`
- `00_Cronograma_Estrutura.md`, `00_LiberacaoGarantia.md`, `00_TaxasPosFixadas_Indexadores.md`, `00_DiasUteis_Calendario.md`
- BACEN Circular 3.690/2013 (ROF)
- Lei 12.249/2010 (gross-up IRRF para remessas ao exterior)
