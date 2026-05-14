# TODO — Análise das Modalidades de Contrato

Acompanha `tasks/plan_analise_modalidades.md`. Marcar `[x]` ao concluir.

---

## Phase 1 — Fundações transversais

- [ ] **T1** Escrever `plan/modalidades/00_DiasUteis_Calendario.md` (Q2)
  - **Aceite**: lista normas (BACEN Circular 3.461, ANBIMA), define `IBusinessDayCalendar` + tabela `Feriado`, regra "se vencimento cai em D não útil → próximo dia útil", impacto em `EventoCronograma.DataPrevista`, decisão pendente sobre NodaTime calendar vs API BACEN.
  - **Verifica**: tem seção 4 (GAPs) e 5 (Proposta com nomes de campos).

- [ ] **T2** Escrever `plan/modalidades/00_Cronograma_Estrutura.md` (Q1, Q3, Q6)
  - **Aceite**: explica decisão de manter `EventoCronograma` 1:N (não data única); define enum `PeriodicidadeAmortizacao { Bullet, Mensal, Trimestral, Semestral, Anual, Customizada }`; define `EstrutraAmortizacao { Bullet, Price, Sac, Customizada }`; descreve Strategy de geração; campo `DataPrimeiroVencimento` + `Periodicidade` + `QuantidadeParcelas` substitui `DataVencimento` simples no wizard.
  - **Verifica**: tem exemplo numérico de FINIMP 720d/2 parcelas semestrais e de 4131 24 meses/8 parcelas trimestrais.

- [ ] **T3** Escrever `plan/modalidades/00_TaxasPosFixadas_Indexadores.md` (Q7)
  - **Aceite**: define entidades `Indexador` (CDI, SELIC, IPCA, SOFR), `CotacaoIndexador` (série temporal diária), campos novos em `Contrato`: `TipoTaxa { Fixa, PosFixada, Hibrida }`, `IndexadorId?`, `PercentualIndexador` (ex.: 100% do CDI), `SpreadAnual`; fórmula `taxa_efetiva = percentual_indexador * cotacao_indexador + spread`; fonte de dados (BACEN SGS séries 12=CDI, 11=SELIC).
  - **Verifica**: contém exemplo "CDI + 5,24%" do FGI BV calculado.

- [ ] **T4** Escrever `plan/modalidades/00_LiberacaoGarantia.md` (Q8)
  - **Aceite**: regra "saldo de garantia exigido = percentual_exigido * saldo_devedor_atual"; descreve evento `EventoLiberacaoGarantia` (criado a cada pagamento); campo `PercentualExigido` na `Garantia`; fluxo: ao quitar parcela → recalcula saldo devedor → calcula garantia excedente → cria evento de liberação → aprovação manual ou automática conforme política do banco.
  - **Verifica**: tabela com 3 cenários (FINIMP CDB 30%, 4131 SBLC 100%, FGI Aval 80%).

- [ ] **T5** Escrever `plan/modalidades/00_Wizard_Fluxo_Cadastro.md` (Q4)
  - **Aceite**: propõe novo fluxo de 4 steps — **Step 0: Modalidade** (escolha primeiro) → Step 1: Identificação (campos condicionais à modalidade, incluindo periodicidade) → Step 2: Detalhes específicos → Step 3: Revisão; lista quais campos dependem da modalidade; descreve componente Vue3 `<WizardStepResolver :modalidade="..." />`; ponto em aberto: permitir voltar e trocar modalidade descarta dados subsequentes?
  - **Verifica**: contém wireframe ASCII de cada step.

### >>> CHECKPOINT 1 — Revisão do PO sobre os 5 transversais

- [ ] Apresentar resumo dos 5 docs ao PO
- [ ] Coletar feedback / aprovar antes de Phase 2

---

## Phase 2 — Documentos por modalidade

- [ ] **T6** Escrever `plan/modalidades/FINIMP.md` (Q1, Q3, Q8)
  - **Aceite**: explica modalidade (importação, USD/EUR/JPY/CNY, ROF, CDB cativo 30%); referencia `00_Cronograma` para periodicidade (suporta bullet OU semestrais até 720d, com exemplo BB); referencia `00_LiberacaoGarantia` para CDB cativo proporcional; lista campos `FinimpDetail` atuais e propõe complementos.
  - **Verifica**: cita `CONTRATOS_MODELOS/CONTRATO_FINIMP_BANCO_DO_BRASIL.pdf` como referência.

- [ ] **T7** Escrever `plan/modalidades/4131.md` (Q1, Q5, Q6, Q8)
  - **Aceite**: explica Lei 4.131; lista periodicidades suportadas por banco (BB semestral, Santander trimestral, outros mensal) — usa referência ao `00_Cronograma`; descreve garantia SBLC e liberação; **referencia `NDF.md` para múltiplos strikes por vencimento** (caso 4131 + hedge com strike 5,2 nos 6 primeiros meses e 5,5 no vencimento).
  - **Verifica**: cita `CONTRATOS_4131_SANTANDER/` e `CONTRATO_4131_BANCO_DO_BRASIL.pdf`.

- [ ] **T8** Escrever `plan/modalidades/FGI.md` (Q1, Q7, Q8)
  - **Aceite**: explica FGI/BNDES (cobertura % + taxa FGI); periodicidade mensal típica com data fixa por mês (usa `00_Cronograma`); taxa pós-fixada CDI+spread (usa `00_TaxasPosFixadas`); garantia FGI + Aval (usa `00_LiberacaoGarantia`); regra "parcelas inteiras, sem fracionamento" do Anexo_C.
  - **Verifica**: cita `CONTRATO_FGI_BANCO_BV.pdf` e mostra exemplo "100% CDI + 5,24%".

- [ ] **T9** Escrever `plan/modalidades/NDF.md` (Q5)
  - **Aceite**: descreve NDF Forward simples e Collar; **propõe nova entidade `StrikePorVencimento` 1:N com `InstrumentoHedge`** (campos: `DataVencimento`, `StrikeForward`/`StrikePut`/`StrikeCall`); regra "se hedge tem 1 strike → toma `StrikeForward` único; se tem N → buscar strike por data efetiva"; impacto em MTM e em Anexo_A.
  - **Verifica**: mostra exemplo numérico do PO ("5,20 nos 6 primeiros meses, 5,50 nos últimos 6").

### >>> CHECKPOINT 2 — Revisão do PO sobre modalidades

- [ ] Apresentar resumo dos 4 docs de modalidade
- [ ] Coletar feedback / aprovar antes de Phase 3

---

## Phase 3 — Fechamento

- [ ] **T10** Escrever `plan/modalidades/README.md`
  - **Aceite**: matriz "Pergunta → Doc → Resposta resumida" das 8 perguntas; links clicáveis para cada doc; visão geral do pacote.

- [ ] **T11** Patch no `SPEC.md`
  - **Aceite**: parágrafo em §[seção apropriada] referenciando o pacote `plan/modalidades/`; entrega proposta como diff para revisão antes de aplicar.

---

## Verificação Final

- [ ] `ls plan/modalidades/` retorna 10 arquivos `.md`
- [ ] `grep -li "Pergunta 1" plan/modalidades/` lista pelo menos `00_Cronograma_Estrutura.md` e modalidades afetadas
- [ ] Cada doc respeita ≤ 600 linhas (`wc -l`)
- [ ] Resumo executivo de 1 página entregue ao PO
