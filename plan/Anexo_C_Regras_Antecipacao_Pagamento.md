# Anexo C — Regras de Antecipação de Pagamento (Pré-pagamento e Liquidação Antecipada)

**Documento complementar ao Business Case do SGCF**
**Foco:** modelar formalmente as regras de antecipação de pagamento (pré-pagamento parcial, liquidação antecipada total, amortização extraordinária) para que o sistema possa **simular o custo real de antecipar uma operação** antes da analista solicitar ao banco.
**Base de análise:** cláusulas literais extraídas dos contratos modelo da pasta `CONTRATOS_MODELOS/`.

---

## 1. Por que esse módulo é estratégico

A antecipação de pagamento é uma das decisões financeiras de **maior alavancagem** que a tesouraria toma — e hoje é feita "no escuro", sem comparativo objetivo entre alternativas:

- **Pergunta real do dia-a-dia**: "se eu liquidar agora a operação X com sobra de caixa, quanto economizo de juros vs quanto pago de break funding fee?"
- **Pergunta estratégica**: "vale a pena trocar uma dívida cara antiga por uma nova mais barata? Quanto custa o pré-pagamento da antiga vs quanto economizo com a nova?"
- **Pergunta de planejamento**: "tenho R$ X de caixa em excesso por 90 dias — qual operação me dá maior retorno se eu amortizar antecipadamente?"

Essas decisões **dependem de regras radicalmente diferentes por banco**:
- **BB FINIMP** cobra 1% flat + indenização "expectativa frustrada"
- **Sicredi FINIMP** cobra **juros do período TOTAL** (efetivamente nada economiza)
- **FGI BV** aplica MTM (saldo + juros futuros − desconto à taxa de mercado)
- **Caixa Balcão** usa fórmula regulada BACEN (Resoluções 3401/06 e 3516/07)
- **Itaú FINIMP** segue pattern ISDA-like (não confirmado nos contratos disponíveis)

**Sem o módulo de simulação**, decisões erradas custam dezenas de milhares de reais por ano. **Com o módulo**, a tesouraria responde em segundos qual a opção ótima.

---

## 2. Modelo conceitual

### 2.1 Tipos de antecipação

| # | Tipo | Descrição | Exemplo |
|---|---|---|---|
| **T1** | **Liquidação Total Antecipada** | Quita 100% do saldo restante antes do vencimento original. Encerra o contrato. | FINIMP USD 200k que vence em 60d — paga hoje USD 200k + juros pro rata + custos |
| **T2** | **Liquidação Parcial — Redução de Prazo** | Paga parte do principal mantendo o valor das parcelas restantes; encurta o prazo final | Quita R$ 1MM de uma dívida de R$ 5MM em 36 parcelas — passa para 28 parcelas iguais |
| **T3** | **Liquidação Parcial — Redução de Parcela** | Paga parte do principal mantendo o prazo final; reduz o valor das parcelas restantes | Quita R$ 1MM mantendo 36 parcelas; cada parcela cai proporcionalmente |
| **T4** | **Amortização Extraordinária Avulsa** | Paga uma parcela inteira (ou conjunto) à frente do cronograma, sem alterar a estrutura | Antecipar a parcela 12 que venceria em 6 meses |
| **T5** | **Refinanciamento Interno** | Liquida o contrato com recursos de nova operação tomada no mesmo banco | Caixa: sem TLA neste caso |

> **Decisão de design:** o sistema **suporta os 5 tipos**, mas o modo permitido depende do banco/modalidade. Algumas modalidades só aceitam T1 (FINIMP bullet, por natureza), outras aceitam todos.

### 2.2 Taxonomia de custos identificados

Após análise dos 5 contratos completos disponíveis, identifiquei **15 componentes de custo distintos** que podem ou não compor o valor total de uma antecipação. Esta taxonomia é **a base do modelo de dados**.

| # | Código | Componente | Definição | Aplicabilidade |
|---|---|---|---|---|
| C1 | `PRINCIPAL_A_QUITAR` | Saldo principal antecipado | Valor de principal sendo pago antes do vencimento | TODAS |
| C2 | `JUROS_PRO_RATA_ATE_DATA` | Juros pro rata acumulados | Juros desde o último pagamento até a data efetiva da antecipação | BB FINIMP, FGI BV (na composição), Caixa, Itaú (provável) |
| C3 | `JUROS_PERIODO_TOTAL_FORCADO` | Juros do período total contratado | Juros como se o contrato fosse até o final original — sem desconto | **Sicredi FINIMP** (regra exclusiva, vinculada à anuência) |
| C4 | `JUROS_FUTUROS_CAPITALIZADOS` | Juros futuros compostos | Saldo capitalizado pela taxa contratada até o vencimento original | FGI BV (componente intermediário) |
| C5 | `DESCONTO_TAXA_MERCADO` | Desconto pela taxa de mercado atual | Aplicado sobre `JUROS_FUTUROS_CAPITALIZADOS` considerando prazo remanescente | **FGI BV** (subtraído) |
| C6 | `BREAK_FUNDING_FEE_FLAT` | Multa flat sobre antecipado | % fixo sobre principal + juros antecipados | **BB FINIMP** (1% flat) |
| C7 | `INDENIZACAO_EXPECTATIVA_FRUSTRADA` | Indenização por perda de rentabilidade | Compensação caso a caso ao banco; calculada e apresentada pelo banco | **BB FINIMP** (indeterminado a priori) |
| C8 | `TLA_BACEN` | Tarifa de Liquidação Antecipada | Regulada pelas Resoluções BACEN 3401/06 e 3516/07. Fórmula explícita | **Caixa Balcão** |
| C9 | `IOF_COMPLEMENTAR` | IOF adicional ou restituição | Em operações com câmbio, antecipação pode gerar IOF complementar ou restituir parte | FINIMP/4131/REFINIMP (todos com câmbio) |
| C10 | `SPREAD_CAMBIAL_ANTECIPACAO` | Spread cambial novo | Diferença entre PTAX e cotação efetiva no fechamento de câmbio antecipado | FINIMP/4131 (todos com câmbio) |
| C11 | `VARIACAO_CAMBIAL_REALIZADA` | Variação cambial entre datas | Diferença entre cotação contratada e cotação na antecipação — afeta valor BRL | FINIMP/4131/REFINIMP |
| C12 | `IRRF_COMPLEMENTAR` | IRRF gross-up sobre juros antecipados | Recolhimento de IR sobre juros pagos ao exterior | FINIMP/4131 com captação no exterior |
| C13 | `MULTA_NDF_DESCASADO` | Custo de descasamento de hedge | Se o NDF foi contratado para data específica e a antecipação a desfaz, há custo de cancelamento | Operações com NDF dedicado |
| C14 | `TARIFA_OPERACAO_ANTECIPADA` | Tarifa fixa de processamento | Algumas operações cobram tarifa simbólica para registrar a antecipação | Variável |
| C15 | `RESTITUICAO_CDB_CATIVO` | Liberação proporcional do CDB cativo | Em liquidação parcial: parte do CDB cativo é liberada (entrada de caixa); em total: 100% liberado | CDB cativo aplicável |

> **Princípio**: cada componente é uma **linha do "extrato de simulação"** — o sistema mostra o detalhamento item a item, não apenas o total. Isso permite à analista negociar com o banco (ex: "por que essa indenização adicional? Mostre o cálculo").

### 2.3 Restrições e regras operacionais (não custos)

Além dos custos, o módulo deve modelar **restrições operacionais** que afetam a viabilidade da antecipação:

| Código | Restrição | Banco/modalidade exemplo |
|---|---|---|
| `AVISO_PREVIO_MIN_DIAS_UTEIS` | Quantidade mínima de dias úteis de aviso prévio | BB FINIMP: 20 dias úteis |
| `VALOR_MINIMO_PARCIAL_ABS` | Valor mínimo (BRL/USD) para liquidação parcial | — |
| `VALOR_MINIMO_PARCIAL_PCT` | % mínimo do saldo de principal | BB FINIMP: 20% |
| `EXIGE_ANUENCIA_EXPRESSA` | Banco precisa aprovar caso a caso (não é direito unilateral do tomador) | Sicredi FINIMP |
| `EXIGE_PARCELA_INTEIRA` | Liquidação parcial só aceita amortização de parcela(s) cheia(s), não fracionada | FGI BV |
| `EXIGE_AUTORIZACAO_GOVERNAMENTAL` | Pré-pagamento depende de autorização (BACEN, etc.) | BB FINIMP (operações com ROF) |
| `JANELA_PERMITIDA` | Antecipação só permitida em janelas específicas (ex: data de pagamento de juros) | — (não observado, mas modelar para futuros) |

---

## 3. Análise contrato-a-contrato (cláusulas literais extraídas)

### 3.1 BB FINIMP — Cláusula 12 (PAGAMENTO ANTECIPADO DA OPERAÇÃO)

**Documento-fonte:** `CONTRATOS_MODELOS/CONTRATO_FINIMP_BANCO_DO_BRASIL.pdf`, p. 7

**Texto literal:**
> "O(A) FINANCIADO(A) poderá liquidar total ou parcialmente o FINANCIAMENTO, desde que obtidas as autorizações governamentais exigíveis nos termos da legislação brasileira em vigor, mediante solicitação formal ao BANCO com no mínimo 20 (vinte) dias úteis de antecedência. Ao valor a ser liquidado, serão acrescidos os juros calculados até a data do efetivo pagamento. No caso de liquidação parcial, o valor da parcela será de no mínimo 20% (vinte por cento) do saldo de principal ainda não pago. O(A) FINANCIADO(A) pagará 1% (um por cento) flat sobre o valor pago antecipadamente (principal e juros acumulados) a título de break funding fee acrescido do montante suficiente para compensar o BANCO de quaisquer perdas, custos ou penalidades incorridos, inclusive a expectativa de rentabilidade frustrada do período interrompido do crédito disponibilizado ao(à) FINANCIADO(A), em decorrência da concessão e manutenção do empréstimo. O valor a ser pago será apresentado ao(à) FINANCIADO(A) pelo BANCO."

**Modelo formal:**

| Atributo | Valor |
|---|---|
| Permite total | ✅ |
| Permite parcial | ✅ |
| Aviso prévio | **20 dias úteis** |
| Valor mínimo parcial | **20% do saldo principal** |
| Anuência expressa | ⚠ Implícita ("autorização governamental") |
| Componentes de custo | C1 + C2 + C6 (1% flat sobre C1+C2) + C7 (a apresentar) |

**Padrão de cálculo (Padrão A — "Pro rata + break flat + indenização"):**
```
valor_antecipado = C1 + C2
break_flat       = (C1 + C2) × 0.01
indenizacao      = (apresentado pelo banco — variável a coletar)
TOTAL            = valor_antecipado + break_flat + indenizacao
```

**Para FINIMP em USD adicione**: C9 (IOF câmbio sobre conversão antecipada), C10 (spread cambial), C11 (variação cambial vs cotação contratada), C12 (IRRF gross-up sobre C2).

---

### 3.2 BB 4131 — não disponível diretamente

**Documento-fonte:** `CONTRATOS_MODELOS/CONTRATO_4131_BANCO_DO_BRASIL.pdf`

**Observação:** este arquivo é o **Contrato de Outorga de Garantia e Contragarantia** (SBLC + Cessão Fiduciária do CDB cativo). O termo de financiamento 4131 propriamente dito é emitido pelo **BB London** (Anexo I do contrato menciona "LINE OF CREDIT: WORKING CAPITAL LOAN 'LEI 4131'") e **não está incluído** na pasta de contratos modelo.

**Cláusulas relevantes do que existe:**
- Cláusula 15.1 (Juros por Atraso): juros de atraso de **PRIME RATE + 7,890% a.a.** se houver débito sem pagamento do contravalor
- Cláusula 15.2 (Transferência para Créditos Vencidos): se o BB honrar a SBLC sem pagamento prévio do tomador, encargos passam a **TMS (Selic) + 7% a.m.**

**Lacuna registrada (decisão na Fase 0):** obter o termo do BB London ou cláusulas-padrão de pré-pagamento. Hipótese de mercado: 4131 BB segue **Padrão A** similar ao FINIMP (break funding fee + juros pro rata + indenização ISDA-like).

---

### 3.3 Itaú FINIMP — não disponível diretamente

**Documentos-fonte:** `CONTRATOS_MODELOS/CONTRATO_FINIMP_ITAU_TERMO-Original.pdf` e `CONTRATO_ITAU-_TERMO-Manifesto.pdf`

**Observação:** ambos os arquivos são **Cessão Fiduciária de Direitos Creditórios** (garantia em CDB cativo de R$ 393.295,25 vinculado ao instrumento CPGI 320410.69017 de USD 232.975,70). O **termo de financiamento FINIMP propriamente dito (CPGI 320410.69017)** referenciado no Anexo II **não está incluído** na pasta.

**O que existe na cessão fiduciária (Anexo III, item B — para ACC/ACE, similar):**
- Em caso de **cancelamento** de contrato de câmbio: paga contravalor em moeda nacional + juros + tributos
- Em caso de **baixa** de contrato de câmbio: paga contravalor em moeda nacional + juros calculado pela **maior** entre (i) taxa contratada e (ii) Taxa de Conversão (taxa média de venda divulgada pelo BCB no dia anterior)
- Cobertura de "encargos financeiros determinados pelo Banco Central do Brasil" + tributos
- Variação cambial positiva é absorvida pelo tomador

Isso indica que Itaú aplica **Padrão A com piso na taxa de mercado** — mas as condições específicas do FINIMP CPGI não estão documentadas no folder.

**Lacuna registrada:** obter cópia do termo FINIMP Itaú. Hipótese a confirmar: padrão ISDA com break funding fee similar ao BB.

---

### 3.4 Sicredi FINIMP — Cláusula Segunda §5º (PAGAMENTO ANTECIPADO **VEDADO**)

**Documento-fonte:** `CONTRATOS_MODELOS/CONTRATO_FINIMP_SICREDI.pdf`, p. 3

**Texto literal:**
> "É vedado à IMPORTADORA o pagamento antecipado do financiamento, despesas e encargos remuneratórios, salvo se houver expressa anuência do BANCO e, na ocorrência do pagamento antecipado do financiamento, os encargos remuneratórios serão cobrados pelo período total contratado."

**Modelo formal:**

| Atributo | Valor |
|---|---|
| Permite total | ⚠ Vedado por padrão; possível com anuência expressa |
| Permite parcial | ⚠ Mesma regra |
| Aviso prévio | Não especificado |
| Valor mínimo parcial | Não especificado |
| Anuência expressa | ✅ **Obrigatória** |
| Componentes de custo | C1 + **C3** (juros do período total — efetivamente sem desconto) |

**Padrão de cálculo (Padrão B — "Sem desconto"):**
```
juros_periodo_total = principal × taxa × prazo_total_contratado / base
TOTAL = C1 + juros_periodo_total
```

**Implicação prática:** antecipar Sicredi FINIMP **não economiza juros** (paga como se fosse até o vencimento). **Só faz sentido** se o objetivo for liberar limite de crédito ou eliminar exposição cambial — nunca para reduzir custo financeiro.

**Atenção:** o sistema deve emitir **alerta crítico** ao simular antecipação Sicredi: "Sicredi cobra juros do período total contratado — antecipação não gera economia de juros."

---

### 3.5 FGI BV — Cláusula 6 (LIQUIDAÇÃO ANTECIPADA com MTM)

**Documento-fonte:** `CONTRATOS_MODELOS/CONTRATO_FGI_BANCO_BV.pdf`, p. 4

**Texto literal:**
> "O Emitente desde já reconhece e aceita que, na hipótese de liquidação desta Cédula, seja ela parcial ou total, antes da data de vencimento originalmente contratada, o valor a ser pago pelo Emitente corresponderá ao saldo de principal não amortizado, acrescido dos encargos remuneratórios estabelecidos no item 3.4 do Preâmbulo, **capitalizados sob o regime composto de capitalização de juros até a data de vencimento original, e descontado pela taxa de juros apurada pelo Banco Votorantim na data do respectivo pagamento, de acordo com as condições de mercado aplicáveis para operações de volume, prazo e natureza semelhantes ao do crédito a ser liquidado, considerando o prazo remanescente da operação financeira**."
>
> "6.1. Na hipótese de liquidação antecipada parcial, o Emitente deverá amortizar o valor de uma ou mais parcelas indicadas no Preâmbulo deste instrumento, **não sendo admitidas amortizações fracionadas**."

**Modelo formal:**

| Atributo | Valor |
|---|---|
| Permite total | ✅ |
| Permite parcial | ✅ apenas em parcelas inteiras |
| Aviso prévio | Não especificado |
| Valor mínimo parcial | Uma parcela inteira |
| Anuência expressa | ❌ (direito do emitente) |
| Componentes de custo | C1 + C4 − C5 |

**Padrão de cálculo (Padrão C — "MTM com desconto a taxa de mercado"):**
```
saldo_a_quitar             = principal não amortizado (= soma das parcelas amortizadas antecipadamente, em T2/T3/T4)
juros_capitalizados_futuro = saldo_a_quitar × (1 + taxa_contratada)^(prazo_remanescente_dias / base) − saldo_a_quitar
valor_futuro_total         = saldo_a_quitar + juros_capitalizados_futuro
desconto                   = valor_futuro_total × [1 − 1/(1 + taxa_mercado_atual)^(prazo_remanescente_dias / base)]
TOTAL                      = valor_futuro_total − desconto
                           = valor_futuro_total × 1/(1 + taxa_mercado_atual)^(prazo_remanescente_dias / base)
```

**Equivalentemente:**
```
TOTAL = saldo_a_quitar × [(1 + taxa_contratada) / (1 + taxa_mercado_atual)]^(prazo_remanescente_dias / base)
```

**Sensibilidade econômica:**
- Se `taxa_mercado_atual ≥ taxa_contratada` → `TOTAL ≤ saldo_a_quitar` (vantajoso antecipar)
- Se `taxa_mercado_atual < taxa_contratada` → `TOTAL > saldo_a_quitar` (desvantajoso — perde-se rentabilidade implícita)

**Sutileza importante:** o `taxa_mercado_atual` é apurada **pelo banco** ("apurada pelo Banco Votorantim na data do respectivo pagamento") — pode haver subjetividade. O sistema deve permitir que a analista informe a taxa cotada pelo banco e simule sensibilidades em torno dela.

---

### 3.6 Caixa Balcão — Cláusulas 5 §6º + 17 (TLA BACEN regulada)

**Documento-fonte:** `CONTRATOS_MODELOS/CONTRATO_FINANCIAMENTO_BALCAO_CAIXA.pdf`, p. 6 e 11

**Texto literal — Cláusula 5 §6º:**
> "É facultada à CREDITADA, a qualquer tempo, realizar amortização extraordinária para redução do saldo devedor, bem como fazer a liquidação antecipada do saldo devedor, com **abatimento proporcional de juros do período futuro, caso já estejam embutidos**."

**Texto literal — Cláusula 17 (DA AMORTIZAÇÃO EXTRAORDINÁRIA / LIQUIDAÇÃO ANTECIPADA):**
> "A CREDITADA poderá, a qualquer tempo, realizar a liquidação antecipada do saldo devedor, bem como pagamentos extraordinários para amortizar a dívida, observando-se a aplicação dos encargos correspondentes, que serão calculados às taxas vigentes."
>
> "Parágrafo Primeiro — É devido pela CREDITADA o pagamento de Taxa de Liquidação Antecipada — TLA em caso de marcação do campo 10 do preâmbulo, conforme Resoluções BACEN 3401/06 e 3516/07."
>
> "Parágrafo Segundo — A tarifa é calculada com base no saldo devedor e no prazo remanescente da operação, sendo 2% (dois por cento) sobre o saldo devedor apurado na data da amortização/liquidação ou de 0,1% (um décimo por cento) do saldo por mês remanescente, conforme fórmula abaixo, sendo cobrado o **maior valor apurado**, conforme fórmula abaixo:
>
> TLA = VTD (0,1% × Pzr), onde:
> - TLA = Taxa de Liquidação Antecipada
> - VTD = Valor total do débito, apurado na data da liquidação/amortização
> - Pzr = Prazo Remanescente da operação, em meses"
>
> "Parágrafo Terceiro — Na hipótese de liquidação antecipada com recursos originados exclusivamente da contratação de nova operação de crédito na CAIXA, **não há incidência da tarifa de liquidação antecipada** no contrato liquidado."
>
> "Parágrafo Quarto — No caso de amortizações extraordinárias/liquidação antecipada realizada **por força de eventos previstos em cláusulas contratuais**, não há incidência de tarifa de amortização extraordinária ou de liquidação antecipada."

**Modelo formal:**

| Atributo | Valor |
|---|---|
| Permite total | ✅ |
| Permite parcial | ✅ (amortização extraordinária) |
| Aviso prévio | Não especificado |
| Valor mínimo parcial | Não especificado |
| Anuência expressa | ❌ (direito da creditada) |
| Componentes de custo | C1 + C8 (TLA) − abatimento_juros_futuros (se prefixado) |
| Isenções da TLA | Refinanciamento interno (T5); pré-pagamento forçado por cláusula |

**Padrão de cálculo (Padrão D — "TLA BACEN"):**
```
VTD                = saldo_devedor_na_data + juros_pro_rata_ate_data
TLA_opcao_A        = VTD × 0.02                          # 2% sobre VTD
TLA_opcao_B        = VTD × 0.001 × Pzr_meses             # 0,1% × prazo remanescente em meses × VTD
TLA                = max(TLA_opcao_A, TLA_opcao_B)        # cobra o maior

# Abatimento de juros futuros (apenas em operação prefixada)
if taxa_prefixada and juros_futuros_embutidos_no_saldo:
    abatimento = juros_futuros_proporcionais_ao_que_foi_quitado
else:
    abatimento = 0

TOTAL              = VTD + TLA − abatimento

# Isenções
if origem_recursos == "nova_operacao_mesmo_banco" or motivo == "forcada_por_clausula":
    TLA = 0
```

**Cuidado:** a Caixa opera com 2 sistemas de amortização (SAC e Price). Em SAC, juros são pagos pro rata e o abatimento de juros futuros é simples; em Price, parcelas são fixas e cada parcela tem mistura de principal + juros — o abatimento exige recálculo do cronograma remanescente.

---

### 3.7 REFINIMP Itaú — não disponível diretamente

**Documento-fonte:** `CONTRATOS_MODELOS/CONTRATO_REFINIMP_ITAU.pdf`

**Observação:** este arquivo é um **Comprovante de Operação de Câmbio** (registro de remessa de juros de USD 8.970,01), não o termo de REFINIMP. Não contém cláusulas de antecipação.

**Lacuna registrada:** obter cópia do termo de REFINIMP Itaú. Hipótese: como REFINIMP é tecnicamente uma rolagem do FINIMP original, as regras de antecipação tendem a seguir o pattern do FINIMP do mesmo banco.

---

### 3.8 Outros bancos (sem contratos disponíveis na pasta)

| Banco | Status | Hipótese de mercado |
|---|---|---|
| **Bradesco** | Não há contrato modelo | Pattern ISDA-like (similar a Itaú) |
| **Santander** | 4 PDFs em `CONTRATOS_4131_SANTANDER/` (CCB, CFA, CFD, contrato de câmbio) | Pattern ISDA-like; **prioridade alta** para Fase 0 |
| **Daycoval** | Não há contrato modelo | A confirmar |
| **Safra** | Não há contrato modelo | Pattern ISDA-like (similar a BB/Itaú) |
| **ABC** | Não há contrato modelo | A confirmar |

> **Tarefa para Fase 0:** coletar contratos modelo dos demais bancos e completar este Anexo C com as cláusulas de antecipação de cada um.

---

## 4. Síntese — Os 5 padrões de cálculo

Após análise dos 5 contratos completos, **5 padrões distintos** foram identificados. Esta é a base do **Strategy pattern** no motor de cálculo:

| Padrão | Nome | Bancos | Custo dominante | Característica |
|---|---|---|---|---|
| **A** | "Pro rata + break flat + indenização" | BB FINIMP (provavelmente Itaú, Bradesco, Santander, Safra) | C1 + C2 + C6 + C7 | Break funding fee fixo + indenização variável apresentada pelo banco |
| **B** | "Sem desconto" (juros do período total) | Sicredi FINIMP | C1 + C3 | Cobra juros como se fosse até o vencimento — não economiza |
| **C** | "MTM com desconto a taxa de mercado" | FGI BV (PEAC) | C1 + C4 − C5 | Equivalente a precificar a operação como derivativo |
| **D** | "TLA BACEN" | Caixa Balcão (e operações regulamentadas similares) | C1 + C8 − abatimento | Fórmula explícita BACEN + abatimento de juros futuros se prefixado |
| **E** | "Pagamento ordinário com abatimento" | Caixa Balcão Cláusula 5 §6º (caso prefixado embutido) | C1 − abatimento_juros_futuros | Para amortização extraordinária sem TLA |

---

## 5. Modelo de dados

### 5.1 Extensão do `BANCO_CONFIG` (do Anexo B §3)

Adiciona-se à configuração por banco/modalidade os seguintes campos:

```sql
-- Em BANCO_CONFIG, novos campos relacionados a antecipação:
ALTER TABLE banco_config ADD COLUMN aceita_liquidacao_total BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE banco_config ADD COLUMN aceita_liquidacao_parcial BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE banco_config ADD COLUMN exige_anuencia_expressa BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE banco_config ADD COLUMN exige_parcela_inteira BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE banco_config ADD COLUMN aviso_previo_min_dias_uteis INTEGER NULL;
ALTER TABLE banco_config ADD COLUMN valor_minimo_parcial_pct numeric(5,4) NULL;     -- ex: 0.20 = 20%
ALTER TABLE banco_config ADD COLUMN padrao_antecipacao TEXT NOT NULL DEFAULT 'A';   -- A/B/C/D/E
ALTER TABLE banco_config ADD COLUMN break_funding_fee_pct numeric(7,4) NULL;        -- ex: 0.01 = 1%
ALTER TABLE banco_config ADD COLUMN tla_pct_sobre_saldo numeric(7,4) NULL;          -- Caixa: 0.02
ALTER TABLE banco_config ADD COLUMN tla_pct_por_mes_remanescente numeric(7,4) NULL; -- Caixa: 0.001
ALTER TABLE banco_config ADD COLUMN observacoes_antecipacao TEXT NULL;
```

### 5.2 Configuração inicial baseada nos contratos analisados

| Banco | Modalidade | Padrão | Aceita Total | Aceita Parcial | Anuência | Aviso (dias úteis) | Mín parcial | Break flat | TLA % saldo | TLA %/mês |
|---|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| BB | FINIMP | A | ✅ | ✅ | ⚠ Implícita | **20** | **20%** | **1.0%** | — | — |
| BB | LEI_4131 | A (presumido) | ✅ | ✅ | ⚠ | (a confirmar) | (a confirmar) | (a confirmar) | — | — |
| Itaú | FINIMP | A (presumido) | ✅ | ✅ | (a confirmar) | (a confirmar) | (a confirmar) | (a confirmar) | — | — |
| Itaú | REFINIMP | A (presumido) | ✅ | ✅ | (a confirmar) | (a confirmar) | (a confirmar) | (a confirmar) | — | — |
| **Sicredi** | **FINIMP** | **B** | ⚠ Vedado/anuência | ⚠ | **✅ Obrigatória** | n/a | n/a | n/a | — | — |
| BV | FGI | C | ✅ | ✅ (parcela inteira) | ❌ | n/a | parcela cheia | n/a | — | — |
| **Caixa** | **BALCAO_CAIXA** | **D** | ✅ | ✅ | ❌ | n/a | n/a | n/a | **2.0%** | **0.1%** |
| Bradesco | FINIMP | A (presumido) | ? | ? | ? | ? | ? | ? | — | — |
| Santander | FINIMP/4131 | A (presumido) | ? | ? | ? | ? | ? | ? | — | — |

### 5.3 Nova entidade — `SIMULACAO_ANTECIPACAO`

Cada simulação é registrada (audit trail + análise histórica de decisões):

```sql
CREATE TABLE simulacao_antecipacao (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contrato_id UUID NOT NULL REFERENCES contrato(id),
    tipo_antecipacao TEXT NOT NULL,                      -- T1..T5
    data_simulacao TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    data_efetiva_proposta DATE NOT NULL,
    valor_principal_a_quitar numeric(20,6) NOT NULL,     -- moeda original
    valor_total_simulado_brl numeric(20,6) NOT NULL,
    cotacao_aplicada numeric(12,6) NULL,
    taxa_mercado_atual_aa numeric(9,6) NULL,             -- usado nos padrões C
    padrao_aplicado TEXT NOT NULL,                        -- A..E
    componentes_custo JSONB NOT NULL,                     -- detalhamento item a item
    economia_estimada_brl numeric(20,6) NULL,             -- vs cenário sem antecipar
    observacoes_banco TEXT NULL,                          -- valor C7 informado pelo banco (se aplicável)
    status TEXT NOT NULL DEFAULT 'SIMULADA',              -- SIMULADA, APROVADA, EXECUTADA, REJEITADA
    created_by TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_simulacao_contrato ON simulacao_antecipacao(contrato_id, data_simulacao DESC);
```

### 5.4 Estrutura JSONB de `componentes_custo`

Cada simulação serializa o detalhamento dos 15 componentes:

```json
{
  "padrao": "A",
  "moeda_original": "USD",
  "componentes": [
    { "codigo": "C1", "descricao": "Principal a quitar", "valor_moeda_original": 200000.000000, "valor_brl": 1060000.000000, "sinal": "+" },
    { "codigo": "C2", "descricao": "Juros pro rata até a data", "valor_moeda_original": 4900.000000, "valor_brl": 25970.000000, "sinal": "+" },
    { "codigo": "C6", "descricao": "Break funding fee 1% flat", "valor_moeda_original": 2049.000000, "valor_brl": 10859.700000, "sinal": "+" },
    { "codigo": "C7", "descricao": "Indenização adicional (informada pelo banco)", "valor_moeda_original": 800.000000, "valor_brl": 4240.000000, "sinal": "+", "fonte": "Banco - email 06/05/2026" },
    { "codigo": "C9", "descricao": "IOF câmbio (0.38% sobre conversão)", "valor_moeda_original": 0, "valor_brl": 4112.20, "sinal": "+" },
    { "codigo": "C12", "descricao": "IRRF gross-up sobre juros", "valor_moeda_original": 864.71, "valor_brl": 4582.96, "sinal": "+" },
    { "codigo": "C15", "descricao": "Restituição CDB cativo (liberação proporcional)", "valor_brl": -318000.000000, "sinal": "-" }
  ],
  "total_brl": 791764.86,
  "comparativo_sem_antecipar": {
    "saldo_total_brl_no_vencimento": 1095200.00,
    "diferenca_brl": 303435.14,
    "rentabilidade_caixa_se_aplicar_em_cdi_ate_vencimento_brl": 25400.00,
    "decisao_otima": "ANTECIPAR" 
  }
}
```

---

## 6. Endpoint de simulação

### 6.1 REST API

```
POST /api/v1/contratos/{contrato_id}/simular-antecipacao
Idempotency-Key: <uuid>
Authorization: Bearer <jwt>

{
  "tipo_antecipacao": "LIQUIDACAO_TOTAL",          // T1..T5
  "data_efetiva": "2026-06-15",                     // dia em que a antecipação ocorreria
  "valor_principal_a_quitar_moeda_original": null,  // null em T1; obrigatório em T2..T4
  "taxa_mercado_atual_aa": 4.85,                    // obrigatório se padrão = C
  "indenizacao_informada_banco_moeda_original": 800,// opcional — preencher quando banco já cotou
  "salvar_simulacao": true
}
```

**Resposta:**

```json
{
  "simulacao_id": "uuid",
  "padrao_aplicado": "A",
  "permitido": true,
  "alertas": [
    "BB exige aviso prévio mínimo de 20 dias úteis. Data efetiva proposta está a 25 dias úteis — OK."
  ],
  "componentes_custo": [ ... ],
  "totais": {
    "total_a_pagar_moeda_original": 207749.71,
    "total_a_pagar_brl": 1100892.46,
    "cotacao_aplicada": 5.30,
    "tipo_cotacao": "PTAX_D_MENOS_1"
  },
  "comparativo": {
    "saldo_total_brl_se_nao_antecipar": 1095200.00,
    "diferenca_brl": 5692.46,
    "rentabilidade_caixa_no_periodo_se_aplicar_brl": 25400.00,
    "decisao_otima_segundo_dados": "NAO_ANTECIPAR",
    "justificativa": "Custo de antecipar (R$ 1.100.892,46) supera o cenário sem antecipar somado à rentabilidade do caixa em CDB no período (R$ 1.095.200,00 + R$ 25.400,00 = R$ 1.120.600,00 disponível no vencimento se mantiver caixa aplicado)."
  }
}
```

### 6.2 Tool MCP — adicionar à lista do ADR-012

Inclui-se como **9º tool MCP read-only** no MVP:

| Tool | Função | Auth |
|---|---|---|
| `simular_antecipacao` | Simular antecipação de pagamento de um contrato (T1..T5), retorna detalhamento de custo e comparativo | leitor |

### 6.3 Skill A2A demonstrativa expandida

Incluir como skill futura (Fase 2): `simular_antecipacao_portfolio` — dado um caixa disponível, identifica quais contratos antecipar para máxima economia.

---

## 7. Casos golden para teste do motor

A suite `tests/Sgcf.GoldenDataset/` deve incluir:

### 7.1 Padrão A — BB FINIMP

```json
{
  "caso": "antecipacao-bb-finimp-padrao-a",
  "entrada": {
    "modalidade": "FINIMP",
    "banco": "BB",
    "principal_original_usd": 367315.30,
    "taxa_aa": 8.047,
    "base": 360,
    "data_desembolso": "2024-04-12",
    "data_vencimento_original": "2024-10-09",
    "data_efetiva_antecipacao": "2024-08-09",
    "tipo_antecipacao": "LIQUIDACAO_TOTAL"
  },
  "esperado": {
    "padrao_aplicado": "A",
    "C1_principal_a_quitar_usd": 367315.30,
    "C2_juros_pro_rata_usd": "calcular: principal × 8.047% × 119/360",
    "C6_break_flat_usd": "(C1 + C2) × 1.0%",
    "C7_indenizacao_usd": "informado_pelo_banco",
    "alerta_aviso_previo": "OK_se_data_efetiva ≥ data_solicitacao + 20 dias úteis"
  }
}
```

### 7.2 Padrão B — Sicredi FINIMP (regra restritiva)

```json
{
  "caso": "antecipacao-sicredi-finimp-padrao-b",
  "entrada": {
    "modalidade": "FINIMP",
    "banco": "Sicredi",
    "principal_original_usd": 210279.50,
    "taxa_aa": 8.5,
    "base": 360,
    "prazo_total_dias": 180,
    "data_efetiva_antecipacao": "60_dias_apos_desembolso",
    "tipo_antecipacao": "LIQUIDACAO_TOTAL"
  },
  "esperado": {
    "padrao_aplicado": "B",
    "alerta_critico": "Sicredi cobra juros do período TOTAL — antecipação não economiza juros",
    "C1_principal_a_quitar_usd": 210279.50,
    "C3_juros_periodo_total_usd": "principal × 8.5% × 180/360 = 8936.88",
    "TOTAL_usd": 219216.38,
    "exige_anuencia": true
  }
}
```

### 7.3 Padrão C — FGI BV (MTM)

```json
{
  "caso": "antecipacao-fgi-bv-padrao-c",
  "entrada": {
    "modalidade": "FGI",
    "banco": "BV",
    "principal_original_brl": 1000000.00,
    "taxa_contratada_aa": "100% CDI + 5.24% = ~16.24% nominal a.a.",
    "prazo_remanescente_dias": 365,
    "saldo_a_quitar_brl": 500000.00,
    "taxa_mercado_atual_aa": 14.0,
    "tipo_antecipacao": "LIQUIDACAO_PARCIAL"
  },
  "esperado": {
    "padrao_aplicado": "C",
    "valor_futuro_total_brl": "500000 × (1 + 0.1624)^(365/360)",
    "fator_desconto": "1 / (1 + 0.14)^(365/360)",
    "TOTAL_brl": "valor_futuro × fator_desconto",
    "vantagem_economica": "taxa_contratada > taxa_mercado → antecipar é vantajoso"
  }
}
```

### 7.4 Padrão D — Caixa Balcão (TLA BACEN)

```json
{
  "caso": "antecipacao-caixa-balcao-padrao-d",
  "entrada": {
    "modalidade": "BALCAO_CAIXA",
    "banco": "Caixa",
    "principal_original_brl": 5000000.00,
    "saldo_devedor_atual_brl": 3500000.00,
    "juros_pro_rata_brl": 21000.00,
    "prazo_remanescente_meses": 18,
    "tipo_antecipacao": "LIQUIDACAO_TOTAL",
    "origem_recursos": "caixa_proprio"
  },
  "esperado": {
    "padrao_aplicado": "D",
    "VTD_brl": 3521000.00,
    "TLA_opcao_A_brl": "VTD × 2% = 70420.00",
    "TLA_opcao_B_brl": "VTD × 0.1% × 18 = 63378.00",
    "TLA_aplicada_brl": 70420.00,
    "TOTAL_brl": 3591420.00,
    "isencao_aplicada": false
  }
}
```

```json
{
  "caso": "antecipacao-caixa-refinanciamento-isencao",
  "entrada": { "...mesmo do anterior...": "...", "origem_recursos": "nova_operacao_mesmo_banco" },
  "esperado": {
    "padrao_aplicado": "D",
    "TLA_aplicada_brl": 0,
    "isencao_aplicada": true,
    "motivo_isencao": "Resoluções BACEN 3401/06 e 3516/07 §3º — refinanciamento interno"
  }
}
```

---

## 8. Lacunas e tarefas para a Fase 0

| # | Lacuna | Ação | Responsável | Prazo |
|---|---|---|---|---|
| 1 | Termo BB 4131 (cláusulas de pré-pagamento do contrato emitido pelo BB London) | Solicitar cópia ao gerente BB | Tesouraria | Sprint 0 |
| 2 | Termo Itaú FINIMP CPGI 320410.69017 | Solicitar cópia ao gerente Itaú | Tesouraria | Sprint 0 |
| 3 | Termo REFINIMP Itaú | Solicitar cópia | Tesouraria | Sprint 0 |
| 4 | Contratos modelo Bradesco, Santander, Daycoval, Safra, ABC | Solicitar cópias | Tesouraria | Sprint 0-1 |
| 5 | Validar com a tesouraria a regra exata da indenização BB FINIMP (C7) — como o banco apresenta? Há histórico em emails? | Coletar 3-5 cotações reais de break funding | Tesouraria | Sprint 0 |
| 6 | Confirmar qual taxa de mercado o BV usa em FGI/PEAC para descontar (C5) — pedir histórico | Solicitar 2-3 cotações reais ao gerente BV | Tesouraria | Sprint 1 |
| 7 | Revisar com Compliance se há regulamentação BACEN/CVM adicional não capturada | Revisão jurídica | Compliance | Fase B |
| 8 | Confirmar com Caixa se o Campo 10 (TLA) está marcado em todos os contratos ativos da Proxys ou apenas em alguns | Verificar 3 contratos ativos | Tesouraria | Sprint 0 |

---

## 8.B Relação com a camada RAG (ADR-015)

Este Anexo C define a **camada estruturada** — a única autorizada a calcular valores financeiros de antecipação. A **camada RAG complementar** (ADR-015 + SPEC §8.6) cumpre papel diferente e não-conflitante:

| Pergunta | Camada que responde | Por quê |
|---|---|---|
| "Quanto custa antecipar contrato X hoje?" | **Estruturada (este Anexo)** | Cálculo determinístico com Strategy A-E, audit trail, golden tests |
| "O que a cláusula 12 do BB FINIMP diz literalmente sobre pré-pagamento?" | **RAG (pgvector)** | Recuperação textual com citação |
| "Por que o cálculo deu R$ X?" | **Híbrido** | Estruturada calcula → RAG cita a cláusula que justifica |
| "Existe cláusula de market flex no contrato Y?" | **RAG** | Busca semântica |
| "Compare a cláusula de antecipação de Sicredi vs BB" | **Híbrido** | RAG retorna trechos lado a lado; este Anexo C explica os 2 padrões (B vs A) |

**Regra inviolável:** o RAG **não calcula** — quando a pergunta tem componente numérico, o orquestrador chama o motor estruturado deste Anexo C e o RAG apenas cita a cláusula que respalda. Boundary registrada em SPEC §17.3.

**Sinergia natural:** o Agente Extrator de Cláusulas da Fase 2 do projeto (alinhado ao "Validador de Contrato" do `Plano_Agentes_FINIMP.md`) lê contratos novos, indexa em pgvector E **sugere atualizações ao `BANCO_CONFIG`** quando detectar parâmetros novos — alimentando ambas as camadas a partir de uma única extração.

---

## 9. Próximos passos no roadmap (alinhamento com `tasks/plan.md`)

| Fase | Tarefa | Descrição |
|---|---|---|
| **Fase 0** | Coleta de contratos pendentes | Itens 1-4 da seção 8 — necessários para configurar `BANCO_CONFIG` completo |
| **Fase 1** | Configuração `BANCO_CONFIG` ampliada | Adicionar campos da seção 5.1 ao schema |
| **Fase 2** | Strategy pattern do motor de cálculo | Criar `IEstrategiaAntecipacao` com 5 implementações (A, B, C, D, E) |
| **Fase 4** | Endpoint `simular-antecipacao` por modalidade | Implementar em paralelo com cada modalidade |
| **Fase 7** | Simulador de cenários ampliado | Incluir simulação de antecipação no Simulador (§7.6 do plan) |
| **Fase 7B** | Tool MCP `simular_antecipacao` | 9º tool MCP read-only |
| **Stream Q** | Golden tests dos 5 padrões | Casos da seção 7 deste anexo |

---

## 10. Resumo executivo em 1 frase

A antecipação de pagamento varia drasticamente entre bancos — desde regras MTM sofisticadas (FGI BV) até proibição total disfarçada (Sicredi cobra juros do período total) — e modelar essas regras como **5 padrões de cálculo configuráveis por banco/modalidade** transforma uma decisão "no escuro" em uma simulação determinística que aponta a opção ótima em segundos, com detalhamento item a item para negociação.

---

## Changelog

- **v1.0 — 10/maio/2026** — versão inicial, baseada em análise literal de 5 contratos modelo (BB FINIMP, Sicredi FINIMP, FGI BV, Caixa Balcão e BB 4131 outorga). 3 contratos pendentes (Itaú FINIMP, REFINIMP Itaú, Santander 4131) registrados como lacuna na seção 8.
- **v1.1 — 10/maio/2026** — adicionada seção **8.B Relação com a camada RAG** (ADR-015) explicitando que este Anexo C governa a camada estruturada (única autorizada a calcular) e que o RAG cumpre papel complementar (recuperação textual e citação literal). Mencionada sinergia com o Agente Extrator de Cláusulas da Fase 2.
