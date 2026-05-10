# Anexo B — Modalidades de Financiamento, Modelo de Dados e Configuração por Banco

**Documento complementar ao Business Case do SGCF**
**Foco:** Estrutura de dados que acomode todas as modalidades operadas pela Proxys, suporte a REFINIMP e configurações por banco.
**Base de análise:** 11 contratos reais fornecidos pelo usuário (FINIMP BB, FINIMP Itaú, FINIMP Sicredi, 4131 BB, FGI BV, Balcão Caixa, NDF Itaú, REFINIMP Itaú, Termos Caixa).

---

## 1. Modalidades suportadas no MVP

| Modalidade | Natureza | Moeda típica | Bancos que oferecem | Particularidades |
|---|---|---|---|---|
| **FINIMP** | Financiamento à importação | USD, EUR, JPY, CNY | BB, Itaú, Sicredi, Santander, Bradesco | Garantia em CDB cativo (% varia por banco), exige ROF, NDF tipicamente atrelado |
| **REFINIMP** | Refinanciamento de FINIMP existente | Mesma do FINIMP original | BB (parcial), Itaú (total/parcial) | Vinculado a contrato-mãe; Santander e Bradesco **não aceitam** |
| **4131** | Empréstimo direto em moeda estrangeira (Lei 4.131) | USD principalmente | BB, Itaú, Santander, Safra | Prazos longos (até 720+ dias); standby LC; market flex |
| **NCE / CCE** | Crédito em BRL com lastro de exportação/comércio | BRL | Vários | Sem IRRF, sem IOF câmbio |
| **Balcão (Caixa)** | Financiamento balcão com lastro de duplicatas | BRL | Caixa | Múltiplas cessões fiduciárias (CDB + duplicatas escalonadas) |
| **FGI** | Crédito com aval do Fundo Garantidor de Investimentos (BNDES) | BRL | BV e outros credenciados | Cobertura parcial pelo FGI; taxa FGI específica |
| **NDF** | Derivativo cambial atrelado a contrato | USD, EUR, JPY, CNY | Itaú, BB, Santander, etc. | Forward simples ou Collar (range forward); sempre dedicado |

---

## 2. Estrutura do modelo de dados — abordagem polimórfica

A análise dos contratos confirma que **um único modelo "achatado" não funciona**: cada modalidade tem campos exclusivos (ROF para FINIMP, FGI ID para FGI, strike para NDF, contrato-mãe para REFINIMP). A solução é **polimorfismo**: tabela `CONTRATO` com campos comuns + tabelas de extensão por modalidade.

```
CONTRATO  (tabela master — campos comuns a todas as modalidades)
  ├── 1:1 ──→ FINIMP_DETAIL
  ├── 1:1 ──→ REFINIMP_DETAIL
  ├── 1:1 ──→ LEI_4131_DETAIL
  ├── 1:1 ──→ NCE_DETAIL
  ├── 1:1 ──→ BALCAO_CAIXA_DETAIL
  ├── 1:1 ──→ FGI_DETAIL
  ├── 1:N ──→ INSTRUMENTO_HEDGE   (NDFs vinculados)
  ├── 1:N ──→ GARANTIA            (CDB cativo, duplicatas, aval, etc.)
  ├── 1:N ──→ CRONOGRAMA_PAGAMENTO
  └── 1:N ──→ MOVIMENTO_HISTORICO
```

### 2.1 Tabela master — CONTRATO

Campos **comuns a todas as modalidades**:

```
CONTRATO
├─ id (PK)
├─ codigo_interno                          (FIN-2026-0042)
├─ banco_id (FK → BANCO)
├─ modalidade (ENUM: FINIMP, REFINIMP, LEI_4131, NCE, BALCAO_CAIXA, FGI, NDF)
├─ numero_contrato_banco                   (ex: CPGI 327382.69011)
├─ contrato_mae_id (FK → CONTRATO, nullable) — preenchido em REFINIMP
├─
├─ data_assinatura
├─ data_desembolso
├─ data_vencimento
├─ prazo_total_dias
├─
├─ moeda_original (ISO 4217: USD, EUR, JPY, CNY, BRL)
├─ valor_principal_moeda_original
├─ taxa_cambio_inicial                     (cotação no desembolso)
├─ tipo_cotacao_inicial                    (PTAX_D_MENOS_1, PTAX_D0, INTRADAY)
├─ valor_principal_brl_inicial
├─
├─ taxa_juros_aa
├─ tipo_taxa (FIXA, FLUTUANTE)
├─ indice_referencia (CDI, SELIC, SOFR, NULL)   — só se flutuante
├─ spread_aa                                     — só se flutuante
├─ base_calculo_dias (360, 365, 252)
├─
├─ estrutura_amortizacao (BULLET, PARCELAS_IGUAIS, CUSTOMIZADA)
├─ periodicidade_juros (NO_VENCIMENTO, MENSAL, SEMESTRAL, CUSTOMIZADA)
├─
├─ jurisdicao_entidade_financiadora        (Tóquio, Nassau, Lux, Brasil...)
├─ aliquota_irrf                            (0%, 15%, 25%)
├─ irrf_gross_up (BOOLEAN)
├─ aliquota_iof_cambio                     (0%, 0,38%, ou específico)
├─
├─ status (ATIVO, QUITADO, REFINANCIADO, EM_ATRASO, CANCELADO)
├─
├─ observacoes (TEXT)
├─ pdf_contrato_url                        (link para arquivo no storage)
├─
├─ created_at, updated_at
└─ created_by, updated_by                   — audit trail
```

### 2.2 Tabelas de extensão por modalidade

#### FINIMP_DETAIL
```
├─ contrato_id (FK)
├─ rof_numero                               (Registro de Operação Financeira)
├─ rof_data_emissao
├─ exportador_nome
├─ exportador_pais
├─ produto_importado (TEXT)
├─ fatura_referencia
├─ incoterm (EXW, FOB, CIF...)
├─ break_funding_fee_percentual
└─ tem_market_flex (BOOLEAN)
```

#### REFINIMP_DETAIL
```
├─ contrato_id (FK)
├─ percentual_refinanciado                 (100% ou parcial — ex: 70% no BB)
├─ valor_quitado_no_refi                   (parte que foi paga, não rolada)
├─ motivo_refinanciamento (TEXT)
├─ taxa_renegociada_aa                     (pode ser diferente da original)
└─ data_efetivacao
```

#### LEI_4131_DETAIL
```
├─ contrato_id (FK)
├─ banco_emissor_sblc                       (banco correspondente no exterior)
├─ valor_sblc_moeda_original
├─ validade_sblc_dias                      (típico 720+ dias)
├─ comissao_sblc_aa
├─ tem_market_flex (BOOLEAN)
└─ break_funding_fee_percentual
```

#### NCE_DETAIL
```
├─ contrato_id (FK)
├─ tipo_lastro (EXPORTACAO, IMPORTACAO_INDIRETA, OUTROS)
├─ documento_lastro
└─ destinacao_recursos (TEXT)
```

#### BALCAO_CAIXA_DETAIL
```
├─ contrato_id (FK)
├─ percentual_desconto_duplicatas          (ex: até 98%)
├─ tem_cessao_duplicatas (BOOLEAN)
├─ instrumento_cessao_data
└─ vencimento_escalonado (BOOLEAN)
```

#### FGI_DETAIL
```
├─ contrato_id (FK)
├─ tipo_fgi (FGI_PEAC, FGI_NOVO_EMPREENDEDOR, FGI_CRESCIMENTO, ...)
├─ percentual_cobertura_fgi                (70-80% típico)
├─ taxa_fgi_aa                             (0,5% a.a. típico)
├─ banco_intermediario                     (BV, etc. — opera em nome do BNDES)
└─ codigo_operacao_bndes
```

#### INSTRUMENTO_HEDGE (já modelado no Anexo A)
```
├─ contrato_id (FK)
├─ tipo (FORWARD_SIMPLES, COLLAR, TARGET_FORWARD, ...)
├─ moeda
├─ notional
├─ data_inicio, data_vencimento
├─ strike_unico (nullable, só forward)
├─ strike_put, strike_call (nullable, só collar)
├─ tipo_fixing (PTAX_D0, PTAX_D_MENOS_1, MEDIA_PERIODO)
├─ contraparte_banco
├─ status
└─ ...
```

### 2.3 Tabelas transversais

#### GARANTIA
```
├─ contrato_id (FK)
├─ tipo (CDB_CATIVO, SBLC, AVAL, ALIENACAO_FIDUCIARIA, FGI, DUPLICATAS, RECEBIVEIS, OUTROS)
├─ valor_brl
├─ percentual_principal                     (ex: 30% para CDB cativo)
├─ banco_custodia                           (onde está bloqueado)
├─ data_constituicao
├─ data_liberacao_prevista
├─ rendimento_aa (nullable)                 — só CDB cativo
└─ percentual_cdi (nullable)                — só CDB cativo (ex: 100%)
```

#### CRONOGRAMA_PAGAMENTO
```
├─ contrato_id (FK)
├─ numero_parcela
├─ data_prevista
├─ tipo (PRINCIPAL, JUROS, COMISSAO, IOF, IRRF, TARIFA)
├─ valor_moeda_original
├─ valor_brl_estimado                       (atualizado dinamicamente)
├─ status (PREVISTO, PAGO, ATRASADO, REFINANCIADO)
├─ data_pagamento_efetivo (nullable)
└─ valor_pagamento_efetivo_brl (nullable)
```

---

## 3. Configuração por banco (tabela `BANCO_CONFIG`)

Esta é uma das **descobertas mais importantes** da análise: cada banco tem regras próprias e precisamos parametrizar isso. Hard-coded por banco no MVP, mas em formato configurável.

### 3.1 Schema de configuração

```
BANCO
├─ id, nome, cnpj
├─ jurisdicao_padrao_finimp                 (Tóquio, Nassau, etc.)
├─ aliquota_irrf_padrao
├─
├─ aceita_finimp (BOOLEAN)
├─ prazo_max_finimp_dias
├─ aceita_amortizacao_intermediaria (BOOLEAN) — BB aceita semestral, outros só bullet
├─
├─ aceita_refinimp (BOOLEAN)
├─ percentual_max_refinimp                  (1.00 = 100%, 0.70 = só 70%)
├─ aceita_dispensa_ndf (BOOLEAN)
├─ dispensa_ndf_exige_aprovacao (BOOLEAN)   — true para BB e Itaú em USD
├─
├─ percentual_cdb_cativo_padrao             (0.20 = 20% Sicredi, 0.30 = BB/Itaú/Bradesco)
├─ exige_outras_garantias_alem_cdb (BOOLEAN) — true para Daycoval (CDB+boleto)
├─ outras_garantias_descricao (TEXT)
├─
├─ cobra_cpg (BOOLEAN)
├─ cpg_aa_padrao
├─ cobra_comissao_sblc (BOOLEAN)
├─ comissao_sblc_aa_padrao
├─ tarifa_rof_padrao_brl
├─ tarifa_cademp_padrao_brl
├─
├─ contratos_alteraveis (BOOLEAN)           — para registro: a maioria não aceita alterações
└─ observacoes_negociais (TEXT)
```

### 3.2 Configuração inicial (baseada nas informações fornecidas)

| Banco | Aceita REFINIMP? | % máx REFINIMP | % CDB cativo | Outras garantias | Prazo máx FINIMP | Dispensa NDF (USD)? |
|---|---|---|---|---|---|---|
| **Banco do Brasil** | Sim | **70%** (quita 30%) | 30% | — | 720 dias (com semestrais) | Exige aprovação |
| **Itaú** | Sim | 100% (ou parcial) | 30% | — | Variável | Exige aprovação |
| **Bradesco** | **Não** | — | 30% | — | Variável | A confirmar |
| **Santander** | **Não** | — | A confirmar | — | Variável | A confirmar |
| **Sicredi** | A confirmar | A confirmar | **20%** | — | **Sem limite** | A confirmar |
| **Daycoval** | A confirmar | A confirmar | 30% | **+ 20% boleto bancário** | A confirmar | A confirmar |
| **Caixa** | N/A (Balcão) | N/A | Variável (CDB + duplicatas) | Duplicatas | N/A | N/A |
| **BV** | N/A (FGI) | N/A | N/A | FGI | N/A | N/A |

> **Importante:** os campos "A confirmar" devem virar tarefas para a tesouraria preencher na Fase 0 do projeto. Sem eles, o sistema não consegue validar regras automaticamente.

---

## 4. Tratamento detalhado do REFINIMP

### 4.1 Modelo conceitual: contrato-mãe + contrato-filho

Quando ocorre um REFINIMP, o sistema **NÃO altera o contrato original**. Ele:

1. Marca o contrato original como `status = REFINANCIADO` (ou parcialmente refinanciado)
2. Cria um **novo contrato** com `modalidade = REFINIMP` apontando para o original via `contrato_mae_id`
3. Registra na tabela `REFINIMP_DETAIL` o percentual refinanciado e o valor quitado

```
CONTRATO (FIN-2026-0001) — FINIMP original
   └── status: REFINANCIADO_PARCIAL
   └── valor_principal: USD 200.000

CONTRATO (FIN-2026-0042) — REFINIMP
   └── modalidade: REFINIMP
   └── contrato_mae_id: → FIN-2026-0001
   └── valor_principal: USD 140.000 (70% do original — caso BB)
   └── REFINIMP_DETAIL.percentual_refinanciado: 0.70
   └── REFINIMP_DETAIL.valor_quitado_no_refi: USD 60.000 (30% pago)
```

### 4.2 Validações automáticas no cadastro de REFINIMP

| Validação | Regra |
|---|---|
| Banco aceita REFINIMP? | Verificar `BANCO_CONFIG.aceita_refinimp` — bloquear se for Santander/Bradesco |
| Percentual máximo permitido | `valor_refinanciado / valor_original ≤ BANCO_CONFIG.percentual_max_refinimp` |
| Moeda igual à original | Sistema bloqueia REFINIMP em moeda diferente |
| Prazo dentro do limite | Soma do prazo original + REFINIMP ≤ prazo máximo do banco |
| Garantia ativa | CDB cativo continua bloqueado (não libera no REFINIMP) |

### 4.3 Impacto em indicadores

- **Saldo devedor**: contrato original "zera" no valor refinanciado, novo contrato adiciona o valor refinanciado
- **Para Dívida/EBITDA**: não há aumento de dívida no REFINIMP (apenas rolagem)
- **Para fluxo de caixa**: data de vencimento original "morre", nova data de vencimento entra
- **Para histórico**: cadeia de REFINIMPs preservada via `contrato_mae_id` recursivo (um REFINIMP pode ter outro REFINIMP)

---

## 5. Pontos de atenção identificados nos contratos

### 5.1 Variações tributárias e regulatórias

- **IRRF varia por jurisdição**: 15% (Japão pelo acordo), 25% (Bahamas/Nassau, Lux conforme cotação)
- **IOF câmbio**: padrão 0,38%, mas algumas operações específicas podem ter alíquota reduzida
- **ROF**: obrigatório em FINIMP; algumas instituições exigem registro em até 2 dias úteis (atraso pode invalidar operação)
- **Registro de derivativos na B3**: NDFs precisam ser registrados; sistema deve manter o ID de registro

### 5.2 Particularidades operacionais

- **Contratos pouco alteráveis**: a maioria dos bancos não negocia alteração de cláusulas. Sistema deve refletir isso em workflow — não esperar negociação posterior
- **Erros frequentes em contratos vs. proposta**: validador automático deve comparar campo a campo (ver Anexo A)
- **NDF dedicado 1:1**: já confirmado no Anexo A
- **Estruturas de NDF observadas**: Forward simples e Collar (range forward) — sistema preparado para outras

### 5.3 Casos especiais a tratar

- **FINIMP com cronograma de quitações intermediárias**: BB permite quitações semestrais até 720 dias
- **Múltiplas cessões fiduciárias**: Caixa Balcão usa CDB + Duplicatas escalonadas — a tabela `GARANTIA` já suporta múltiplas garantias por contrato
- **NDF que vence em data diferente do contrato-mãe**: sistema permite, mas alerta sobre mismatch
- **REFINIMP de REFINIMP**: cadeia recursiva permitida (B refinancia A; C refinancia B)

---

## 6. Controle de parcelas, saldo devedor e provisão de juros

Esta é uma das funcionalidades mais críticas do sistema. Operações 4131, FINIMP com BB, REFINIMPs e até alguns Balcão Caixa têm cronogramas customizados — o sistema precisa modelar todas as variações sem hard-code.

### 6.1 Periodicidades suportadas

| Periodicidade | Operação típica | Exemplo |
|---|---|---|
| **Bullet único** | FINIMP padrão (Itaú Nassau, Santander Lux) | 1 pagamento de principal + juros no vencimento |
| **Mensal** | 4131 raro, alguns NCE | 12 amortizações de principal em 12 meses |
| **Trimestral** | 4131 médio prazo | 4 amortizações em 12 meses |
| **Semestral** | **4131 BB padrão**, FINIMP BB longo | 4 amortizações em 720 dias |
| **Anual** | Algumas operações de longo prazo | Raro |
| **Customizada** | Balcão Caixa, FGI complexos | Datas e valores manuais |
| **Juros e principal em datas distintas** | Algumas estruturas específicas | Juros mensais + principal bullet |

**Decisão de design:** o cronograma é sempre **uma lista de parcelas explícitas** — não há "regra de geração" hard-coded. O sistema gera o cronograma inicial automaticamente conforme parâmetros, mas qualquer parcela pode ser editada manualmente para refletir cláusulas específicas do contrato.

### 6.2 Tipos de parcela (linha do cronograma)

Cada item do cronograma tem um `tipo` que define o que está sendo pago:

| Tipo | Descrição | Afeta saldo principal? | Tributo associado |
|---|---|---|---|
| `PRINCIPAL` | Amortização do valor principal | **Sim** (reduz) | — |
| `JUROS` | Pagamento de juros sobre saldo devedor | Não | IRRF gross-up |
| `IRRF_RETIDO` | IRRF efetivo recolhido (visibilidade fiscal) | Não | — |
| `IOF_CAMBIO` | IOF câmbio sobre conversão | Não | — |
| `COMISSAO_SBLC` | Comissão de standby letter of credit | Não | — |
| `COMISSAO_CPG` | Comissão de permanência de garantia | Não | — |
| `COMISSAO_GARANTIA_FGI` | Comissão FGI | Não | — |
| `TARIFA_ROF` | Tarifa de registro ROF | Não | — |
| `TARIFA_CADEMP` | Tarifa CADEMP | Não | — |
| `TARIFA_CARTORIO` | Cartório / abertura | Não | — |
| `BREAK_FUNDING_FEE` | Multa por pré-pagamento | Não | — |
| `MULTA_MORATORIA` | Multa de atraso | Não | — |

**Vantagens dessa modelagem:**
- Cronograma único cobre **todas as obrigações** do contrato (não só principal+juros)
- Cada linha tem `status` próprio (PAGO/PREVISTO/ATRASADO) — um contrato pode ter pagamento parcial
- Indicadores de adimplência calculados automaticamente
- Histórico de pagamentos auditável

### 6.3 Schema detalhado de `CRONOGRAMA_PAGAMENTO`

```
CRONOGRAMA_PAGAMENTO
├─ id (PK)
├─ contrato_id (FK)
├─ numero_evento                            (1, 2, 3... — agrupa parcelas da mesma data)
├─ tipo (PRINCIPAL, JUROS, IOF_CAMBIO, COMISSAO_*, TARIFA_*, ...)
├─ data_prevista
├─ valor_moeda_original
├─ valor_brl_estimado                       (atualizado dinamicamente pela cotação)
├─ saldo_devedor_apos                       (saldo principal restante após este pagamento)
├─
├─ status (PREVISTO, PAGO, ATRASADO, REFINANCIADO, CANCELADO)
├─ data_pagamento_efetivo (nullable)
├─ valor_pagamento_efetivo_moeda_original (nullable)
├─ valor_pagamento_efetivo_brl (nullable)
├─ taxa_cambio_pagamento (nullable)
├─
├─ observacoes (TEXT)
└─ comprovante_url (nullable)
```

### 6.4 Cálculo de saldo devedor a qualquer data

A pergunta "qual o saldo agora?" é decomposta em **três componentes** calculados separadamente:

#### A) Saldo de principal (sem juros)

```
saldo_principal_aberto =
  valor_principal_inicial
  - SUM(parcelas WHERE tipo = PRINCIPAL AND status = PAGO)
```

Esse é o **valor "limpo"** que ainda falta amortizar — em moeda original.

#### B) Juros provisionados (acumulados desde o último pagamento de juros)

Para cada período de juros aberto, calcula-se a provisão pro rata:

```
juros_provisionados_periodo =
  saldo_principal_no_periodo
  × taxa_aa
  × dias_decorridos_desde_ultimo_pagamento
  / base_calculo_dias
```

Soma de todos os períodos abertos = juros provisionados totais.

#### C) Comissões e tarifas a pagar

```
comissoes_a_pagar =
  SUM(parcelas WHERE tipo IN (COMISSAO_*, TARIFA_*, IOF_CAMBIO)
              AND status IN (PREVISTO, ATRASADO))
```

#### Saldo devedor total

```
saldo_total = saldo_principal_aberto
            + juros_provisionados
            + comissoes_a_pagar
```

**Em moeda original (USD/EUR/etc.) e em BRL** (aplicando a cotação configurada no momento — ver Anexo A seção 8.3).

### 6.5 Indicadores operacionais por contrato

A tabela `CRONOGRAMA_PAGAMENTO` permite calcular indicadores de forma direta:

| Indicador | Fórmula |
|---|---|
| **Total de parcelas (eventos)** | `COUNT(DISTINCT numero_evento)` |
| **Parcelas pagas** | `COUNT(DISTINCT numero_evento WHERE todas_linhas.status = PAGO)` |
| **Parcelas em aberto** | `COUNT(DISTINCT numero_evento WHERE alguma_linha.status IN (PREVISTO, ATRASADO))` |
| **Parcelas em atraso** | `COUNT(DISTINCT numero_evento WHERE alguma_linha.status = ATRASADO)` |
| **% adimplência** | `parcelas_pagas / total_parcelas` |
| **Próxima parcela** | `MIN(data_prevista WHERE status IN (PREVISTO, ATRASADO))` |
| **Valor da próxima parcela** | `SUM(valor_brl_estimado para parcelas da mesma data da próxima)` |
| **Dias até próxima parcela** | `(próxima_data - hoje) em dias` |
| **% principal já amortizado** | `principal_pago / principal_total` |
| **% prazo decorrido** | `dias_desde_desembolso / prazo_total_dias` |

### 6.6 Exemplo prático: 4131 BB com semestrais

**Premissas do contrato:**
- Banco: Banco do Brasil
- Modalidade: 4131
- Desembolso: 01/01/2026, USD 1.000.000 (cotação R$ 5,00 → R$ 5.000.000)
- Taxa: 6,0% a.a., base 360
- Estrutura: 4 amortizações iguais a cada 180 dias (USD 250.000 cada)
- Juros pagos junto com cada amortização

**Cronograma gerado automaticamente (8 linhas, 4 eventos):**

| # | Data | Tipo | Valor (USD) | Saldo após (USD) | Status (em 01/03/2026) |
|--:|---|---|---:|---:|---|
| 1 | 30/06/2026 | JUROS | 30.000,00 | — | PREVISTO |
| 1 | 30/06/2026 | PRINCIPAL | 250.000,00 | 750.000 | PREVISTO |
| 2 | 27/12/2026 | JUROS | 22.500,00 | — | PREVISTO |
| 2 | 27/12/2026 | PRINCIPAL | 250.000,00 | 500.000 | PREVISTO |
| 3 | 25/06/2027 | JUROS | 15.000,00 | — | PREVISTO |
| 3 | 25/06/2027 | PRINCIPAL | 250.000,00 | 250.000 | PREVISTO |
| 4 | 22/12/2027 | JUROS | 7.500,00 | — | PREVISTO |
| 4 | 22/12/2027 | PRINCIPAL | 250.000,00 | 0 | PREVISTO |

**Saldo em 01/03/2026 (60 dias após desembolso, antes da 1ª parcela):**

| Componente | USD | BRL @ R$ 5,30 |
|---|---:|---:|
| Saldo principal aberto | 1.000.000,00 | 5.300.000,00 |
| Juros provisionados (60/360 × 6% × 1MM) | 10.000,00 | 53.000,00 |
| Comissões/tarifas em aberto | 0,00 | 0,00 |
| **Saldo total (com juros)** | **1.010.000,00** | **5.353.000,00** |
| **Saldo total (sem juros)** | **1.000.000,00** | **5.300.000,00** |

Indicadores: 0 parcelas pagas / 4 totais (0% de adimplência), próxima parcela em 30/06/2026 no valor de USD 280.000.

**Saldo em 15/09/2026 (após 1ª parcela, no meio do 2º período):**

A 1ª parcela (USD 30.000 juros + USD 250.000 principal) foi paga em 30/06/2026.

| Componente | USD | BRL @ R$ 5,30 |
|---|---:|---:|
| Saldo principal aberto | 750.000,00 | 3.975.000,00 |
| Juros provisionados (77 dias × 6% × 750k / 360) | 9.625,00 | 51.012,50 |
| **Saldo total (com juros)** | **759.625,00** | **4.026.012,50** |
| **Saldo total (sem juros)** | **750.000,00** | **3.975.000,00** |

Indicadores: 1/4 parcelas pagas (25% de adimplência), próxima parcela em 27/12/2026.

### 6.7 Exemplo prático: FINIMP com pagamentos mensais

**Premissas:**
- Banco: 4131 hipotético com mensais
- USD 1.000.000, taxa 6% a.a., base 360
- 12 amortizações mensais (~30 dias cada) — Price ou SAC?

**Decisão de design:** o sistema deve suportar três sistemas de amortização configuráveis:

| Sistema | Como funciona | Quando usar |
|---|---|---|
| **SAC** (Sistema de Amortização Constante) | Principal igual em todas, juros decrescentes | Maioria dos 4131/FINIMP |
| **Price** (Tabela Price) | Parcela total igual em todas, principal crescente | Raro em corporate |
| **Bullet** | Sem amortização intermediária; tudo no fim | FINIMP padrão |
| **Customizado** | Datas e valores manuais | Balcão Caixa, FGI complexos |

Cada modalidade pode ter sua estrutura padrão configurada, e o usuário pode override caso a caso.

### 6.8 Visões/Relatórios típicos

A interface deve oferecer pelo menos:

| Visão | Conteúdo |
|---|---|
| **Cronograma do contrato** | Todas as linhas do `CRONOGRAMA_PAGAMENTO` agrupadas por data, com status colorido |
| **Posição do contrato hoje** | Saldo principal aberto, juros provisionados, próxima parcela, % adimplência |
| **Calendário consolidado** | Todas as parcelas de todos os contratos em um calendário mensal/anual |
| **Curva de amortização** | Gráfico mostrando saldo devedor ao longo do tempo (passado e projeção) |
| **Histórico de pagamentos** | Lista cronológica de tudo que foi pago, com taxa de câmbio aplicada |
| **Inadimplência** | Parcelas em atraso por contrato, com dias de atraso e valor |

### 6.9 Tratamento de pagamento parcial e antecipado

**Pagamento parcial de uma parcela:**
- Sistema permite registrar pagamento menor que o previsto
- Diferença vira nova parcela com status ATRASADO ou é tratada como rolagem (configurável)

**Pagamento antecipado (pré-pagamento):**
- Sistema permite quitar parcela antes da data prevista
- Aplica `BREAK_FUNDING_FEE` automaticamente (conforme cláusula do contrato)
- Recalcula juros pro rata até a data efetiva do pagamento

**Pagamento de múltiplas parcelas em uma só vez:**
- Sistema permite agrupar pagamento de N parcelas
- Atualiza status de todas as parcelas afetadas
- Exemplo comum: liquidação total antecipada do contrato

### 6.10 Provisão contábil mensal (mesmo no MVP gerencial)

Mesmo no modelo gerencial standalone, o SGCF deve **provisionar mensalmente** os juros acumulados até o último dia do mês. Isso permite:

- Mostrar despesa financeira do mês na DRE gerencial
- Comparar realizado vs orçado
- Calcular custo médio ponderado da dívida
- Gerar dados para Fase 2 (quando integrar com SAP)

Lançamentos gerenciais sugeridos no fim de cada mês (exemplo BB acima):

```
Mês de janeiro/2026 (31 dias após desembolso):
  Débito  3.2.1 Juros sobre 4131            5.166,67 USD = R$ 27.383,33
  Crédito 2.3.2 Juros provisionados 4131    5.166,67 USD = R$ 27.383,33

Mês de fevereiro/2026 (28 dias):
  Débito  3.2.1 Juros sobre 4131            4.666,67 USD = R$ 24.733,33
  Crédito 2.3.2 Juros provisionados 4131    4.666,67 USD = R$ 24.733,33
```

Em 30/06/2026, no pagamento dos juros (USD 30.000):

```
Débito  2.3.2 Juros provisionados 4131    30.000 USD = R$ 159.000,00
Crédito 1.1.1 Conta Corrente em BRL                  R$ 159.000,00 + IRRF
```

Esse fluxo prepara o terreno para a Fase 2 — quando os mesmos lançamentos serão refletidos no SAP B1 com o de-para de plano de contas.

---

## 7. Tabela completa do financiamento (visão consolidada sob demanda)

A qualquer momento, o usuário poderá solicitar uma **visão consolidada de qualquer contrato** — equivalente a uma "ficha completa" exportável em tela, PDF ou Excel.

### 7.1 Estrutura da tabela completa

A visão é organizada em **6 blocos**, todos calculados em tempo real a partir das tabelas do banco:

| Bloco | Conteúdo |
|---|---|
| **A. Identificação** | Banco, modalidade, número, datas, status |
| **B. Valores principais** | Principal original (moeda+BRL), cotação inicial, saldo devedor atual em moeda original e BRL |
| **C. Encargos** | Taxa de juros, base, tipo, IRRF, IOF, comissões aplicáveis |
| **D. Resumo financeiro** | Total pago dividido em **principal / juros / comissões / total**; em aberto dividido em **principal / juros provisionados / comissões / saldo total** |
| **E. Cronograma detalhado** | Todas as parcelas (pagas, em aberto, atrasadas) com status colorido |
| **F. Garantias atreladas** | Cada garantia com tipo, valor, status e detalhes específicos do tipo |
| **G. Hedge (NDFs)** | Posições de hedge atreladas com MTM atual |
| **H. Histórico de pagamentos** | Pagamentos efetivos com data, valor, taxa de câmbio aplicada |

### 7.2 Exemplo visual (4131 BB, USD 1MM, 720 dias com semestrais, sem pagamentos ainda)

```
┌────────────────────────────────────────────────────────────────────┐
│  TABELA COMPLETA DO FINANCIAMENTO                                  │
│  Gerada em 07/05/2026 14:32 por Welysson Soares                    │
└────────────────────────────────────────────────────────────────────┘

A. IDENTIFICAÇÃO
   Código interno      : FIN-2026-0042
   Banco               : Banco do Brasil S.A. - Ag. 0760 Tóquio (Japão)
   Modalidade          : 4131 (Lei 4.131/62)
   Nº contrato banco   : 4131-BB-2026-001
   Data assinatura     : 28/12/2025
   Data desembolso     : 01/01/2026
   Data vencimento     : 22/12/2027
   Status              : ATIVO

B. VALORES PRINCIPAIS
   Moeda               : USD
   Principal original  : USD 1.000.000,00
   Cotação inicial     : R$ 5,0000 (PTAX D-1, 31/12/2025)
   Equivalente BRL     : R$ 5.000.000,00
   Cotação atual       : R$ 5,3000 (PTAX D-1, 06/05/2026)
   Saldo BRL @ atual   : R$ 5.412.183,33

C. ENCARGOS
   Taxa de juros       : 6,000% a.a. fixa
   Base de cálculo     : 360 dias (pro rata diária)
   Periodicidade juros : Semestral (junto com amortização)
   IRRF                : 15% (Tóquio - acordo BR-JP) com gross-up
   IOF câmbio          : 0,38% por conversão
   Outras comissões    : —

D. RESUMO FINANCEIRO                            USD             BRL
   ┌─ TOTAL PAGO ATÉ HOJE ─────────────────────────────────────────┐
   │   Principal pago         :          0,00          R$       0,00 │
   │   Juros pagos            :          0,00          R$       0,00 │
   │   Comissões/tarifas pagas:          0,00          R$       0,00 │
   │   IRRF efetivo recolhido :          0,00          R$       0,00 │
   │   ─────────────────────────────────────────────────────────────│
   │   TOTAL PAGO             :          0,00          R$       0,00 │
   └────────────────────────────────────────────────────────────────┘

   ┌─ EM ABERTO ───────────────────────────────────────────────────┐
   │   Principal a amortizar  :  1.000.000,00          R$ 5.300.000,00│
   │   Juros provisionados (*):     21.166,67          R$   112.183,33│
   │   Comissões a pagar      :          0,00          R$       0,00 │
   │   ─────────────────────────────────────────────────────────────│
   │   SALDO TOTAL DEVEDOR    :  1.021.166,67          R$ 5.412.183,33│
   └────────────────────────────────────────────────────────────────┘
   (*) Calculado pro rata desde último pagamento de juros até hoje

   INDICADORES OPERACIONAIS
   ▸ Parcelas pagas       : 0 de 4 eventos (0,0% adimplência)
   ▸ Próxima parcela      : 30/06/2026 (em 54 dias)
   ▸ Valor próxima        : USD 280.000,00 (R$ 1.484.000,00)
   ▸ Principal amortizado : 0,00%
   ▸ Prazo decorrido      : 17,6% (127 / 720 dias)

E. CRONOGRAMA DETALHADO
   ┌─────┬────────────┬───────────┬─────────────┬──────────────┬──────────┐
   │ Evt │ Data       │ Tipo      │ Valor (USD) │ Saldo após   │ Status   │
   ├─────┼────────────┼───────────┼─────────────┼──────────────┼──────────┤
   │  1  │ 30/06/2026 │ JUROS     │   30.000,00 │            — │ PREVISTO │
   │  1  │ 30/06/2026 │ PRINCIPAL │  250.000,00 │   750.000,00 │ PREVISTO │
   │  2  │ 27/12/2026 │ JUROS     │   22.500,00 │            — │ PREVISTO │
   │  2  │ 27/12/2026 │ PRINCIPAL │  250.000,00 │   500.000,00 │ PREVISTO │
   │  3  │ 25/06/2027 │ JUROS     │   15.000,00 │            — │ PREVISTO │
   │  3  │ 25/06/2027 │ PRINCIPAL │  250.000,00 │   250.000,00 │ PREVISTO │
   │  4  │ 22/12/2027 │ JUROS     │    7.500,00 │            — │ PREVISTO │
   │  4  │ 22/12/2027 │ PRINCIPAL │  250.000,00 │         0,00 │ PREVISTO │
   └─────┴────────────┴───────────┴─────────────┴──────────────┴──────────┘
   Total juros previstos:         USD 75.000,00 (R$ 397.500,00 @ 5,30)
   Total amortizações:           USD 1.000.000,00

F. GARANTIAS ATRELADAS (3)
   ┌──────────────────────────────────────────────────────────────────────┐
   │ #1 CDB CATIVO (Cash Collateral)                          STATUS: ATIVA│
   │    Banco custodiante     : Banco do Brasil                            │
   │    Nº CDB               : CDB-BB-2026-XXXX                            │
   │    Valor bloqueado      : R$ 1.500.000,00 (30% do principal)          │
   │    Rendimento            : 100% CDI                                   │
   │    Vencimento previsto   : 22/12/2027 (junto com contrato)            │
   ├──────────────────────────────────────────────────────────────────────┤
   │ #2 RECEBÍVEIS DE CARTÃO DE CRÉDITO                       STATUS: ATIVA│
   │    Operadora             : Cielo                                      │
   │    Tipo de recebível     : Vendas à vista + parcelado sem juros       │
   │    % faturamento compr.  : 15% do faturamento mensal médio            │
   │    Valor médio mensal    : R$ 250.000,00 (referência)                 │
   │    Validade              : Enquanto contrato ativo                    │
   ├──────────────────────────────────────────────────────────────────────┤
   │ #3 BOLETO BANCÁRIO (garantia complementar)               STATUS: ATIVA│
   │    Banco emissor         : Daycoval                                   │
   │    Quantidade boletos    : 12                                         │
   │    Valor unitário        : R$ 16.667,00                               │
   │    Valor total emitido   : R$ 200.000,00                              │
   │    Vencimento            : Mensal (jan/2026 a dez/2026)               │
   └──────────────────────────────────────────────────────────────────────┘
   COBERTURA TOTAL DE GARANTIAS: R$ 1.950.000,00 (36,8% do saldo BRL)

G. HEDGE (NDFs ATRELADOS)
   Nenhum NDF atrelado a este contrato.

H. HISTÓRICO DE PAGAMENTOS EFETIVOS
   Nenhum pagamento registrado até o momento.

OBSERVAÇÕES
   ▸ Contrato com cláusula Market Flex (banco pode renegociar termos)
   ▸ Break funding fee de 1% se houver pré-pagamento
```

### 7.3 Formatos de exportação

| Formato | Uso típico |
|---|---|
| **Tela (HTML)** | Consulta rápida pela tesouraria |
| **PDF** | Apresentação à diretoria, auditoria, comitê |
| **Excel (.xlsx)** | Análises adicionais, manipulação ad-hoc |
| **JSON** | Integração futura com outros sistemas |

### 7.4 Permissões e auditoria

- Toda exportação fica registrada em log: quem exportou, quando, qual contrato, qual formato
- Permissões granulares: usuários da tesouraria veem todos os contratos; gerentes veem só sua área de negócio (futuro)
- Marca d'água com nome do usuário e data/hora em PDFs gerados

---

## 8. Modelo expandido de garantias

A análise dos contratos revelou diversidade ampla de garantias — algumas combinadas. O modelo precisa suportar **múltiplas garantias por contrato**, cada uma com seus campos próprios.

### 8.1 Tipos de garantia suportados

| Tipo | Banco/situação típica | Campos específicos |
|---|---|---|
| **CDB cativo (Cash Collateral)** | BB, Itaú, Bradesco, Santander, Sicredi, Daycoval | Banco custódia, % CDI, vencimento, número |
| **SBLC (Standby Letter of Credit)** | 4131 BB, FINIMP Itaú, Santander Lux | Banco emissor, país, validade, comissão |
| **Aval / Garantia pessoal** | Vários | Avalista (PF/PJ), valor, validade |
| **Alienação fiduciária** | Imóveis, equipamentos | Bem alienado, valor avaliado, matrícula |
| **Cessão fiduciária de duplicatas** | Caixa Balcão | Lista de duplicatas, % desconto, vencimento escalonado |
| **Cessão fiduciária de recebíveis (cartão)** | Múltiplos bancos | Operadora, % faturamento, tipo recebível |
| **Boleto bancário (garantia adicional)** | Daycoval | Banco emissor, quantidade, valor unitário, vencimentos |
| **FGI (Fundo Garantidor)** | BV e bancos credenciados | Tipo FGI, % cobertura, taxa FGI |
| **Carta de fiança bancária** | Vários | Banco emissor, validade, valor |
| **Outros** | Casos especiais | Texto livre + valor |

### 8.2 Modelo polimórfico de garantias

Mesmo padrão usado para contratos: tabela master + extensões por tipo.

```
GARANTIA (master)
├─ id (PK)
├─ contrato_id (FK)
├─ tipo (ENUM dos tipos acima)
├─ valor_brl
├─ percentual_principal              (ex: 30% — calculado vs principal do contrato)
├─ data_constituicao
├─ data_liberacao_prevista
├─ data_liberacao_efetiva (nullable)
├─ status (ATIVA, LIBERADA, EXECUTADA, CANCELADA)
├─ observacoes
├─ created_at, updated_at, created_by

  ├─ 1:1 ──→ GARANTIA_CDB_CATIVO_DETAIL
  ├─ 1:1 ──→ GARANTIA_SBLC_DETAIL
  ├─ 1:1 ──→ GARANTIA_AVAL_DETAIL
  ├─ 1:1 ──→ GARANTIA_ALIENACAO_FIDUCIARIA_DETAIL
  ├─ 1:1 ──→ GARANTIA_DUPLICATAS_DETAIL
  ├─ 1:1 ──→ GARANTIA_RECEBIVEIS_CARTAO_DETAIL
  ├─ 1:1 ──→ GARANTIA_BOLETO_BANCARIO_DETAIL
  └─ 1:1 ──→ GARANTIA_FGI_DETAIL
```

#### `GARANTIA_CDB_CATIVO_DETAIL`
```
├─ garantia_id (FK)
├─ banco_custodia                    (geralmente o próprio banco do contrato)
├─ numero_cdb
├─ data_emissao_cdb
├─ data_vencimento_cdb
├─ rendimento_aa                      (taxa nominal do CDB)
├─ percentual_cdi                     (ex: 100,0% — confirmou-se que rende CDI integral)
└─ taxa_irrf_aplicacao                (tabela regressiva: 22,5% / 20% / 17,5% / 15%)
```

#### `GARANTIA_RECEBIVEIS_CARTAO_DETAIL` *(novo)*
```
├─ garantia_id (FK)
├─ operadora_cartao                   (Cielo, Stone, Rede, Getnet, PagSeguro, etc.)
├─ tipo_recebivel                     (VISTA, PARCELADO_SEM_JUROS, PARCELADO_COM_JUROS, MIX)
├─ percentual_faturamento_comprometido
├─ valor_medio_mensal_referencia
├─ prazo_recebimento_dias            (D+1, D+30, D+33, etc.)
└─ termo_cessao_url                   (link para PDF do termo)
```

#### `GARANTIA_BOLETO_BANCARIO_DETAIL` *(novo)*
```
├─ garantia_id (FK)
├─ banco_emissor                      (banco que emitiu os boletos como garantia)
├─ quantidade_boletos
├─ valor_unitario
├─ data_emissao_inicial
├─ data_vencimento_inicial            (1º boleto)
├─ data_vencimento_final              (último boleto)
├─ periodicidade                      (MENSAL, TRIMESTRAL, ETC.)
└─ status_boletos                     (lista de status individual: ATIVO/PAGO/CANCELADO)
```

#### `GARANTIA_DUPLICATAS_DETAIL`
```
├─ garantia_id (FK)
├─ percentual_desconto_aplicado
├─ vencimento_escalonado_inicio
├─ vencimento_escalonado_fim
├─ qtd_duplicatas_cedidas
├─ valor_total_duplicatas
└─ instrumento_cessao_data
```

#### `GARANTIA_SBLC_DETAIL`
```
├─ garantia_id (FK)
├─ banco_emissor
├─ pais_emissor
├─ swift_code
├─ validade_dias
├─ comissao_aa
└─ numero_sblc
```

#### `GARANTIA_FGI_DETAIL`
```
├─ garantia_id (FK)
├─ tipo_fgi                           (FGI_PEAC, FGI_NOVO_EMPREENDEDOR, etc.)
├─ percentual_cobertura               (0,70 = 70%)
├─ taxa_fgi_aa
├─ banco_intermediario
└─ codigo_operacao_bndes
```

#### `GARANTIA_AVAL_DETAIL`
```
├─ garantia_id (FK)
├─ avalista_tipo                      (PF, PJ)
├─ avalista_nome
├─ avalista_documento                 (CPF/CNPJ)
├─ valor_aval
└─ vigencia_ate
```

#### `GARANTIA_ALIENACAO_FIDUCIARIA_DETAIL`
```
├─ garantia_id (FK)
├─ tipo_bem                           (IMOVEL, EQUIPAMENTO, VEICULO, OUTRO)
├─ descricao_bem
├─ valor_avaliado
├─ matricula_ou_chassi
└─ cartorio_registro
```

### 8.3 Indicadores de garantia por contrato

| Indicador | Cálculo |
|---|---|
| **Cobertura total** | `SUM(garantia.valor_brl WHERE status = ATIVA) / saldo_devedor_brl` |
| **Cobertura líquida (sem CDB próprio)** | `SUM(garantia.valor_brl exceto CDB_CATIVO) / saldo` |
| **Garantias por tipo** | Distribuição em pizza/barras |
| **CDB cativo total bloqueado** | Soma de todos CDBs cativos atrelados a contratos ativos |
| **% do faturamento comprometido em recebíveis** | Soma de `percentual_faturamento_comprometido` de garantias ATIVAS |

### 8.4 Visões críticas sobre garantias

**Visão 1 — Tela do contrato (ver exemplo seção 7.2):** lista todas as garantias atreladas com status e detalhes específicos.

**Visão 2 — Painel de garantias (todos os contratos):**

```
PAINEL DE GARANTIAS — POSIÇÃO EM 07/05/2026

POR TIPO                          Valor BRL       % do total
─────────────────────────────────────────────────────────────
CDB Cativo                    R$ 12.500.000,00      62,5%
Recebíveis Cartão              R$ 4.200.000,00      21,0%
Boleto Bancário                R$ 1.800.000,00       9,0%
SBLC                           R$ 1.200.000,00       6,0%
Aval                             R$ 300.000,00       1,5%
─────────────────────────────────────────────────────────────
TOTAL GARANTIAS ATIVAS        R$ 20.000.000,00     100,0%

POR BANCO                       Valor BRL    % capacidade
─────────────────────────────────────────────────────────────
Banco do Brasil               R$  6.500.000,00      32,5%
Itaú                          R$  4.700.000,00      23,5%
Santander                     R$  3.200.000,00      16,0%
Sicredi                       R$  2.800.000,00      14,0%
Daycoval                      R$  1.800.000,00       9,0%
Outros                        R$  1.000.000,00       5,0%

ALERTAS (3)
▸ CDB cativo do Itaú vence em 12 dias - operação Itaú-2026-008
▸ % faturamento Cielo já em 18% (alvo: 15%) - revisar
▸ Boleto Daycoval #5/12 vence amanhã
```

**Visão 3 — Calendário de liberação de garantias:**

Mostra em quais datas os CDBs serão liberados (junto com a quitação de cada contrato) — importante para planejamento de fluxo de caixa.

### 8.5 Validações automáticas no cadastro

| Validação | Comportamento |
|---|---|
| Cobertura total < 100% do principal | **Alerta** (não bloqueia, pode ser desejado) |
| % CDB cativo abaixo do mínimo do banco | **Alerta** (ex: BB exige mínimo 30%) |
| Operação em moeda estrangeira sem NDF nem outra cobertura cambial | **Alerta crítico** |
| Garantia vencendo antes do contrato | **Bloqueio** — não permite cadastrar |
| Soma de % de faturamento (recebíveis cartão) > 100% | **Bloqueio** — comprometimento excessivo |

---

## 9. Limitações da análise atual

1. **Pasta Google Drive (Santander 4131)**: o usuário mencionou contratos detalhados do Santander 4131 em pasta no Drive. **Não foi possível acessar diretamente.** Recomenda-se que esses contratos sejam anexados em uma próxima rodada para enriquecer o cadastro do Santander no `BANCO_CONFIG`.
2. **Dados marcados "A confirmar"**: em particular regras de REFINIMP de Sicredi e Daycoval, % CDB Santander, prazo máximo FINIMP por banco — devem virar tarefas da Fase 0.
3. **Validação cruzada com prática real**: o que está nos contratos é o "máximo permitido"; o que é praticado por banco/cliente pode ser diferente. Tesouraria deve validar.

---

## 10. Próximos passos

1. **Aprovação do modelo de dados** pela tesouraria e por um arquiteto técnico
2. **Coleta dos dados "A confirmar"** no `BANCO_CONFIG` — tarefa para a Fase 0
3. **Anexar contratos do Santander 4131** (Google Drive) para análise complementar
4. **Definição do plano de contas gerencial inicial** — contas listadas no Anexo A são sugestão, podem ser ajustadas
5. **Validação com 5-10 contratos reais** de cada modalidade — golden dataset para testar o motor de cálculo
6. **Detalhamento das telas de cadastro** — UX por modalidade (campos diferentes mostrados conforme modalidade)

---

**Resumo em 1 frase:** o modelo polimórfico (master + extensões por modalidade) acomoda toda a diversidade de FINIMP, REFINIMP, 4131, NCE, Balcão Caixa, FGI e NDF, com regras de negócio configuráveis por banco — sem hard-code e preparado para crescer.
