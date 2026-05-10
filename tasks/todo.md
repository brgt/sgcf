# Todo List — SGCF MVP (v1.1)

> Plano detalhado em `plan.md`. Marque cada item ao concluir.
> Stack: **.NET 11** (fallback .NET 10 LTS) + ASP.NET Core + EF Core + PostgreSQL + Cloud Run + GCP.
> Interfaces de consumo: REST + **MCP** (ADR-012) + **A2A baseline** (ADR-013).
> **Agentes funcionais (Comparador/Gravador/Parecerista) = Fase 2** (fora do escopo MVP).
> Cronograma: ~24 semanas (revisado em v1.1).
> Streams M (Migração), Q (Qualidade) e A (Habilitação de Agentes) rodam em paralelo a partir da Sprint 0/1.

---

## Fase B — Arquitetura técnica detalhada (4 semanas)
- [ ] **B.1a** Atualizar `Prompt_Agente_Arquiteto_SGCF.md` para v1.1 (incluir SPEC.md, .NET 11, MCP, A2A, padrões de precisão)
- [ ] **B.1b** Executar agente Arquiteto com 5 inputs (SPEC + Business Case + Anexos A/B + ADR v1.1) e produzir Documento de Arquitetura v1.0 com 18 seções (15 originais + Camada MCP + Camada A2A + Aderência ao SPEC)
- [ ] **B.2** Revisão por 6 agentes críticos → v1.1
- [ ] **B.3** Backlog inicial (30-50 user stories) priorizado
- [ ] **B.4** Travar decisões pendentes:
  - Front-end stack
  - Perfil dev
  - Reforço pontual
  - LGPD
  - UAT
  - **Versão definitiva do .NET (.NET 11 GA vs .NET 10 LTS)**
  - **SDK MCP (Anthropic vs próprio)**
  - **Versão da spec A2A**
  - **Inclusão do Agente Migrador na Fase 1** (decisão final em 9.0)
- [ ] **B.5** Spike POC MCP + A2A (2-3 dias) — validar SDK, transport, autenticação
- [ ] **🚦 Checkpoint B → Sprint 0** (gate sponsor)

## Sprint 0 — Infra e fundações (2 semanas)
- [ ] **0.1** Provisionar projetos GCP (dev/staging/prod) + IAM + redes (Terraform)
- [ ] **0.2** Cloud SQL PostgreSQL 16 (CMEK) + **pgvector extension** + Memorystore + Cloud Storage
- [ ] **0.3** Solution **.NET 11** Clean Architecture + EF Core + health checks + analyzers + métricas de qualidade no CI (complexidade, tamanho, duplicação)
- [ ] **0.4** Auth OAuth 2.0/OIDC + RBAC com 6 papéis
- [ ] **0.5** Audit trail via interceptor EF Core (campo `source` para discriminar `rest`/`mcp`/`a2a`)
- [ ] **0.6** Pipeline Cloud Build → Cloud Run + rollback
- [ ] **0.7** Observabilidade: Serilog + OpenTelemetry + dashboard + 3 alertas
- [ ] **🚦 Checkpoint Sprint 0** (Hello world autenticado em prod)

## Fase 1 — Cadastros base (1 semana)
- [ ] **1.1** `BANCO` + `BANCO_CONFIG` + seed
- [ ] **1.2** `PLANO_CONTAS_GERENCIAL` + seed Anexo A
- [ ] **1.3** `PARAMETRO_COTACAO` + função `ResolveTipoCotacao`
- [ ] **🚦 Checkpoint Fase 1**

## Fase 2 — Vertical slice "FINIMP USD bullet" (3 semanas) — TRILHO DE OURO
- [ ] **2.1** Schema `CONTRATO` + `FINIMP_DETAIL` polimórfico
- [ ] **2.2** API CRUD + idempotência + OpenAPI
- [ ] **2.3** Schema `CRONOGRAMA_PAGAMENTO` + 12 tipos de evento
- [ ] **2.4** `BulletStrategy` + golden tests (funções puras, ADR-014)
- [ ] **2.5** `CalculadorSaldo` (golden tests Anexo B 6.6)
- [ ] **2.6** Endpoint `tabela-completa` (8 blocos)
- [ ] **🚦 Checkpoint Fase 2** (cobertura motor ≥ 95%, gate crítico do sponsor)

## Fase 3 — Multi-moeda + ingestão PTAX (2 semanas)
- [ ] **3.1** `COTACAO_FX` + ingestão BCB (parciais/D0/spot intraday)
- [ ] **3.2** Cache Memorystore + fallback D-1
- [ ] **3.3** Saldo BRL com cotação configurável (3 visões)
- [ ] **🚦 Checkpoint Fase 3**

## Fase 4 — Outras modalidades + Antecipação (3,5 semanas)
- [ ] **4.1** `4131` + `LEI_4131_DETAIL` + `SacStrategy`
- [ ] **4.2** `REFINIMP` + `contrato_mae_id` + validações
- [ ] **4.3** `NCE`, `Balcão Caixa`, `FGI`
- [ ] **4.4** Motor de antecipação — 5 strategies (Anexo C):
  - PadraoA — BB FINIMP: pro rata + break flat 1% + indenização
  - PadraoB — Sicredi FINIMP: juros do período total (alerta crítico — não economiza)
  - PadraoC — FGI BV: MTM com desconto a taxa de mercado
  - PadraoD — Caixa Balcão: TLA BACEN `max(2% × VTD; 0.1% × Pzr × VTD)`
  - PadraoE — Caixa abatimento prefixado
  - Migration: 11 novos campos em `BANCO_CONFIG` + tabela `SIMULACAO_ANTECIPACAO`
  - Endpoint `POST /api/v1/contratos/{id}/simular-antecipacao` (5 tipos T1-T5)
  - Resposta com 15 componentes de custo + comparativo "sem antecipar + CDI"
  - Golden tests dos 5 padrões (Anexo C §7)
- [ ] **🚦 Checkpoint Fase 4**

## Fase 5 — Garantias polimórficas (1,5 semana)
- [ ] **5.1** `GARANTIA` master + 8 extensões
- [ ] **5.2** Validações automáticas + indicadores
- [ ] **🚦 Checkpoint Fase 5**

## Fase 6 — NDFs + Mark-to-Market (3 semanas) ⚠ MAIOR RISCO TÉCNICO
- [ ] **6.1** `INSTRUMENTO_HEDGE` + `FORWARD_SIMPLES`
- [ ] **6.2** MTM Forward (golden tests Anexo A 2.2) — funções puras
- [ ] **6.3** `COLLAR` + MTM (golden tests Anexo A 2.1)
- [ ] **6.4** Job MTM intraday (Cloud Scheduler)
- [ ] **6.5** Liquidação NDF + 5 alertas de exposição
- [ ] **🚦 Checkpoint Fase 6** (cobertura MTM ≥ 95%)

## Fase 7 — Painéis, dashboards, exportações (2 semanas)
- [ ] **7.1** Painel consolidado de dívida
- [ ] **7.2** Painel de garantias
- [ ] **7.3** Calendário de vencimentos + curva de amortização
- [ ] **7.4** Exportação PDF + Excel da tabela completa
- [ ] **7.5** Dashboard executivo (Dívida/EBITDA, Share, Custo Médio)
- [ ] **7.6** Simulador de cenários
- [ ] **🚦 Checkpoint Fase 7**

## Fase 7B — Servidor MCP + Baseline A2A (1,5 semana) — NOVA na v1.1
- [ ] **7B.1** Infra do servidor MCP (auth OAuth 2.1, transport HTTP+SSE, audit, rate limiting)
- [ ] **7B.2** 11 tools MCP read-only:
  - `list_contratos` · `get_contrato` · `get_tabela_completa`
  - `get_posicao_divida` · `get_calendario_vencimentos` · `get_cotacao_fx`
  - `get_mtm_hedge` · `simular_cenario_cambial`
  - `simular_antecipacao` (Anexo C — 5 padrões + 15 componentes de custo)
  - `buscar_clausula_contratual` (RAG — ADR-015)
  - `comparar_clausulas` (RAG — ADR-015)
- [ ] **7B.3** Agent Card A2A em `/.well-known/agent.json` + endpoint de descoberta + 1 skill demonstrativa (`consulta_posicao_divida`)
- [ ] **7B.4** Documentação onboarding agentes externos (Claude Desktop, Gemini, etc.)
- [ ] **7B.5** (Opcional) Tools MCP de escrita (`create_contrato`, `registrar_pagamento`, `cadastrar_hedge`) — decidir após 7B.4
- [ ] **7B.6** Camada RAG (ADR-015) — schema `clausula_contratual` (pgvector + hnsw), parser PDF por cláusula, cliente Gemini embeddings, tools `buscar_clausula_contratual` e `comparar_clausulas`, doc onboarding RAG
- [ ] **🚦 Checkpoint Fase 7B** (sponsor consulta dívida via Claude Desktop)

## Fase 8 — Alertas, snapshots, provisão mensal (1 semana)
- [ ] **8.1** Alertas de vencimento D-7/D-3/D-0
- [ ] **8.2** Alertas de limite de crédito por banco
- [ ] **8.3** Snapshot mensal posicional
- [ ] **8.4** Provisão mensal de juros
- [ ] **🚦 Checkpoint Fase 8**

## Fase 9 — Migração + UAT + Go-live (2 semanas)
- [ ] **9.0** Decisão final ETL puro vs Agente Migrador (relatório Stream M)
- [ ] **9.1** ETL planilha → SGCF (com Agente Migrador opcional via MCP/A2A)
- [ ] **9.2** Reconciliação automática de saldos (≥99% match)
- [ ] **9.3** Dual-run controlado (2 semanas)
- [ ] **9.4** UAT formal (30-50 cenários)
- [ ] **9.5** Documentação + 5 runbooks + treinamento
- [ ] **9.5b** Indexação batch dos 1.200 contratos no pgvector (RAG — ADR-015) — gera ~50.000+ chunks de cláusulas com embeddings
- [ ] **9.6** Go-live + aposentadoria da planilha
- [ ] **🚦 Checkpoint final — MVP em produção**

---

## Stream M — Migração e qualidade de dados (Sprints 0–8, paralelo)
- [ ] Limpeza inicial da planilha (duplicidades, normalização) — Sprint 0-2
- [ ] Preencher "A confirmar" em BANCO_CONFIG — Sprint 0-1
- [ ] Anexar contratos do Santander 4131 (Anexo B 9) — Sprint 0
- [ ] **Coletar contratos pendentes do Anexo C** (BB 4131 termo, Itaú FINIMP CPGI, REFINIMP Itaú, Bradesco/Daycoval/Safra/ABC) — Sprint 0-1
- [ ] **Coletar 3-5 emails reais com cotação de break funding fee BB FINIMP** (componente C7) — Sprint 0-1
- [ ] **Coletar 2-3 cotações reais BV de taxa de mercado para FGI** (componente C5) — Sprint 1
- [ ] Coleta de golden dataset (5-10 contratos por modalidade) — Sprint 1-3
- [ ] Mapeamento documentado planilha → schema — Sprint 2-3
- [ ] Validação de regras tributárias (IRRF Lux 15% vs 25%) — Sprint 2
- [ ] Definição final do plano de contas gerencial — Sprint 3-4
- [ ] **Relatório de qualidade da planilha** (input para decisão Agente Migrador em 9.0) — até Sprint 7

## Stream Q — Qualidade e UAT (Sprints 4–8, paralelo)
- [ ] Roteiros de UAT (30-50 cenários) — Sprint 4-5
- [ ] Casos de teste para motor de cálculo (golden tests) — Sprint 1-3
- [ ] Validação cruzada manual contra ≥20 contratos — Sprint 5-7
- [ ] Bug bash semanal — Sprint 5-8

## Stream A — Habilitação de Agentes (Sprints 1–7, paralelo, leve) — NOVA na v1.1
- [ ] Catalogar capacidades a expor via MCP/A2A (lista priorizada de tools) — Sprint 1-2
- [ ] Specs detalhadas dos 8 tools MCP read-only — Sprint 3-4
- [ ] Cenários de uso de Tesouraria via Claude Desktop (5-10 perguntas típicas) — Sprint 4-5
- [ ] Backlog Fase 2 — agentes funcionais (Comparador, Gravador, Parecerista) — Sprint 6-7

---

## Decisões pendentes (precisam de input humano)
- [ ] Stack do front-end headless — **Fase B.4**
- [ ] Perfil exato do dev — **antes Sprint 0**
- [ ] Reforço pontual de dev em picos — **conforme necessário**
- [ ] Política de retenção LGPD — **Fase B.4**
- [ ] Critérios de UAT — **Sprint 4**
- [ ] NDFs com média de PTAX (exceções) — **Fase 6**
- [ ] Layout Excel exportado (compat. SAP futuro) — **Sprint 7**
- [ ] Plano de contas final — **Fase 1**
- [ ] SLA de produção — **Sprint 7**
- [ ] Auto-post vs review-then-post (Fase 2 SAP) — **pós-MVP**
- [ ] **Versão definitiva do .NET (.NET 11 GA vs .NET 10 LTS)** — **Final Fase B**
- [ ] **SDK MCP (Anthropic oficial vs próprio)** — **Sprint 0**
- [ ] **Versão da spec A2A** — **Sprint 0**
- [ ] **Agente Migrador na Fase 1** — **Final Fase B / Fase 9.0**
- [ ] **Tools MCP de escrita no MVP (7B.5) ou Fase 2** — **Após Checkpoint Fase 7B**

---

## Definition of Done (resumo — ver plan.md seção 6)
- Princípios de codificação aplicados (ADR-014: clean code, simple > clever)
- Métricas de qualidade no CI passam (complexidade, tamanho, duplicação)
- Cobertura ≥ 80% (95% motor financeiro)
- OpenAPI + schemas MCP atualizados
- Audit log com `source` correto (`rest`/`mcp`/`a2a`)
- Funções puras em código de cálculo financeiro
