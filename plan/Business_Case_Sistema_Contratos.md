# Business Case — Sistema de Gestão de Contratos de Financiamento

**Empresa:** Proxys Comércio Eletrônico
**Patrocinador:** Diretoria Financeira / Tesouraria
**Autor:** Welysson Soares (Tesouraria)
**Versão:** v1.0 — Documento inicial para discussão
**Data:** maio/2026

---

## 1. Sumário Executivo

A Proxys gerencia hoje **mais de 1.200 contratos** de financiamento (4131, FINIMP, NCE, CCB) com **10 instituições financeiras** através de uma única planilha Excel. Esse modelo, que serviu bem nos primeiros anos, **chegou ao limite operacional**: a empresa não consegue cruzar a posição de dívida com a DRE/Balanço com a velocidade e precisão que o porte atual exige.

Este documento propõe a construção de um **Sistema de Gestão de Contratos de Financiamento (SGCF)** próprio que substitua a planilha como fonte única de verdade da dívida, integre-se à DRE/Balanço, automatize cálculos de juros e tributos, e ofereça governança auditável.

**Investimento estimado:** R$ 180k–280k (build) + R$ 4k–7k/mês (operação)
**Payback estimado:** 8–14 meses
**Principais ganhos:** redução de 70%+ no tempo de fechamento mensal, eliminação de erros de conciliação, capacidade de simulação de cenários de dívida em tempo real

---

## 2. Contexto e situação atual

### 2.1 Como funciona hoje

O controle da dívida vive em uma planilha Excel composta por duas abas operacionais:

| Aba | Função | Volume |
|---|---|---|
| **DÍVIDA_2026** | Painel mensal de saldo devedor, pagamentos, novas dívidas e indicadores (Dívida/EBITDA, Share por banco) | 12 meses × 10 bancos × múltiplos blocos de dados |
| **RESUMO_ENDIVIDAMENTO** | Log mestre de contratos: cronograma, valores em USD/BRL, PTAX, NDF, IRRF, garantias, status | **1.200+ registros históricos** |

**Modalidades cobertas:** 4131, FINIMP, NCE, CCB
**Bancos:** Itaú, Bradesco, Santander, ABC, BV, Safra, Banco do Brasil, Sicredi, Caixa, Daycoval

### 2.2 Pontos de dor (sintomas observados)

| # | Sintoma | Impacto operacional |
|---|---|---|
| 1 | **Conciliação com DRE/Balanço é manual e demorada** | Atrasos no fechamento mensal; risco de divergência entre tesouraria e contabilidade |
| 2 | **Cálculo de juros, IRRF, IOF e NDF é em fórmula** | Erros silenciosos em fórmulas; difícil rastrear quem mudou o quê |
| 3 | **Sem audit trail nativo** | Auditoria interna/externa precisa reconstruir histórico; risco de compliance |
| 4 | **Limite de crédito por banco em conferência manual** | Risco de comprometer limite sem perceber, perdendo capacidade de novas operações |
| 5 | **Concentração de conhecimento em 1-2 pessoas** | Risco operacional crítico se essas pessoas saírem ou ficarem indisponíveis |
| 6 | **Não permite simulação de cenários** | Decisões de novas operações sem visão "e se" do impacto na Dívida/EBITDA |
| 7 | **Versionamento via cópia de arquivo** | "DÍVIDA_2026_v3_FINAL_REAL.xlsx" — risco de trabalhar em versão errada |
| 8 | **Sem alertas automáticos de vencimento** | Risco de atraso de pagamento por esquecimento |
| 9 | **Indicadores (Dívida/EBITDA, Share) calculados manualmente** | Dashboards executivos defasados em relação à realidade |
| 10 | **Integração com bancos é zero** | Saldos consultados manualmente, internet banking a internet banking |

### 2.3 Por que agora?

Três fatores tornam o problema urgente:

1. **Volume crescente:** 1.200+ contratos é o ponto onde planilha vira passivo, não ativo.
2. **Complexidade tributária:** operações em moeda estrangeira (4131 e FINIMP) com IRRF gross-up, IOF câmbio, NDF e PTAX exigem cálculo determinístico — fórmulas de Excel não escalam.
3. **Pressão de governança:** crescimento da empresa demanda relatórios executivos e capacidade de resposta a auditoria/bancos/investidores em horas, não dias.

---

## 3. Visão da solução

### 3.1 O que é o SGCF

Um **sistema web** (SaaS interno ou produto comprado e customizado) que centraliza:

- **Cadastro mestre** de cada contrato com todos os atributos contratuais
- **Motor de cálculo** que gera cronograma de pagamentos, juros, IRRF, IOF, NDF automaticamente
- **Painel de saldo** por banco, modalidade, prazo, moeda — em tempo real
- **Conciliação automática** com lançamentos contábeis (DRE/Balanço)
- **Indicadores executivos** (Dívida/EBITDA, Dívida Líquida, Share, Custo Médio) atualizados online
- **Alertas e workflow** (vencimentos, limite de crédito, divergências)
- **Audit trail completo** (quem mudou o quê, quando, com qual justificativa)
- **Capacidade de simulação** ("se eu fechar mais R$ 5MM em FINIMP, qual fica meu Dívida/EBITDA?")

### 3.2 Princípios de desenho

1. **Fonte única de verdade** — toda dívida vive no SGCF. Excel deixa de ser fonte e vira destino (export).
2. **Integração first-class com contabilidade** — cada lançamento financeiro tem rastreabilidade direta para a conta contábil correspondente.
3. **Cálculo determinístico** — fórmulas em código versionado (não em planilha), com testes automatizados.
4. **Auditável por design** — cada mudança gera registro; cada relatório é reproduzível.
5. **Acessível à tesouraria, não só à TI** — tela amigável, sem necessidade de SQL.

---

## 4. Escopo do sistema

### 4.1 Funcionalidades core (MVP)

| Bloco | Funcionalidades essenciais |
|---|---|
| **Cadastro de Contratos** | CRUD de contratos com todos os atributos (banco, modalidade, valor, taxa, garantias, datas, status). Upload do PDF do contrato anexo. |
| **Motor de Cronograma** | Geração automática do cronograma de pagamentos a partir dos parâmetros do contrato. Suporte a bullet, parcelas iguais, parcelas customizadas. |
| **Cálculos Tributários** | IRRF gross-up por jurisdição (15%, 25%), IOF câmbio (0,38%), tabela regressiva IRRF aplicações financeiras. |
| **Conversão Cambial** | Aplicação de PTAX D-1 ou cotação informada; histórico de PTAX por data. |
| **NDF e Swap** | Vinculação de derivativos a contratos; cálculo de ajustes. |
| **Painel de Saldo Devedor** | Por banco, modalidade, prazo (CP/LP), moeda, vencimento. Filtros e drill-down. |
| **Limites de Crédito** | Cadastro de limite total e disponível por banco; alerta automático ao se aproximar do teto. |
| **Indicadores** | Dívida Total, Dívida Líquida, Dívida/EBITDA, Share por banco, Custo Médio Ponderado, Prazo Médio. |
| **Alertas e Notificações** | E-mail/Slack para vencimentos próximos (D-7, D-3, D-0), estouro de limite, divergências. |
| **Export para Excel/PDF** | Relatórios padronizados (executivo, auditoria, banco). Mantém compatibilidade com modelo atual durante transição. |

### 4.2 Funcionalidades avançadas (pós-MVP)

| Bloco | Funcionalidades |
|---|---|
| **Integração SAP Business One** | API REST (Service Layer) substituindo a exportação manual de Excel do MVP. Criação automática de journal entries, sincronização de plano de contas. |
| **Conciliação Contábil automática** | Cruzamento automático entre lançamentos do SGCF e extrato contábil do SAP. Identifica divergências sem intervenção manual. |
| **Simulador de Cenários** | "E se" — adicionar/remover/refinanciar contratos e ver impacto em Dívida/EBITDA, fluxo de caixa, exposição cambial. |
| **OCR + IA para extração** | Upload do PDF do contrato → preenchimento automático dos campos do cadastro (precisão validada por humano). |
| **Histórico de Cotações** | Comparação de propostas históricas para benchmark de novas cotações (apoia negociação com gerente). |
| **Workflow de Aprovação** | Fluxo de aprovação de novos contratos (Tesouraria → Gerente Financeiro → Diretoria conforme alçada). |
| **Integração com bancos (Open Finance)** | Saldo devedor sincronizado direto do banco — sem digitação manual. |
| **Dashboard executivo (BI)** | Power BI / Looker Studio com visualizações para diretoria. |

> **Decisão estratégica do MVP**: a primeira versão será **standalone**, sem integração automatizada com o SAP Business One. A contabilidade receberá uma **planilha Excel padronizada** (compatível com Import from Excel do SAP B1) gerada pelo SGCF para importação manual. Isso reduz dependência de TI/SAP, acelera entrega e permite validar valor antes de investir em integração. Detalhes na seção 8.5 do Anexo A.

### 4.3 Fora de escopo (não vai ser o SGCF)

- **Não substitui o ERP** — é um sistema **especialista** em dívida que **conversa** com o ERP
- **Não substitui o sistema contábil** — gera os lançamentos sugeridos, contabilidade decide
- **Não é homebanking** — não inicia pagamentos, apenas controla
- **Não é sistema de aprovação de crédito interno** — controla o que já foi aprovado

---

## 5. Stakeholders

| Stakeholder | Papel | Interesse |
|---|---|---|
| **Tesouraria** (você) | Owner do sistema, principal usuário | Eficiência, controle, redução de erros, qualidade de vida |
| **Gerente Financeiro** | Aprovador de operações | Visibilidade gerencial, ferramenta de decisão |
| **Diretoria Financeira** | Sponsor executivo, revisor de indicadores | Dívida/EBITDA, exposição, custo médio |
| **Contabilidade** | Conciliação contábil | Reduzir retrabalho de fechamento mensal |
| **Auditoria interna** | Compliance | Audit trail, rastreabilidade |
| **Auditoria externa** | Validação | Reproducibilidade dos números |
| **TI** | Infraestrutura, integrações, segurança | Atender SLA, manter sistema vivo |
| **Bancos** | Origem dos dados | (passivos no projeto, mas afetados via integração futura) |

---

## 6. Benefícios esperados

### 6.1 Benefícios quantificáveis

| Benefício | Métrica atual (estimada) | Meta com SGCF | Impacto financeiro |
|---|---|---|---|
| **Tempo de fechamento mensal de dívida** | 3-4 dias | 0,5-1 dia | Liberação de ~30h/mês da analista de tesouraria |
| **Tempo de geração de relatório executivo** | 4-8 horas | <30 min | ~6h/mês da liderança |
| **Erros de conciliação detectados após fechamento** | 2-5 por mês | <1 por trimestre | Redução de retrabalho contábil |
| **Tempo de resposta a pedido de auditoria** | 2-5 dias | <1 hora | Reduz risco de não conformidade |
| **Capacidade de simular cenário (planejamento)** | Não existe | Em segundos | Decisões melhores em novas operações |
| **Detecção de oportunidade de troca de dívida cara** | Não sistemática | Alerta automático | Potencial de R$ 50-150k/ano em economia financeira |
| **Risco de pagamento atrasado** | Existe (manual) | ~zero (alertas) | Evita multa + relacionamento bancário |

### 6.2 Benefícios qualitativos

- **Continuidade operacional** — conhecimento codificado no sistema, não em pessoas
- **Profissionalização da função tesouraria** — sai do "Excel artesanal" para ferramenta de gestão
- **Confiabilidade dos números** — diretoria, auditoria e bancos veem a mesma fonte
- **Capacidade de crescer** — sistema escala com o negócio (de 1.200 para 5.000 contratos sem dor)
- **Ativo para due diligence** — em rounds de captação ou M&A, dívida controlada em sistema é diferencial
- **Suporte a compliance regulatório** — Lei das S.A., regras CVM (se aplicável), IFRS

### 6.3 Benefícios estratégicos

| Benefício | Descrição |
|---|---|
| **Visibilidade em tempo real** | Diretoria deixa de "esperar o relatório" — consulta a posição quando precisa |
| **Plataforma para próximos projetos** | Base para automações com agentes de IA (vide projeto paralelo de comparação de cotações FINIMP) |
| **Ativo de dados** | 1.200+ contratos viram dataset para benchmarking, machine learning, predição |
| **Diferencial competitivo** | Empresa do porte da Proxys com tesouraria sistematizada é minoria |

---

## 7. Custo da inação

Manter a planilha tem custo escondido que tende a crescer. Cenários:

| Cenário negativo | Probabilidade em 24 meses | Impacto |
|---|---|---|
| **Erro em fórmula de planilha gera divergência material com balanço** | Alta | Retrabalho contábil, possível republicação de demonstrativos |
| **Pessoa-chave da tesouraria sai e ninguém entende a planilha** | Média | Paralisação parcial; tempo de recomposição: 2-4 meses |
| **Auditor externo aponta deficiência de controle interno** | Alta | Risco de qualificação no parecer; impacto em relacionamento bancário |
| **Esquecimento de vencimento gera pagamento atrasado** | Média | Multa + juros + arranhão no relacionamento + risco de aceleração de outras dívidas |
| **Limite estourado em banco** | Média | Operação importante negada por falta de espaço |
| **Crescimento atinge 2.500+ contratos sem sistema** | Alta | Planilha trava (literalmente — Excel tem limites); virada de chave emergencial |

**Estimativa de custo da inação em 24 meses:** R$ 200k–500k (compostos por retrabalho, multas, oportunidades perdidas, recomposição de equipe).

---

## 8. Investimento estimado

### 8.1 Opções de implementação

| Opção | Como | Investimento (build) | Operação mensal | Prós | Contras |
|---|---|---|---|---|---|
| **A. Software de mercado** | Compra de SaaS especializado em gestão de dívida (ex: Treasury Suite, Kyriba, BMS) | R$ 80-150k setup | R$ 15-40k/mês | Maturidade, suporte | Caro, configuração engessada, customização limitada |
| **B. Build interno** | Desenvolvimento interno (squad próprio ou consultoria) | R$ 200-350k | R$ 3-6k/mês infra | Customizado, propriedade total | Tempo de build, dependência da equipe |
| **C. Build com low-code** | Plataformas como Mendix/Outsystems/Appian + integrações | R$ 150-250k | R$ 8-15k/mês licença | Tempo de build menor, baixa manutenção | Lock-in da plataforma, custo de licença escalável |
| **D. Híbrida (recomendada)** | Frontend low-code + motor de cálculo em código próprio + BigQuery | R$ 180-280k | R$ 4-7k/mês | Balanço entre velocidade e flexibilidade | Exige arquiteto técnico bom |

### 8.2 Detalhamento da opção recomendada (D) — MVP standalone

| Componente | Custo build | Custo mensal |
|---|---|---|
| **Frontend** (React/Next.js ou low-code) | R$ 60-90k | R$ 500-1k (hosting) |
| **Motor de cálculo** (Python/Node, código próprio com testes) | R$ 50-80k | R$ 500-1k (compute) |
| **Banco de dados** (PostgreSQL/BigQuery) | R$ 10-20k (modelagem) | R$ 1-2k |
| **Módulo de exportação Excel** (bridge para SAP) | R$ 8-15k | — |
| **Integrações leves** (e-mail, Slack, BCB API para PTAX) | R$ 12-20k | R$ 300-500 |
| **Migração de dados** (1.200+ contratos da planilha) | R$ 20-30k | — |
| **UX/UI design** | R$ 10-20k | — |
| **Gerenciamento de projeto** | R$ 10-20k | — |
| **Contingência (15%)** | R$ 27-42k | — |
| **TOTAL MVP (Fase 1)** | **R$ 207-337k** | **R$ 2,3-4,5k** |

**Reserva para Fase 2** (integração SAP B1 via Service Layer): R$ 30-50k de build + R$ 500-1k/mês de operação adicional. **Não compõe o orçamento do MVP**.

### 8.3 Custos recorrentes adicionais

- **Suporte técnico:** R$ 1-2k/mês (sustentação leve, cobertura de horas reativas)
- **Evoluções funcionais:** R$ 2-4k/mês (orçamento de melhorias contínuas)
- **Treinamento e documentação:** R$ 5k no Ano 1, depois R$ 1-2k/ano

---

## 9. ROI e Payback

### 9.1 Cálculo de payback

**Investimento total Ano 1:** R$ 280k (build) + R$ 60k (operação 12 meses) = **R$ 340k**

**Benefícios anuais quantificados:**

| Benefício | Valor anual estimado |
|---|---|
| Liberação de tempo da analista (30h/mês × R$ 80/h × 12) | R$ 28,8k |
| Redução de retrabalho contábil (5h/mês × R$ 100/h × 12) | R$ 6k |
| Otimização de operações (1 troca de dívida cara/ano evitada × R$ 100k) | R$ 100k |
| Eliminação de risco de multa por atraso (1 evento × R$ 30k) | R$ 30k |
| Capacidade de novas operações por gestão de limite (margem ganha) | R$ 50k+ |
| **Total benefícios diretos/ano** | **~R$ 215k** |

**Payback:** R$ 340k / R$ 215k = **~19 meses no Ano 1**, descendo para **~9 meses do Ano 2 em diante** (sem CapEx novo).

**TIR estimada em 5 anos:** 35-50% a.a. — atrativo mesmo em cenário conservador.

### 9.2 Benefícios não monetizados

Os benefícios qualitativos (governança, continuidade operacional, ativo para due diligence) não entram no cálculo mas têm peso real em decisão estratégica.

---

## 10. Roadmap proposto (12 meses)

### Mês 1-2: **Discovery e Desenho**
- Levantamento detalhado de requisitos com tesouraria, contabilidade, diretoria
- Modelagem do banco de dados (schema canônico de "contrato de financiamento")
- Definição de integrações (quais sistemas conectar e com qual prioridade)
- Escolha definitiva de tecnologia
- **Entregável:** Documento de Arquitetura + Backlog priorizado

### Mês 3-5: **MVP — Cadastro + Motor de Cálculo**
- Cadastro de contratos
- Motor de cronograma e cálculos
- Migração dos 1.200+ contratos históricos
- Painel básico de saldo por banco
- **Entregável:** Sistema operando em paralelo com a planilha (dual run)

### Mês 6-7: **Indicadores + Alertas + Exportação para SAP**
- Painel de Dívida/EBITDA, Dívida Líquida, Share
- Sistema de alertas (vencimentos, limites)
- Relatórios padronizados
- **Geração de planilha Excel padronizada para importação manual no SAP B1** (bridge manual)
- Cadastro do plano de contas e centros de custo da Proxys (espelho do SAP)
- **Entregável:** Tesouraria começa a usar SGCF como fonte primária; contabilidade importa lançamentos via Excel

### Mês 8-9: **Funcionalidades avançadas — parte 1**
- Simulador de cenários ("e se" cambial, "e se" novas operações)
- Workflow de aprovação de novos contratos
- Conciliação manual assistida (paste de extrato do SAP → SGCF aponta divergências)
- **Entregável:** Sistema maduro para uso operacional sem dependência de planilha

### Mês 10-11: **Funcionalidades avançadas — parte 2**
- OCR + IA para extração de PDFs de contratos
- Histórico de cotações para benchmarking de propostas
- Refinamento de UX baseado em uso real
- **Entregável:** Operação "0 planilha" para tesouraria

### Mês 12: **Aposentadoria da planilha + preparação Fase 2**
- Migração completa
- Treinamento estendido
- Documentação final
- **Levantamento técnico para integração SAP B1 via Service Layer** (Fase 2)
- **Entregável:** Planilha aposentada; Business Case da Fase 2 (integração nativa SAP) pronto para aprovação

---

## 11. Riscos e mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| **Migração de 1.200+ contratos com dados sujos/inconsistentes** | Alta | Alto | Limpeza prévia em paralelo ao build; validação cruzada com saldos contábeis |
| **Resistência à mudança da equipe atual** | Média | Médio | Tesouraria como co-criadora; transição gradual (dual run) |
| **Estouro de orçamento por escopo crescente** | Alta | Alto | Backlog priorizado rígido; novas demandas viram Fase 2 |
| **Atraso por dependência de TI/integrações** | Média | Médio | Mocks no início; integrações reais como milestone separado |
| **Regulatório/compliance descobre algo crítico no meio do projeto** | Baixa | Alto | Envolver Compliance desde o Mês 1 |
| **Pessoas-chave saem durante o projeto** | Baixa | Médio | Documentação contínua; conhecimento distribuído |
| **Sistema fica "pesado" e perde adoção** | Média | Alto | UX é prioridade desde o Mês 1; testes com usuário real semanais |

---

## 12. Decisões para a Diretoria

Para destravar o projeto, são necessárias 5 decisões:

1. **Aprovação do conceito** — vamos construir o SGCF? (Sim/Não)
2. **Definição de orçamento** — R$ 280k–340k Ano 1 está aprovado?
3. **Escolha da abordagem de implementação** — Opção A (compra), B (build), C (low-code), ou D (híbrida)?
4. **Sponsor executivo nomeado** — quem é o "dono" do projeto na Diretoria?
5. **Disponibilidade da Tesouraria** — pode alocar 30-40% do tempo da analista durante o build?

---

## 13. Próximos passos imediatos (próximas 2 semanas)

1. **Apresentar este Business Case à Diretoria Financeira** para discussão e aprovação preliminar
2. **Coletar feedback e ajustar premissas** (orçamento, escopo, prazo)
3. **Decidir abordagem técnica** (consulta a TI sobre opções A/B/C/D)
4. **Definir squad inicial** e disponibilidade
5. **Iniciar Mês 1 (Discovery)** se aprovado

---

## Anexos sugeridos para versões futuras

- **A1.** Arquitetura técnica detalhada
- **A2.** Schema do banco de dados (modelo de "contrato")
- **A3.** Mapeamento de integrações com sistemas existentes
- **A4.** Plano de migração de dados (1.200+ registros)
- **A5.** Plano de testes e qualidade
- **A6.** Política de backup e disaster recovery
- **A7.** Comparativo detalhado entre fornecedores de SaaS (se Opção A escolhida)

---

**Resumo em 1 frase:** A planilha de dívida atingiu seu limite e está virando passivo da empresa — investir R$ 280-340k em um sistema próprio paga em ~9 meses (regime), elimina risco operacional crítico, e prepara a tesouraria para escalar sem perder controle.
