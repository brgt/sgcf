# Anexo A — Módulo de Valoração de Dívida em Tempo Real e Tratamento de NDFs

**Documento complementar ao Business Case do SGCF**
**Foco:** Capacidade do sistema em mostrar o **valor real da dívida** em qualquer momento, considerando moeda original, taxa de câmbio atual e instrumentos de hedge atrelados.

---

## 1. Por que esse é o módulo mais crítico do projeto

A pergunta mais simples e mais difícil que a tesouraria precisa responder hoje é: **"quanto a empresa deve, em reais, neste momento?"**

Não é uma pergunta retórica — é a base de:
- Demonstrações financeiras (DRE/Balanço)
- Indicadores executivos (Dívida/EBITDA, Dívida Líquida)
- Decisões de novas operações
- Resposta a auditoria, banco e investidor
- Compliance regulatório (covenants, IFRS 9)

Hoje a resposta exige reconciliação manual de:

1. Saldo devedor por contrato em moeda original (USD, EUR, JPY, CNY)
2. Taxa de câmbio aplicável (PTAX D-1, fechamento, cotação à vista)
3. Posição de NDFs atrelados a cada contrato
4. Mark-to-market dos NDFs (com regras diferentes por estrutura: forward, collar)
5. Provisão de juros acumulados desde a última liquidação
6. IRRF gross-up e outros tributos a apropriar

A planilha **não consegue fazer isso em tempo real** com 1.200+ contratos. O resultado é que **a empresa nunca sabe com precisão sua posição real até o fechamento do mês**, e mesmo aí com defasagem.

---

## 2. Anatomia da operação real (caso típico Proxys)

Para deixar concreto, vamos decompor um exemplo real:

### 2.1 Operação de FINIMP em USD com NDF Collar

**Contrato FINIMP:**
- Captação: USD 200.000
- Taxa de câmbio na entrada (D0): R$ 5,00 → contraentrada R$ 1.000.000 em caixa
- Taxa juros: 5,879% a.a.
- Prazo: 180 dias (vencimento em D+180)
- Garantia: 30% Cash Collateral (R$ 300.000 em CDB)

**NDF Collar atrelado:**
- Notional: USD 200.000 (cobre 100% do principal)
- Strike piso (put vendida pela empresa): R$ 5,10
- Strike teto (call comprada pela empresa): R$ 5,40

**No vencimento (D+180), três cenários possíveis:**

| Cenário | USD/BRL | Saída via FINIMP (200k × cotação) | Ajuste NDF | Custo total em BRL | Custo "efetivo" por USD |
|---|---:|---:|---:|---:|---:|
| **A. Dólar baixo** | R$ 4,80 | R$ 960.000 | (R$ 60.000) *empresa paga* | **R$ 1.020.000** | R$ 5,10 |
| **B. Dólar dentro** | R$ 5,25 | R$ 1.050.000 | R$ 0 | **R$ 1.050.000** | R$ 5,25 |
| **C. Dólar alto** | R$ 5,80 | R$ 1.160.000 | R$ 80.000 *empresa recebe* | **R$ 1.080.000** | R$ 5,40 |

**Leitura financeira:**
- Cenário A: dólar caiu, mas o NDF "trava" o piso em 5,10 — empresa paga a diferença → **custo de hedge ativado**
- Cenário B: dólar dentro da banda — NDF inativo, paga ao mercado
- Cenário C: dólar subiu, NDF "trava" o teto em 5,40 — empresa recebe a diferença → **proteção ativada**

**Sem o sistema**, em qualquer momento entre D0 e D+180 a tesouraria precisaria recalcular tudo no Excel para saber: "se eu fechasse hoje, qual seria o impacto em caixa e na DRE?"

### 2.2 Operação com NDF Forward simples (sem cap)

**Contrato FINIMP:**
- Captação: USD 200.000 a R$ 5,00 → R$ 1.000.000
- Mesmo perfil de juros e prazo

**NDF Forward:**
- Strike: R$ 5,50
- Sem banda — qualquer variação ajusta

**No vencimento:**

| Cenário | USD/BRL | Saída FINIMP | Ajuste NDF | Custo total |
|---|---:|---:|---:|---:|
| A. Dólar baixo | R$ 4,80 | R$ 960.000 | (R$ 140.000) *empresa paga* | **R$ 1.100.000** |
| B. Dólar igual ao strike | R$ 5,50 | R$ 1.100.000 | R$ 0 | **R$ 1.100.000** |
| C. Dólar alto | R$ 5,80 | R$ 1.160.000 | R$ 60.000 *empresa recebe* | **R$ 1.100.000** |

**Leitura financeira:** o NDF Forward **fixa o custo em R$ 1.100.000 (R$ 5,50 por USD)** independentemente do dólar. É hedge integral, custo previsível.

### 2.3 Por que isso é difícil de controlar com 1.200 contratos

Imagine fazer essa decomposição **simultânea para todos os contratos abertos**, em **4 moedas (USD, EUR, JPY, CNY)**, com **diferentes estruturas de NDF**, e **taxa de câmbio atualizada continuamente**. É exatamente o que o sistema precisa entregar.

---

## 3. Especificação funcional do módulo

### 3.1 Capacidade #1 — "Quanto a empresa deve agora?"

**Saída esperada (consulta instantânea):**

```
POSIÇÃO DE DÍVIDA — 08/05/2026 14:32

Por moeda original:
  USD ......................... 4.250.000  → R$ 22.525.000  (PTAX 5,30)
  EUR .........................   850.000  → R$  4.930.000  (PTAX 5,80)
  JPY ......................... 80.000.000 → R$  2.984.000  (PTAX 0,0373)
  CNY ......................... 6.500.000  → R$  4.745.000  (PTAX 0,73)
  BRL .........................          —    R$  3.200.000

DÍVIDA BRUTA EM REAIS .......................... R$ 38.384.000

Ajustes de hedge (Mark-to-Market dos NDFs):
  + Posições "in the money" (a receber) ........ R$    420.000
  - Posições "out of the money" (a pagar) ...... R$  (180.000)

DÍVIDA LÍQUIDA APÓS HEDGE ..................... R$ 38.624.000

(-) Caixa e equivalentes ..................... R$ (12.500.000)

DÍVIDA LÍQUIDA FINAL .......................... R$ 26.124.000
```

Com drill-down até o contrato individual.

### 3.2 Capacidade #2 — Mark-to-Market dos NDFs em tempo real

Para cada NDF aberto, o sistema deve calcular continuamente:

| Tipo de NDF | Fórmula de payoff (perspectiva da empresa importadora) |
|---|---|
| **Forward simples** | `Payoff = Notional × (S_atual - K)` *(positivo = empresa recebe; negativo = empresa paga)* |
| **Collar (Range Forward)** | `Se S < K_put:  Payoff = Notional × (S - K_put)  → negativo`<br>`Se K_put ≤ S ≤ K_call: Payoff = 0`<br>`Se S > K_call: Payoff = Notional × (S - K_call)  → positivo` |
| **Target Forward** | Estrutura específica (precisa parametrizar — se a Proxys usar) |
| **Forward com Knock-out** | Estrutura específica (precisa parametrizar — se a Proxys usar) |

**Onde:**
- `Notional` = quantidade de moeda estrangeira contratada
- `S_atual` = cotação spot atual da moeda (em BRL)
- `K_*` = strike(s) do NDF

**Importante:** o payoff é o **valor instantâneo**, não o valor de liquidação. Para a liquidação real, aplica-se o ajuste no vencimento (ou no fixing) com a cotação daquela data.

### 3.3 Capacidade #3 — Fluxo de caixa projetado

A consulta deve mostrar **o que vai sair de caixa em cada data futura**, considerando:

- Amortização do principal (em BRL convertido pela cotação na data + ajuste NDF)
- Pagamento de juros (em BRL convertido + IRRF gross-up)
- Comissões e tarifas
- Liquidação de NDFs (mark-to-market congelado no fixing)

Com **3 cenários** de stress cambial: pessimista (-10%), realista (cotação atual), otimista (+10%).

### 3.4 Capacidade #4 — Alertas de exposição

| Alerta | Trigger |
|---|---|
| **Dívida em moeda X passou Y%** | Variação cambial diária supera limiar |
| **Cobertura de hedge < 80% do principal** | Operação ficou desprotegida |
| **NDF próximo do strike** | Cotação a menos de 1% de acionar piso/teto do collar |
| **Vencimento de NDF sem rolagem definida** | D-15 do vencimento e ainda não foi decidido se renova |
| **Mismatch entre NDF e FINIMP** | Datas, moeda ou notional não batem |

### 3.5 Capacidade #5 — Histórico e simulação

- **Histórico**: posição de dívida em qualquer data passada (snapshot reproduzível)
- **Simulação**: "se eu contratar mais USD 500k em FINIMP hoje, com NDF Forward a 5,30, qual fica minha posição?"

---

## 4. Modelo de dados conceitual

### 4.1 Entidades principais

```
CONTRATO_FINANCIAMENTO
├─ id
├─ banco
├─ modalidade (4131, FINIMP, NCE, CCB)
├─ moeda_original (USD, EUR, JPY, CNY, BRL)
├─ valor_original
├─ taxa_juros_aa
├─ base_calculo_dias (360 ou 365)
├─ data_emissao, data_vencimento
├─ estrutura_amortizacao (bullet, parcelas, customizada)
├─ irrf_jurisdicao (15%, 25%, 0%)
├─ tem_cash_collateral (boolean)
├─ percentual_cash_collateral
├─ pdf_contrato (anexo)
└─ status (ABERTO, QUITADO, EM_ATRASO)

CRONOGRAMA_PAGAMENTO
├─ contrato_id (FK)
├─ data_pagamento
├─ tipo (principal, juros, comissao, tarifa)
├─ valor_moeda_original
├─ valor_brl_estimado (atualizado dinamicamente)
└─ status (PREVISTO, PAGO, ATRASADO)

INSTRUMENTO_HEDGE
├─ id
├─ contrato_id (FK — vinculação ao FINIMP)
├─ tipo (FORWARD_SIMPLES, COLLAR, TARGET_FORWARD, ...)
├─ moeda
├─ notional
├─ data_inicio, data_vencimento
├─ strike_put (nullable — só em collar)
├─ strike_call (nullable — só em collar)
├─ strike_unico (nullable — em forward simples)
├─ contraparte_banco
└─ status (ABERTO, LIQUIDADO, ROLADO)

COTACAO_FX
├─ moeda_par (USD/BRL, EUR/BRL, JPY/BRL, CNY/BRL)
├─ data_hora
├─ tipo (PTAX, B3_FECHAMENTO, SPOT_INTRADAY)
├─ valor
└─ fonte (BCB_API, B3, BLOOMBERG)

POSICAO_SNAPSHOT
├─ data_hora
├─ contrato_id
├─ saldo_devedor_moeda_original
├─ cotacao_aplicada
├─ saldo_devedor_brl
├─ mtm_hedge_brl
└─ valor_liquido_brl
```

### 4.2 Relacionamentos críticos

```
CONTRATO 1───* CRONOGRAMA_PAGAMENTO
CONTRATO 1───0..* INSTRUMENTO_HEDGE
INSTRUMENTO_HEDGE *───1 CONTRATO  (ou null se hedge não vinculado)
COTACAO_FX (série temporal independente)
POSICAO_SNAPSHOT *───1 CONTRATO  (gerado periodicamente para histórico)
```

**Regra crítica:** um NDF pode ser vinculado a um contrato específico (hedge dedicado) **ou** ser independente (hedge de portfólio). O sistema precisa suportar ambos os modelos.

---

## 5. Fontes de cotação (FX feeds)

A precisão do módulo depende da qualidade das cotações. Recomendações:

| Moeda | Fonte primária | Fonte fallback |
|---|---|---|
| **USD/BRL** | API SCB/BCB (PTAX oficial) | B3 (futuro de dólar) |
| **EUR/BRL** | API SCB/BCB | ECB → BCB cross |
| **JPY/BRL** | API SCB/BCB | BoJ → BCB cross |
| **CNY/BRL** | API SCB/BCB (CNY onshore — ver nota) | PBoC → BCB cross |

> **Nota CNY:** A Proxys opera **onshore** (sem entidade offshore), então a cotação a usar é a **PTAX CNY publicada pelo BCB** — que é a referência aceita para operações em yuan no Brasil. Não há necessidade de tratar CNH (offshore Hong Kong) separadamente.

### 5.1 Tratamento da PTAX D0 (decisão da Proxys)

A Proxys usa **PTAX D0 para fechamento e pagamento**, conforme decidido. Há uma nuance operacional importante:

| Momento | O que está disponível | O que o sistema mostra |
|---|---|---|
| **Durante o dia (intraday)** | 4 PTAX parciais (10h, 11h, 12h, 13h) + cotação spot do mercado | **Cotação spot atual** com label "Posição estimada" |
| **Após 13h15 do dia** | PTAX D0 oficial publicada no SISBACEN | **PTAX D0 travada** como cotação contábil oficial daquele dia |
| **Madrugada D+1** | PTAX D0 entra no fechamento contábil | Sistema gera **lançamentos definitivos** com PTAX D0 oficial |

**Implicação técnica:** o sistema precisa distinguir três tipos de visão:

1. **Visão gerencial intraday** — para tomada de decisão rápida (uso da tesouraria durante o dia)
2. **Visão contábil oficial** — para DRE/Balanço, sempre com PTAX D0 (ou D-1 se ainda não publicada)
3. **Visão de fixing/liquidação** — para NDFs que vencem hoje, usa a PTAX D0 ou D-1 conforme cláusula

### 5.2 Frequência de atualização sugerida

- **Intraday**: a cada 5–15 min para dashboard operacional (cotação spot)
- **PTAX parcial**: ingestão automática às 10h, 11h, 12h e 13h
- **PTAX oficial D0**: ingestão automática quando publicada (~13h15) — dispara recálculo da posição contábil do dia
- **EOD (end of day)**: madrugada do D+1, snapshot oficial da posição com PTAX D0 já consolidada

---

## 6. Casos de uso operacionais

### Caso 1 — Reunião de comitê de tesouraria (semanal)

> *"Bom dia, abro o sistema e mostro: posição consolidada da dívida em BRL, breakdown por moeda, exposição cambial líquida (após NDFs), cobertura de hedge por moeda. Tempo de preparação: 0 minutos. Hoje gastamos 2 horas reunindo isso na planilha."*

### Caso 2 — Resposta a pergunta da Diretoria

> *"Diretora pergunta no meio da reunião: 'se o dólar subir 5%, qual o impacto na minha dívida líquida?' — analista abre o simulador, ajusta cotação, mostra cenário em 30 segundos. Hoje a resposta é 'mando depois do almoço'."*

### Caso 3 — Decisão de rolar ou liquidar NDF

> *"NDF Collar de USD 500k vence em 7 dias. Sistema mostra: cotação atual está dentro da banda (sem ajuste); cenário pessimista de queda de 3% gera ajuste negativo de R$ 75k; rolar por 6 meses custa X em prêmio. Decisão informada em minutos."*

### Caso 4 — Fechamento contábil mensal

> *"No último dia do mês, sistema gera automaticamente: saldo devedor por contrato em BRL pela PTAX, ajuste cambial (variação vs PTAX do mês anterior), MTM dos NDFs, lançamentos contábeis sugeridos. Contabilidade revisa e aprova. Tempo: minutos vs dias hoje."*

### Caso 5 — Auditoria pede valoração de derivativos

> *"Auditor pede: 'me mostre como vocês valoram seus NDFs e qual o impacto na DRE.' Sistema gera relatório com fórmula aplicada, fonte da cotação, data/hora do cálculo, histórico de revisões. Em 5 minutos. Hoje é projeto de 2-3 dias."*

---

## 7. Impacto deste módulo no Business Case original

### 7.1 Eleva a prioridade do projeto

Esse módulo **deixa de ser uma melhoria operacional para ser fundamental para fechamento contábil correto**. Sem ele:

- **Risco de DRE/Balanço incorretos** — exposição que pode gerar republicação de demonstrativos
- **Risco regulatório** — IFRS 9 exige mensuração a valor justo de instrumentos financeiros
- **Risco de auditoria** — opinião com qualificação por deficiência de controle interno

### 7.2 Aumenta o ROI estimado

Benefícios adicionais não considerados na versão original:

| Benefício adicional | Valor anual estimado |
|---|---|
| Eliminação de erro de conversão cambial em fechamento | R$ 30-80k (evita ajustes de auditoria) |
| Decisão melhor de rolagem/liquidação de NDFs | R$ 50-150k (uma decisão melhor por trimestre) |
| Resposta rápida a comitê e diretoria | Difícil monetizar — alto impacto na qualidade da gestão |
| Compliance IFRS 9 sem retrabalho | Evita custo de consultoria especializada (~R$ 30-50k/ano) |
| **Total adicional** | **~R$ 110-280k/ano** |

**Novo ROI consolidado:** benefícios diretos sobem de R$ 215k/ano (versão v1.0) para **R$ 325-495k/ano**, reduzindo payback para **~7-9 meses** após estabilização.

### 7.3 Antecipa a entrega na Fase 2 do roadmap

**Reorganização sugerida do roadmap:**

| Fase | Original | Revisado |
|---|---|---|
| Mês 3-5 | MVP cadastro + cálculo | **MVP cadastro + cálculo + valoração FX básica** |
| Mês 6-7 | Indicadores + alertas | **Mark-to-Market completo dos NDFs** *(antecipado)* |
| Mês 8-9 | Conciliação contábil | **Posição em tempo real + simulador** *(antecipado)* |
| Mês 10-11 | Avançadas | Conciliação contábil + workflow |
| Mês 12 | Aposentadoria planilha | Idem |

A justificativa da antecipação é simples: **a maior dor da empresa é não saber quanto deve. Resolver isso primeiro entrega o maior valor percebido**.

---

## 8. Premissas confirmadas com a tesouraria

### 8.1 Estruturas de NDF utilizadas (confirmado)

- **Estruturas hoje em uso**: Forward simples e Collar (Range Forward)
- **Decisão de design**: o sistema deve ser **extensível** para suportar novas estruturas no futuro (Target Forward, KO/KI, Worst-Of, etc.) sem refatoração estrutural
- **Implicação técnica**: a entidade `INSTRUMENTO_HEDGE` deve ter campo `tipo` extensível e suporte a múltiplos `strikes`/`barreiras` (modelo flexível com sub-tipos)

### 8.2 Vinculação NDF ↔ Contrato (confirmado)

**Decisão**: a Proxys contrata exclusivamente **NDFs Dedicados** (vinculação 1:1 com contrato FINIMP).

**Implicações no modelo:**
- `INSTRUMENTO_HEDGE.contrato_id` é **obrigatório** (NOT NULL) — toda hedge deve ter contrato vinculado
- Validações automáticas no cadastro: notional do NDF deve casar com saldo do contrato; data de vencimento do NDF deve ser ≤ vencimento do contrato; moeda deve ser a mesma
- Alerta automático se um contrato em moeda estrangeira **for aberto sem NDF associado** (cobertura de hedge incompleta)

**Recomendação de design:** apesar de hoje ser sempre Dedicado, manter o campo nullable na próxima geração do schema para preservar a opção de evoluir para hedge de portfólio no futuro **sem migration de dados**. A regra de obrigatoriedade fica na camada de validação aplicacional, não no banco — fácil de relaxar se a política mudar.

### 8.3 Cotações por momento da operação (configurável)

**Decisão**: o sistema deve manter o tipo de cotação como **parâmetro configurável** por momento da operação. Isso permite ajustar a regra sem mudança de código quando bancos ou políticas mudam.

**Mapeamento de cotações por momento (configuração inicial baseada nas práticas atuais da Proxys):**

| Momento da operação | Tipo de cotação | Justificativa |
|---|---|---|
| **Cotação inicial do FINIMP** (entrada/desembolso) | **PTAX D-1** | Padrão usado pelos bancos para fechamento da operação no momento da contratação |
| **Fixing de juros durante a operação** | **PTAX D0** | Conversão das parcelas de juros em datas intermediárias |
| **Pagamento/liquidação do principal** | **Cotação intraday** (cotação spot do banco no momento do fechamento) | Banco passa cotação no ato do fechamento de câmbio para pagamento |
| **Liquidação do NDF (fixing)** | **PTAX D0** do fixing | Padrão de mercado para NDFs onshore |
| **Mark-to-Market gerencial intraday** | **Cotação spot atual** | Para visão de posição em tempo real durante o dia |
| **Mark-to-Market contábil oficial** | **PTAX D0** (D-1 enquanto D0 não publicada) | Para fechamento contábil mensal |

**Modelo de dados — entidade `PARAMETRO_COTACAO`:**

```
PARAMETRO_COTACAO
├─ id
├─ momento (DESEMBOLSO, FIXING_JUROS, LIQUIDACAO_PRINCIPAL,
│           LIQUIDACAO_NDF, MTM_GERENCIAL, MTM_CONTABIL)
├─ tipo_cotacao (PTAX_D_MENOS_1, PTAX_D0, SPOT_INTRADAY, MEDIA_PERIODO)
├─ aplicavel_banco_id (FK opcional - permite override por banco)
├─ aplicavel_modalidade (FINIMP, 4131, NCE, CCB - permite override por modalidade)
├─ data_inicio_vigencia
└─ data_fim_vigencia
```

**Vantagens da configurabilidade:**
- Se o BB mudar a regra para cotação intraday, ajusta-se um parâmetro (não código)
- Permite override pontual por banco ou modalidade
- Mantém histórico de vigência das regras (auditoria preserva qual regra estava ativa em cada operação)
- Facilita simulação de cenários ("e se eu tivesse usado PTAX D-1 em vez de intraday no último fechamento?")

### 8.4 Moedas (confirmado)

- **Operações onshore**: bancos brasileiros operando com correspondentes no exterior. Sem entidades offshore próprias da Proxys.
- **CNY**: usar PTAX CNY publicada pelo BCB. Não há necessidade de tratar CNH (offshore HK) separadamente.
- **JPY**: PTAX JPY/BRL do BCB (notação: 1 JPY = R$ 0,037X — o sistema deve **exibir o valor multiplicado por 100 ou 1.000** em interfaces para evitar erros de leitura, prática comum no mercado).

### 8.5 Estratégia de integração com ERP — abordagem em 2 fases

**Decisão estratégica REVISADA (maio/2026)**: o MVP (Fase 1) será **100% standalone**, sem qualquer integração ou exportação para o SAP Business One 11 HANA — nem mesmo via planilha Excel. O sistema é puramente **gerencial**, com **plano de contas próprio em padrão de mercado**. A integração com o SAP entra apenas na Fase 2.

**Justificativa:**
- Menor dependência de TI/fornecedor SAP no início → entrega mais rápida
- Reduz risco do projeto (integrações são fonte comum de atrasos)
- Permite validar a funcionalidade core antes de adicionar complexidade
- Tesouraria começa a usar o sistema sem esperar approvals/licenciamento de Service Layer
- Diretoria vê valor antes do investimento em integração

#### Fase 1 (MVP) — Sistema 100% gerencial standalone

**Sem integração com SAP, sem exportação para SAP, sem dependência da contabilidade.**

O SGCF opera com **plano de contas gerencial próprio** (padrão de mercado para tesouraria/dívida) que **não precisa coincidir** com o plano de contas do SAP B1 nesta fase. O sistema gera relatórios financeiros gerenciais para uso interno da tesouraria e diretoria.

**Plano de contas gerencial sugerido (padrão de mercado, configurável):**

```
1. ATIVO
   1.1 Caixa e Equivalentes
       1.1.1 Conta Corrente em BRL
       1.1.2 CDBs e Aplicações Livres
   1.2 Garantias Cativas
       1.2.1 CDB Cativo (Cash Collateral)
       1.2.2 Outras garantias bloqueadas
   1.3 Instrumentos Derivativos (MTM positivo)
       1.3.1 NDFs a receber

2. PASSIVO
   2.1 Empréstimos e Financiamentos
       2.1.1 FINIMP em moeda estrangeira
       2.1.2 4131 em moeda estrangeira
       2.1.3 NCE/CCE em BRL
       2.1.4 Balcão (Caixa)
       2.1.5 FGI (BNDES via banco intermediário)
       2.1.6 REFINIMPs ativos
   2.2 Instrumentos Derivativos (MTM negativo)
       2.2.1 NDFs a pagar
   2.3 Juros a Pagar
       2.3.1 Juros provisionados FINIMP
       2.3.2 Juros provisionados 4131
       2.3.3 Juros provisionados outros
   2.4 Tributos a Recolher
       2.4.1 IRRF s/ juros remetidos exterior
       2.4.2 IOF câmbio a recolher

3. RESULTADO FINANCEIRO
   3.1 Receitas Financeiras
       3.1.1 Rendimento de CDB cativo
       3.1.2 Ganho com NDF (MTM e liquidação)
       3.1.3 Variação cambial ativa
   3.2 Despesas Financeiras
       3.2.1 Juros sobre FINIMP
       3.2.2 Juros sobre 4131
       3.2.3 Juros sobre demais modalidades
       3.2.4 IRRF gross-up
       3.2.5 IOF câmbio
       3.2.6 Comissões SBLC, CPG, garantia
       3.2.7 Tarifas (ROF, CADEMP, cartório)
       3.2.8 Perda com NDF (MTM e liquidação)
       3.2.9 Variação cambial passiva
       3.2.10 Custo de oportunidade do CDB cativo (gerencial)
```

> **Importante**: este plano é **gerencial**, não contábil oficial. Cada conta tem `codigo_gerencial` no SGCF e um campo `codigo_sap_b1` (que fica vazio no MVP, será preenchido na Fase 2 quando ocorrer o de-para com o plano oficial do SAP).

**Vantagens dessa abordagem:**
- Tesouraria começa a usar imediatamente, sem bloqueio de TI ou contabilidade
- Equipe contábil não precisa aprender novo sistema no MVP
- Contas são pensadas para gestão de dívida (granularidade adequada), não para reporting fiscal
- Reduz drasticamente escopo, prazo e custo do MVP
- Acelera entrega de valor

**Limitação consciente (resolvida na Fase 2):**
- Tesouraria e contabilidade trabalham com fontes diferentes durante o MVP — exige reconciliação manual periódica (mas isso já é feito hoje)
- Demonstrativos oficiais continuam saindo do SAP B1 sem alteração

#### Fase 2 (pós-MVP) — De-para com plano de contas SAP + Integração

Após validação do MVP gerencial, a Fase 2 estabelece a ponte com o sistema contábil oficial:

**Etapa 2.1 — De-para de plano de contas:**
- Mapear cada conta gerencial do SGCF (ex: `2.1.1 FINIMP em moeda estrangeira`) para a conta contábil oficial correspondente no SAP B1 (ex: `2.01.001.0001 — Empréstimos e Financiamentos em Moeda Estrangeira`)
- Esse mapeamento é **um cadastro de configuração** no SGCF, sem mudança estrutural do banco

**Etapa 2.2 — Integração via Service Layer (API REST do SAP B1):**
- Criação automática de journal entries
- Sincronização de centros de custo e dimensões (Line of Business)
- Webhook de "lançamento postado com sucesso"

**Pré-requisitos para Fase 2:**
- Service Layer habilitado e licenciado no SAP B1
- De-para de plano de contas validado pela contabilidade
- Política definida de "auto-post vs review-then-post"

#### Decisão de design crítica (mesmo na Fase 1)

O SGCF deve ser desenhado para receber o de-para na Fase 2 sem refatoração:

- **Estrutura canônica de "lançamento contábil"** definida no SGCF desde o MVP
- **Tabela `PLANO_DE_CONTAS_GERENCIAL`** com campo `codigo_sap_b1` nullable (vazio no MVP, preenchido na Fase 2)
- **Camada de exportação isolada** (módulo separado) — facilita adicionar adapter SAP depois

**Resultado:** quando chegar a Fase 2, a evolução é (a) preencher o campo `codigo_sap_b1` em cada conta gerencial, (b) adicionar adapter da API do SAP — sem refatoração estrutural.

### 8.6 Pendências para próxima rodada

- [x] ~~Confirmar modelo de NDF (Dedicado vs. Portfolio)~~ → **Resolvido: Dedicado 1:1**
- [x] ~~Definir tipo de cotação para cada momento da operação~~ → **Resolvido: configurável (seção 8.3)**
- [x] ~~Estratégia de integração com SAP B1~~ → **Resolvido: bridge manual no MVP (Fase 1), API na Fase 2**
- [ ] Confirmar particularidades de NDFs específicos (alguns podem usar média de PTAX em vez de D0 — cadastrar como exceção)
- [ ] Levantar amostra de 5-10 contratos com NDF para servir de golden dataset
- [ ] Definir layout exato da planilha Excel de exportação para SAP (alinhar com contabilidade)
- [ ] Definir lista de contas contábeis utilizadas em operações de dívida (cadastro inicial no SGCF)

---

## 9. Próximos passos recomendados

1. **Validar com o autor (Welysson) os pontos de seção 8** — alinhar premissas antes do desenho técnico
2. **Convidar contabilidade para discussão** — alinhar como os ajustes contábeis devem fluir
3. **Levantar amostra de 5-10 contratos com NDF atrelado** — usar como golden dataset para validar fórmulas
4. **Apresentar este anexo à Diretoria junto com o Business Case principal** — mostrar a profundidade do problema e o nível de cuidado da solução
5. **Decidir: este módulo entra no MVP da Fase 1, ou pode ficar para Fase 2?** — minha recomendação é **MVP**, dado o impacto direto em demonstrativos

---

**Resumo em 1 frase:** sem capacidade de mostrar o valor real da dívida em tempo real (multi-moeda, com NDFs MTM), o sistema é apenas uma planilha bonita — com essa capacidade, vira a ferramenta central da gestão financeira da empresa.
