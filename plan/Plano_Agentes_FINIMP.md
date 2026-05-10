# Plano de Implementação — Sistema Multi-Agente para Operações FINIMP

**Empresa:** Proxys Comércio Eletrônico
**Stack:** Gemini Enterprise Plus + Agent Builder (GCP) + Claude Opus/Sonnet + Gemini Fast/Reasoning/Pro
**Documento:** v1.0 — maio/2026

---

## 1. Diagnóstico do processo atual

O fluxo de uma operação FINIMP envolve **10 decisões interdependentes** que hoje são feitas manualmente:

| Etapa | Hoje | Risco/Desperdício |
|---|---|---|
| Aprovação de crédito por banco | Consulta manual a cada banco | Tempo perdido em cotações inviáveis |
| Comparação de prazos disponíveis | Conhecimento tácito (BB 720d, Sicredi qualquer prazo, etc.) | Operação subótima por desconhecer alternativa |
| Cálculo de custo financeiro | Excel manual | Erros de fórmula, tempo, falta de padronização |
| Validação contrato vs proposta | Leitura linha-a-linha | **Bancos cometem erros — risco material** |
| Decisão sobre NDF (USD vs CNY) | Análise caso-a-caso | Esquecer de pedir dispensa em USD |
| Sincronização com fluxo de caixa | Planilha de tesouraria | Concentração de pagamentos no mês |
| Respeito ao teto mensal de R$ 4MM | Conferência manual | Estouro de teto compromete liquidez |
| Gestão de limite por banco | Tracking manual | Alongar prazo trava banco para novas operações |
| Negociação com gerente | Conhecimento individual | Perda de poder de barganha sem dados consolidados |
| Documentação e arquivo | Processo manual | Auditoria difícil |

**Problemas estruturais:**
- Conhecimento concentrado em 1-2 pessoas
- Decisões sob pressão de tempo (cotação expira em horas)
- Sem visão consolidada de "qual a próxima melhor operação"
- Erros de banco passam para contrato sem detecção sistemática

---

## 2. Arquitetura proposta — Sistema de Agentes Especialistas

A automação **não é um agente único** — é uma orquestra de agentes especialistas, cada um com sua função, modelo e fonte de dados.

```
┌─────────────────────────────────────────────────────────────────┐
│                  ORQUESTRADOR (Agent Builder)                   │
│              Modelo: Gemini Pro (raciocínio)                    │
│       Função: roteamento, delegação, consolidação final         │
└──────────────────────────┬──────────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────────────────┐
         │                 │                             │
┌────────▼─────────┐ ┌─────▼──────┐ ┌──────────────────▼─────┐
│ EXTRATOR         │ │ CALCULADOR │ │ VALIDADOR DE CONTRATO  │
│ Gemini Fast      │ │ Gem. Reas. │ │ Claude Sonnet          │
│ Lê PDF/email →   │ │ Aplica DRE │ │ Cota vs contrato →     │
│ JSON estruturado │ │ de FINIMP  │ │ flags de divergência   │
└──────────────────┘ └────────────┘ └────────────────────────┘

         ┌─────────────────┬─────────────────────────────┐
         │                 │                             │
┌────────▼─────────┐ ┌─────▼──────┐ ┌──────────────────▼─────┐
│ LIMITES DE       │ │ FLUXO DE   │ │ COMPLIANCE TRIBUTÁRIO  │
│ CRÉDITO          │ │ CAIXA      │ │ Claude Sonnet          │
│ Gemini Fast      │ │ Gem. Fast  │ │ IRRF/IOF/jurisdição    │
│ Saldo por banco  │ │ Calendário │ │ Valida cotação         │
└──────────────────┘ └────────────┘ └────────────────────────┘

         ┌─────────────────┐         ┌─────────────────────────┐
         │ OTIMIZADOR      │         │ RECOMENDADOR FINAL      │
         │ Gemini Reason.  │         │ Claude Opus             │
         │ Combina ofertas │         │ Decisão com nuance,     │
         │ vs teto R$ 4MM  │         │ pleitos, próximos passos│
         └─────────────────┘         └─────────────────────────┘
```

### 2.1 Detalhamento dos agentes

| # | Agente | Modelo | Input | Output | Custo/exec. |
|---|---|---|---|---|---|
| 1 | **Extrator de Propostas** | Gemini Fast | PDF/email/screenshot de cotação | JSON estruturado (taxa, prazo, garantias, comissões) | Baixo |
| 2 | **Calculador DRE** | Gemini Reasoning | JSON da proposta + premissas (CDI, %CDB) | DRE completa, custo a.a. linear/composto | Médio |
| 3 | **Validador de Contrato** | Claude Sonnet | Proposta (JSON) + contrato (PDF) | Lista de divergências numéricas e cláusulas suspeitas | Médio-alto |
| 4 | **Limites de Crédito** | Gemini Fast | Banco + valor da operação | Saldo disponível, % comprometido, projeção pós-operação | Baixo |
| 5 | **Fluxo de Caixa** | Gemini Fast | Período + valor da parcela | Datas viáveis (sem estouro do teto R$ 4MM), datas ideais (R$ 2-3MM) | Baixo |
| 6 | **Compliance Tributário** | Claude Sonnet | Cotação + jurisdição | Validação IRRF (15% vs 25%), IOF, ROF, NDF (USD vs CNY) | Médio |
| 7 | **Otimizador de Portfólio** | Gemini Reasoning | N propostas + teto mensal + limites | Combinação ótima (qual banco, qual prazo, qual data) | Médio-alto |
| 8 | **Recomendador Final** | Claude Opus | Output dos 7 anteriores | Decisão estruturada, pleitos de negociação, riscos | Alto (mas executa pouco) |

### 2.2 Por que essa divisão de modelos

**Pragmatismo de custo/qualidade:**

- **Gemini Fast**: tarefas repetitivas de extração e consulta — alta frequência, baixa complexidade. Roda 30-50× por análise sem inflar conta.
- **Gemini Reasoning/Pro**: cálculos matemáticos e otimização combinatória — onde precisa raciocínio numérico estruturado.
- **Claude Sonnet**: análises com nuance regulatória, leitura comparativa de documentos longos — onde a atenção a detalhes salva dinheiro real (validação de contrato).
- **Claude Opus**: apenas no recomendador final — uma chamada por análise, com responsabilidade de "amarrar tudo" e gerar a saída executiva. Menor frequência justifica o custo.

**Regra de bolso:** Gemini Fast é seu "soldado raso" (faz 80% do trabalho), Sonnet é seu "sargento" (valida), Opus é seu "general" (decide).

---

## 3. Fontes de dados e integrações

| Fonte | Conteúdo | Integração proposta |
|---|---|---|
| **E-mails do gerente bancário** | Propostas, cotações, contratos | Conector Gmail/Outlook (Agent Builder nativo) |
| **Drive corporativo** | PDFs de contratos arquivados, planilhas | Google Drive API (já no GCP) |
| **ERP (provável SAP/Protheus/TOTVS)** | Limites de crédito, posição de dívida | Conector via BigQuery ou API REST |
| **Planilha de Fluxo de Caixa** | Calendário de quitações | Google Sheets API com leitura programática |
| **Tabela de propostas históricas** | Base para benchmarking de taxas | BigQuery + Vertex AI feature store |
| **Banco Central / CDI** | Taxa CDI vigente, SELIC, IOF | API SCB-BCB (gratuita, oficial) |
| **Receita Federal** | Lista de paraísos fiscais (IN 1.037/2010) | Documento estático versionado |

**Camada de dados sugerida:**

```
BigQuery (data warehouse)
  ├─ propostas_recebidas       (histórico para benchmarking)
  ├─ contratos_assinados       (auditoria)
  ├─ limites_credito           (atualizado via integração ERP)
  ├─ fluxo_caixa_planejado     (sync da planilha)
  └─ operacoes_em_andamento    (saldo devedor por banco)

Vertex AI Vector Store
  └─ contratos_padrao_por_banco  (RAG para validação de cláusulas)
```

---

## 4. Roadmap em 5 fases

### **Fase 0 — Discovery e fundações** (2 semanas)

**Entregáveis:**
- Mapeamento completo de 10-15 operações históricas (golden dataset)
- Definição de schema JSON canônico para "proposta FINIMP"
- Catalogação de erros encontrados em contratos passados
- Setup de ambiente GCP (Agent Builder, BigQuery, IAM)
- Política de logs e auditoria

**Por que começar aqui:** sem dataset histórico não há como avaliar os agentes. 10-15 operações com os "gabaritos" (decisões corretas que foram tomadas) viram **eval set** para todas as fases seguintes.

---

### **Fase 1 — MVP: Extração + Cálculo (humano no loop 100%)** (3 semanas)

**Escopo:**
- Implementar Agente Extrator + Agente Calculador
- Output: análise comparativa em formato Excel (igual ao que fizemos manualmente)
- Validação 100% humana antes de qualquer ação

**Critério de sucesso:**
- 90%+ de extração correta de campos estruturados
- 99%+ de precisão nos cálculos (testado contra 10 casos do Fase 0)
- Tempo de análise reduzido de ~2h para <15min

**Por que aqui:** entrega valor imediato sem risco operacional. A analista revisa antes de decidir.

---

### **Fase 2 — Validação de Contrato e Compliance** (3 semanas)

**Escopo:**
- Agente Validador de Contrato (compara proposta vs PDF assinado)
- Agente Compliance Tributário (flagga IRRF errado, NDF esquecido)
- Output: relatório de divergências com gravidade

**Critério de sucesso:**
- Detectar 100% dos erros conhecidos do dataset histórico
- Gerar 0 falsos positivos críticos (alertas que param a operação sem motivo)

**Valor entregue:** este é o agente com **maior ROI direto** — um único contrato com erro de IRRF 25% em vez de 15% custa R$ 5k+ por operação. Se fizem 20 operações/ano, isso é R$ 100k de economia em algo que só falta atenção.

---

### **Fase 3 — Limites + Fluxo de Caixa** (3-4 semanas)

**Escopo:**
- Integração com ERP (limites de crédito por banco)
- Integração com planilha de Fluxo de Caixa
- Agente que projeta: "se fechar essa operação, qual o saldo de limite por banco e o pico de quitação no mês?"

**Crítico:** essa fase exige **engenharia de integração** (APIs, sync). Pode demorar mais se o ERP for legado.

**Critério de sucesso:**
- Projeção de fluxo com erro <2% vs realizado
- Alerta automático quando proposta + operações em curso ultrapassam R$ 4MM/mês

---

### **Fase 4 — Otimização e Orquestração End-to-End** (3 semanas)

**Escopo:**
- Agente Otimizador (combinatorial: qual banco, qual prazo, qual data)
- Agente Recomendador Final (consolida tudo em uma decisão)
- Orquestrador (Agent Builder workflow)
- Dashboard único para a analista

**Output esperado:** dada uma necessidade de R$ X em moeda Y para data Z, o sistema sugere top 3 estruturas com prós/contras, dentro das restrições de teto e limites.

---

### **Fase 5 — Governança contínua e melhoria** (ongoing)

**Atividades recorrentes:**
- Eval mensal: amostragem de 5 operações e comparação agente vs analista
- Atualização de prompts conforme novos casos
- Re-treino de extrator se taxa de erro subir
- Monitoramento de custo de inferência (alertas se passar do orçamento)
- Atualização da base de "erros conhecidos em contratos por banco"

---

## 5. Governança e Human-in-the-Loop

**Princípio:** **nenhum agente fecha operação sozinho.** Todos os agentes são "copilotos".

| Decisão | Quem decide | Papel do agente |
|---|---|---|
| Solicitar cotações aos bancos | Analista | Pode sugerir bancos a contatar |
| Aceitar uma cotação | Analista + Gerente Financeiro | Recomenda com justificativa |
| Assinar contrato | Diretoria Financeira | Valida e flagga divergências |
| Negociar com banco | Analista | Munição (dados comparativos) |
| Avisar sobre estouro de teto | — | **Bloqueia automaticamente** (alerta crítico) |
| Avisar sobre IRRF errado | — | **Bloqueia automaticamente** (alerta crítico) |

**Audit trail obrigatório:** todo prompt enviado a todo agente, todo output, todo dado consultado fica versionado em BigQuery por 5 anos.

---

## 6. Métricas de sucesso

### Métricas de eficiência (operacional)

| Métrica | Baseline atual | Meta 6 meses | Meta 12 meses |
|---|---|---|---|
| Tempo de análise por cotação | ~2h | 15 min | 5 min |
| Cotações analisadas/mês | ~5 | 15 | 25 |
| Tempo de validação de contrato | ~1h | 10 min | 5 min |
| Operações com erro de banco passando | desconhecido | 0% | 0% |

### Métricas de qualidade financeira (estratégico)

| Métrica | Por que importa |
|---|---|
| % de operações fechadas com banco "ranking 1" da análise | Mede se a análise está sendo seguida |
| Spread médio negociado vs proposta inicial | Mede valor das informações para negociação |
| Concentração de quitações por mês (R$ MM) | Manter <R$ 4MM, ideal R$ 2-3MM |
| Custo médio de FINIMP a.a. linear (líquido) | Tendência de redução |

### Métricas de custo do sistema

| Item | Estimativa |
|---|---|
| Inferência (Gemini + Claude) | R$ 1.500-3.000/mês para 25 análises |
| Agent Builder + GCP | R$ 800-1.500/mês |
| Tempo analista alocado em FINIMP | redução de 60-70% |
| **ROI esperado** | Pagamento em 4-6 meses |

---

## 7. Riscos e mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Extrator erra dado crítico (ex: taxa) | Média | Alto | Validação humana em Fase 1; alarme se taxa fora de range histórico |
| Banco muda formato de proposta | Alta | Médio | Eval contínuo; re-prompt do extrator quando taxa de erro >5% |
| Integração ERP falha | Média | Alto | Fallback para entrada manual; cache local de limites |
| Modelo muda (deprecation) | Alta | Médio | Versionar prompts; testes de regressão antes de migrar |
| Custo de inferência explode | Baixa | Médio | Orçamento mensal com alerta; downgrade Opus → Sonnet quando viável |
| Vazamento de dados sensíveis (contratos) | Baixa | Crítico | Gemini Enterprise (não treina com dados); zero-retention setting; logs auditáveis |
| Resistência de usuários | Média | Médio | Co-criação com a analista desde Fase 0; mostrar valor real (não substituir, mas potencializar) |

---

## 8. Quick wins recomendados (próximos 30 dias)

Antes mesmo de iniciar a Fase 0 formal, há 3 ações de baixo investimento e alto retorno:

1. **Padronizar o prompt de comparação** que fizemos hoje em uma "skill" reutilizável no Gemini Enterprise. A analista usa hoje sem desenvolvimento.
2. **Criar o golden dataset**: pedir 10 operações fechadas no último ano com todos os documentos. Esse dataset vale ouro para todas as fases seguintes.
3. **Mapear os "erros conhecidos" por banco**: BB costuma errar X, Itaú costuma errar Y. Esse conhecimento tácito vira regra explícita do agente Validador.

---

## 9. Decisões a tomar antes de começar

| Decisão | Quem decide | Prazo |
|---|---|---|
| Orçamento total do projeto | Diretoria Financeira | Antes da Fase 0 |
| Sponsor executivo | Diretoria | Antes da Fase 0 |
| Squad: dev, analista, infra | TI + Tesouraria | 1ª semana Fase 0 |
| Hospedagem dos contratos (compliance) | Jurídico + TI | 1ª semana Fase 0 |
| Conector ERP — quem implementa | TI | Antes da Fase 3 |
| Política de retenção de dados | Compliance | Antes da Fase 1 |

---

## Próximos passos imediatos

1. **Sponsor executivo aprova o plano** (ou pede ajustes)
2. **Forma o squad mínimo**: 1 dev/eng. de IA, 1 analista de tesouraria (você), 1 ponto de TI para integrações
3. **Inicia Fase 0** — coletar dataset histórico e setar GCP
4. **Em 8-10 semanas, Fase 1 está em produção** com ROI mensurável

---

**Resumo executivo em 1 frase:** comece pequeno (extração + cálculo), prove valor, expanda para validação e integração, mantendo humano sempre no loop em decisões financeiras críticas — investimento de 3-4 meses paga em 4-6 meses pela combinação de tempo economizado, erros detectados e operações otimizadas.
