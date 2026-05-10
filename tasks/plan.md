# Plano de Implementação — SGCF (Sistema de Gestão de Contratos de Financiamento)

**Empresa:** Proxys Comércio Eletrônico
**Sponsor:** Welysson Soares (Tesouraria/Controladoria)
**Squad:** 1 dev sênior + 2 controladoria (PO/analistas)
**Stack:** **.NET 11** (com fallback .NET 10 LTS — ver ADR-003 nota v1.1) + ASP.NET Core + EF Core + PostgreSQL (Cloud SQL) + Cloud Run + GCP `southamerica-east1`
**Interfaces de consumo:** REST API + **Servidor MCP** (ADR-012) + **baseline A2A** (ADR-013)
**Cronograma alvo:** ~24 semanas (6 meses) para MVP em produção — revisado em v1.1 com inclusão de MCP/A2A
**Versão do plano:** v1.2 — 08/maio/2026 (inclusão do SPEC.md como documento âncora)
**Documentos-base (em ordem de precedência):**
1. **`SPEC.md`** (raiz) — **documento âncora** com padrões operacionais (precisão decimal, timezone, LGPD, RBAC, convenções API/MCP/A2A, SLAs, boundaries)
2. `Business_Case_Sistema_Contratos.md` — visão de negócio
3. `Anexo_A_Valoracao_Divida_NDF.md` — NDFs e MTM
4. `Anexo_B_Modalidades_e_Modelo_Dados.md` — modalidades e dados
5. `Anexo_C_Regras_Antecipacao_Pagamento.md` — **5 padrões de antecipação por banco/modalidade**
6. `ADR_Decisoes_Estrategicas.md` (v1.1) — decisões arquiteturais

> Em caso de conflito entre documentos, vale o **SPEC.md** (mais recente e mais detalhado em padrões operacionais), seguido do ADR v1.1 (decisões estratégicas), depois Anexos e Business Case.

**Changelog v1.1:**
- Stack atualizada para .NET 11 (com fallback documentado para .NET 10 LTS)
- Nova seção 1.5 — Estratégia de Agentes, MCP e A2A
- Nova **Fase 7B** — Servidor MCP + Baseline A2A (~1,5 semana)
- Cronograma revisado: 22 → ~24 semanas
- Princípios de codificação (clean code, simple > clever) adicionados ao Definition of Done
- Stream A — Habilitação de Agentes (paralelo, leve) introduzido
- Tarefa 9.0 — Avaliação Agente Migrador na Fase 1 (decisão na Fase B)

---

## 1. Visão geral

### 1.1 Objetivo do MVP

Substituir a planilha Excel de 1.200+ contratos por um sistema **API-first** que:
- Cadastra contratos polimórficos (FINIMP, REFINIMP, 4131, NCE, Balcão Caixa, FGI) em multi-moeda (USD, EUR, JPY, CNY, BRL).
- Gera cronogramas de pagamento e calcula saldo devedor em tempo real (principal + juros provisionados + comissões).
- Modela NDFs (Forward simples + Collar) com Mark-to-Market intraday.
- Suporta 8 tipos de garantia (CDB cativo, SBLC, recebíveis cartão, boleto, duplicatas, FGI, aval, alienação fiduciária).
- Mantém plano de contas gerencial próprio com provisão mensal de juros (sem integração SAP nesta fase — ADR-008).
- Entrega audit trail completo, RBAC, painéis consolidados (dívida, garantias, vencimentos) e exportação PDF/Excel da tabela completa do contrato.

### 1.2 Decisões arquiteturais já fixas (ver ADR)

| Item | Decisão | Origem |
|---|---|---|
| Stack | **.NET 11** (fallback .NET 10 LTS) + EF Core + PostgreSQL 16 | ADR-003 (v1.1) |
| Hospedagem | GCP, `southamerica-east1`, Cloud Run | ADR-005 |
| Arquitetura | API-first headless, REST + OpenAPI 3.1 | ADR-004 |
| Segurança | Padrão de mercado, OAuth 2.0/OIDC, CMEK, audit 5 anos | ADR-006 |
| DevOps | 100% interno, Cloud Build CI/CD | ADR-007 |
| Plano de contas | Gerencial próprio; campo `codigo_sap_b1` nullable | ADR-008 |
| **Agentes Fase 1** | Backend agent-ready; **sem agentes de negócio** (exceto avaliar Migrador) | ADR-011 |
| **Servidor MCP** | SGCF expõe MCP read-only no MVP; tools de escrita opcionais | ADR-012 |
| **A2A baseline** | Agent Card publicado + endpoint de descoberta no MVP | ADR-013 |
| **Princípios de código** | Clean Code, simple > clever, DDD tático, YAGNI agressivo | ADR-014 |

### 1.3 Princípios de planejamento aplicados

1. **Vertical slicing**: cada fase entrega valor end-to-end (cadastro + cálculo + API + exportação), não camadas horizontais isoladas.
2. **Modalidade-piloto primeiro**: FINIMP USD com cronograma bullet é o "trilho de ouro" — depois que ele funciona ponta a ponta, as demais modalidades viram extensões incrementais.
3. **Risco alto antes**: NDF MTM é o componente tecnicamente mais difícil — fica em fase intermediária (Sprints 5-6), depois das fundações estarem estáveis e antes de painéis dependerem dele.
4. **Migração paralela ao build**: as 2 controladoria começam limpeza/normalização do golden dataset desde a Sprint 0, sem bloquear o dev.
5. **Checkpoints duros**: a cada 2-3 tarefas há gate com critério objetivo (testes verdes + demo + revisão do sponsor).

### 1.4 Mapa de dependências

```
Fase B (Arquitetura)
    │
    ▼
Sprint 0 (Infra GCP + .NET skeleton + Auth + Audit + CI/CD)
    │
    ▼
Fase 1 (Cadastros base: BANCO, BANCO_CONFIG, PARAMETRO_COTACAO, PLANO_CONTAS)
    │
    ▼
Fase 2 (Vertical slice FINIMP USD: contrato + cronograma bullet + saldo + tabela completa)
    │
    ├─────────────────────────┬─────────────────────────┐
    ▼                         ▼                         ▼
Fase 3                    Fase 4                    Fase 5
(Multi-moeda + PTAX)  (Outras modalidades)      (Garantias polimórficas)
    │                         │                         │
    └────────────┬────────────┴────────────┬────────────┘
                 ▼                         ▼
              Fase 6 (NDFs + MTM)
                 │
                 ▼
              Fase 7 (Painéis + Dashboards + Exportações)
                 │
                 ▼
              Fase 8 (Alertas + Snapshots + Provisão mensal)
                 │
                 ▼
              Fase 9 (Migração 1.200+ + UAT + Go-live)
```

Em paralelo (controladoria, sem bloquear dev):
- **Stream M (Migração)**: limpeza da planilha, golden dataset, preenchimento de "A confirmar" em BANCO_CONFIG.
- **Stream Q (Qualidade)**: levantamento de casos de teste, validação contra realidade, definição de UAT.
- **Stream A (Habilitação de Agentes — leve)**: catalogar capacidades do SGCF a expor via MCP/A2A, escrever specs de tools, preparar terreno para Fase 2 sem competir com o build core.

### 1.5 Estratégia de Agentes, MCP e A2A

Esta seção consolida ADR-011, ADR-012 e ADR-013.

#### 1.5.1 Posicionamento da Fase 1 — backend "agent-ready"

A Fase 1 entrega um **sistema de registro determinístico** com **APIs prontas para serem consumidas por agentes na Fase 2** — mas **sem implementar agentes funcionais de negócio** no caminho crítico do MVP. Princípio: a confiabilidade do core financeiro **nunca depende de LLM**.

```
┌─────────────────────────────────────────────────────────┐
│              ECOSSISTEMA PROXYS (visão alvo)            │
│                                                         │
│  FASE 2 — Agentes funcionais (consumidores)             │
│  ┌──────────┐ ┌──────────┐ ┌──────────────┐            │
│  │Comparador│ │ Gravador │ │ Parecerista  │            │
│  │ Cotações │ │ Cotações │ │ (Recomendador│  ...       │
│  │(Gem.Reas)│ │(Gem.Fast)│ │   - Opus)    │            │
│  └─────┬────┘ └────┬─────┘ └──────┬───────┘            │
│        │           │              │                    │
│        │           │              │   A2A              │
│        └───────────┴──────────────┘                    │
│                    │                                   │
│             ┌──────┴────────┐                          │
│             │ Orquestrador  │  (Agent Builder ou       │
│             │  (A2A)        │   .NET próprio)          │
│             └──────┬────────┘                          │
│                    │   MCP                              │
│  ──────────────────┼────────────────────────────────── │
│                    │                                   │
│  FASE 1 — Backend SGCF (sistema de registro)           │
│  ┌─────────────────▼──────────────────┐                │
│  │   SGCF — APIs REST + Servidor MCP  │                │
│  │   + Agent Card A2A                 │                │
│  │   ─────────────────────────────    │                │
│  │   Domínio determinístico .NET 11   │                │
│  │   PostgreSQL · Cloud Run · GCP     │                │
│  └────────────────────────────────────┘                │
└─────────────────────────────────────────────────────────┘
```

#### 1.5.2 O que o MVP entrega na camada de agentes

| Componente | Status no MVP | Tarefa |
|---|---|---|
| **Servidor MCP read-only** com 8 tools básicos | ✅ Entrega no MVP | Fase 7B |
| **Tools MCP de escrita** (`create_contrato`, `registrar_pagamento`) | ⚠ Opcional — decidir após estabilização do MCP read-only | Fase 7B |
| **Agent Card A2A** publicado em `/.well-known/agent.json` | ✅ Entrega no MVP | Fase 7B |
| **1 skill A2A demonstrativa** (consulta posição) | ✅ Entrega no MVP | Fase 7B |
| **Agente Migrador** (LLM extraindo planilha) | ⚠ Decisão na Fase B; default = não, ETL determinístico basta | Fase 9.0 |
| **Agentes funcionais** (Comparador, Gravador, Parecerista) | ❌ Fora do escopo MVP — Fase 2 | — |

#### 1.5.3 Princípios de design para a camada agent-ready

1. **MCP e REST API compartilham os mesmos handlers** — tools MCP são adaptadores finos sobre Application Services existentes. Lógica de negócio nunca duplicada.
2. **Toda chamada via MCP/A2A vai para o mesmo `AUDIT_LOG`** com campo `source` discriminando origem (`rest`, `mcp`, `a2a`).
3. **Schemas dos tools MCP versionados** e disponíveis em `/mcp/v1/tools` para descoberta.
4. **Authn/Authz unificada** — o token OAuth do usuário é o mesmo para REST/MCP/A2A; agentes têm service accounts dedicadas com escopo limitado.
5. **Rate limiting separado** para MCP/A2A (agentes podem pedir muitos tools em sequência — proteger o backend).
6. **Idempotência obrigatória** em qualquer tool de escrita (cabeçalho `Idempotency-Key`).

#### 1.5.4 Por que MCP **e** A2A (não apenas um)

| Função | Onde | Exemplo |
|---|---|---|
| LLM consulta dado / executa operação no SGCF | **MCP** | "Claude, qual a dívida atual em USD?" → chama tool MCP `get_posicao_divida` |
| Agente A coordena tarefa com agente B | **A2A** | Comparador → Validador → Parecerista trocam tasks com estado e streaming |
| Agente externo descobre o que SGCF oferece | **A2A Agent Card** + **MCP tools list** | Both surfaces inspecionáveis |

#### 1.5.5 Princípios de codificação (ADR-014) aplicados ao plano

- Todo PR responde "isso poderia ser mais simples?"
- Código do motor financeiro é **funções puras** (sem side effects, sem dependência de tempo/contexto fora dos parâmetros)
- DTOs são `record` imutáveis
- Testes não usam mocks no domínio — Testcontainers + in-memory para integração
- Refatoração contínua orçada: ~10% do tempo de cada sprint
- Métricas de qualidade no CI: complexidade ciclomática (alerta >10), tamanho de arquivo (alerta >400 linhas), duplicação <3%

---

## 2. Fases e tarefas

### Fase B — Arquitetura técnica detalhada (4 semanas)

**Objetivo:** transformar ADR + Business Case + Anexos em Documento de Arquitetura executável + Backlog inicial.

#### B.1 Executar agente Arquiteto e produzir Documento de Arquitetura

**Descrição:** rodar o prompt `Prompt_Agente_Arquiteto_SGCF.md` com Claude Opus (ou equivalente) tendo como contexto **5 documentos** (em ordem de leitura recomendada):

1. **`SPEC.md`** (raiz do projeto) — documento âncora com objetivo, personas, glossário, padrões de precisão/arredondamento (HalfUp), timezone, LGPD, RBAC, convenções REST/MCP/A2A, SLAs, boundaries — **leitura obrigatória primeiro**
2. `Business_Case_Sistema_Contratos.md` — visão de negócio, ROI, escopo
3. `Anexo_A_Valoracao_Divida_NDF.md` — valoração e NDFs (Forward + Collar), MTM
4. `Anexo_B_Modalidades_e_Modelo_Dados.md` — modalidades, modelo polimórfico, garantias, cronograma
5. `ADR_Decisoes_Estrategicas.md` (v1.1) — decisões fixas (.NET 11, MCP, A2A, princípios de código)

**Atualização do prompt (necessária):** o `Prompt_Agente_Arquiteto_SGCF.md` foi escrito antes do SPEC e do ADR v1.1. **Atualizar o prompt** antes de executar para:
- Adicionar SPEC.md como 1º input obrigatório
- Trocar referência ".NET 8" por ".NET 11 (com fallback .NET 10 LTS)"
- Adicionar exigência de cobertura para MCP (ADR-012), A2A (ADR-013) e princípios ADR-014
- Adicionar como restrição absoluta os padrões do SPEC: HalfUp, NodaTime, Money/Percentual VOs, RFC 7807, idempotência, audit com `source`, RBAC matrix
- Adicionar nas validações finais: "Documento usa Money/Percentual VOs no schema?", "Schemas dos 8 tools MCP listados?", "Agent Card A2A descrito?"

Validar a saída de 200 palavras (dúvidas críticas) antes de autorizar a produção do documento completo.

**Critérios de aceite:**
- [ ] **`Prompt_Agente_Arquiteto_SGCF.md` atualizado para v1.1** com inclusão do SPEC e dos novos ADRs como contexto
- [ ] Documento de Arquitetura cobre as 15 seções do prompt (modelo de dados físico, API, motor de cálculos, jobs, segurança, observabilidade, CI/CD, migração, testes, custos, riscos)
- [ ] Documento adiciona 3 seções por força do SPEC + ADRs novos: **(16) Camada MCP**, **(17) Camada A2A**, **(18) Aderência aos padrões do SPEC** (precisão/arredondamento, NodaTime, VOs, RBAC, RFC 7807)
- [ ] Todas as validações da seção `<validacoes_antes_de_terminar>` do prompt passam
- [ ] Schema PostgreSQL completo (CREATE TABLE para todas as entidades) com tipos `numeric(20,6)` para monetário, `numeric(12,6)` para FX, `numeric(9,6)` para percentual conforme SPEC §8
- [ ] Lista de endpoints REST com método/path/request/response presente, seguindo padrões SPEC §12
- [ ] Estrutura de solution .NET (`.sln`/`.csproj`) sugerida — confrontar com layout do SPEC §5 e justificar divergências
- [ ] Schemas dos 8 tools MCP read-only (ADR-012) com JSON Schema 2020-12
- [ ] Esboço do Agent Card A2A (ADR-013)
- [ ] Aderência explícita ao Glossário (SPEC §7) — termos do código batem com termos do glossário

**Verificação:**
- [ ] Sponsor aprova ou solicita ajustes
- [ ] Documento salvo em `docs/architecture/Documento_Arquitetura_v1.md`
- [ ] Prompt atualizado salvo em `Prompt_Agente_Arquiteto_SGCF.md` (v1.1)

**Dependências:** nenhuma (SPEC v1.0 e ADR v1.1 já existentes na raiz)
**Estimativa:** S (1 documento, ~15-30 páginas equivalente — maior que v1.0 do plano original devido às 3 seções adicionais)

#### B.2 Revisão por agentes críticos em paralelo

**Descrição:** rodar 6 agentes críticos especializados (Security, DBA, DevOps, QA, Frontend, Code Reviewer) sobre o Documento de Arquitetura. Cada um produz relatório de 300-500 palavras com lacunas/melhorias.

**Critérios de aceite:**
- [ ] 6 relatórios produzidos, um por especialidade
- [ ] Lacunas críticas (severidade alta) consolidadas em backlog
- [ ] Documento de Arquitetura v1.1 incorpora correções de severidade alta

**Verificação:**
- [ ] Sponsor aprova v1.1
- [ ] `architecture/critical_reviews/` contém os 6 relatórios

**Dependências:** B.1
**Estimativa:** M (6 execuções em paralelo + consolidação)

#### B.3 Produzir backlog inicial (épicos + user stories)

**Descrição:** transformar o Documento de Arquitetura v1.1 em backlog de 30-50 user stories priorizadas, com t-shirt size, agrupadas por épico alinhado às Sprints da ADR-010.

**Critérios de aceite:**
- [ ] Cada story em formato "Como X, quero Y, para Z" com critérios de aceite explícitos
- [ ] Stories marcadas como MVP-mandatory vs nice-to-have
- [ ] Épicos correspondem às fases deste plano (Sprint 0 a 8)

**Verificação:**
- [ ] Backlog importado no tracker do squad (Jira/Linear/GitHub Projects)
- [ ] Sponsor + dev concordam com priorização da Sprint 0 e Sprint 1

**Dependências:** B.2
**Estimativa:** M

#### B.4 Definições pendentes do squad (decisões P0 antes do código)

**Descrição:** travar decisões pendentes do ADR-010 e v1.1 antes do início da Sprint 0.

**Critérios de aceite:**
- [ ] Front-end headless escolhido (ex: Next.js, Nuxt 3, Blazor, Vue 3 + Vite) com justificativa
- [ ] Perfil do dev confirmado (sênior .NET com afinidade financeira)
- [ ] Estratégia de reforço pontual definida (sim/não + budget)
- [ ] Política de retenção LGPD aprovada por Compliance
- [ ] Critérios de UAT definidos (quem participa, métricas de aceite)
- [ ] **Versão definitiva do .NET confirmada** — .NET 11 GA disponível? Se ainda preview, adotar .NET 10 LTS (ADR-003 v1.1)
- [ ] **SDK MCP escolhido** — oficial Anthropic vs implementação própria (ADR-012)
- [ ] **Versão da spec A2A escolhida** (ADR-013)
- [ ] **Decisão sobre Agente Migrador na Fase 1** — sim/não baseado em análise de qualidade da planilha (ADR-011)

**Verificação:**
- [ ] Decisões adicionais registradas como ADR-015+
- [ ] Quando "Agente Migrador" = sim, alocar 1-2 semanas extras na Fase 9 e definir modelo (recomendação: Gemini Fast)

**Dependências:** B.3
**Estimativa:** S (decisão executiva) + análise de dados da planilha (M, paralelo)

#### B.5 Prova de conceito MCP + A2A

**Descrição:** validar tecnicamente que SDK MCP em .NET e implementação A2A funcionam antes de escalar. Spike de 2-3 dias.

**Critérios de aceite:**
- [ ] Servidor MCP "Hello World" em .NET responde a um cliente Claude Desktop com 1 tool dummy
- [ ] Agent Card A2A publicado em endpoint local responde corretamente a `GET /.well-known/agent.json`
- [ ] Decisão técnica final: SDK escolhido, transporte (HTTP+SSE), padrão de serialização

**Verificação:**
- [ ] Demo gravada (3-5min) mostrando Claude Desktop chamando tool MCP local
- [ ] Documento de 2 páginas com decisões e gotchas encontrados

**Dependências:** B.4
**Estimativa:** S (spike de 2-3 dias)
**Arquivos prováveis:** `spikes/mcp-poc/`, `spikes/a2a-poc/`

---

### Checkpoint B → Sprint 0
- [ ] Documento de Arquitetura v1.1 aprovado
- [ ] Backlog priorizado importado no tracker
- [ ] Front-end stack escolhida
- [ ] Dev contratado/alocado
- [ ] **Gate humano:** sponsor autoriza início do build

---

### Sprint 0 — Infra e fundações (2 semanas)

**Objetivo:** ambiente GCP funcional, pipeline CI/CD entregando "Hello World" autenticado em produção, com audit log e observabilidade básicos.

#### 0.1 Provisionar projeto GCP + IAM + redes

**Descrição:** criar projetos GCP (dev, staging, prod), service accounts com least-privilege, VPC, firewall rules, e configurar billing/orçamentos com alertas.

**Critérios de aceite:**
- [ ] 3 projetos GCP isolados (dev/staging/prod) em `southamerica-east1`
- [ ] IAM por papel (dev, ops, sponsor) com roles específicos
- [ ] Service accounts da aplicação sem permissões de produção em dev
- [ ] Alerta de orçamento configurado (threshold 80%)

**Verificação:**
- [ ] `gcloud projects list` mostra os 3 projetos
- [ ] `gcloud iam service-accounts list` mostra contas com roles documentadas
- [ ] Tentativa cross-projeto retorna 403

**Dependências:** B.4
**Estimativa:** S (2-3 dias, infra-as-code via Terraform recomendado)
**Arquivos prováveis:** `infra/terraform/projects.tf`, `infra/terraform/iam.tf`

#### 0.2 Cloud SQL PostgreSQL 16 + pgvector + Memorystore + Cloud Storage

**Descrição:** instâncias gerenciadas para banco primário, cache e storage de PDFs. Habilitar CMEK, backups diários, point-in-time recovery. **Habilitar a extension `pgvector`** desde o início para suportar a camada RAG (ADR-015).

**Critérios de aceite:**
- [ ] Cloud SQL PostgreSQL 16 com CMEK habilitado em dev/staging/prod
- [ ] **Extension `pgvector` habilitada** (`CREATE EXTENSION IF NOT EXISTS vector`) — flag `cloudsql.enable_pgvector = on`
- [ ] Memorystore Redis (1GB inicial) em prod
- [ ] Cloud Storage bucket para PDFs com versionamento + retenção 5 anos
- [ ] Backup automático diário do Cloud SQL com retenção 30 dias

**Verificação:**
- [ ] Conexão do skeleton .NET ao Cloud SQL via Cloud SQL Auth Proxy
- [ ] `SELECT * FROM pg_extension WHERE extname = 'vector'` retorna 1 linha em todos os ambientes
- [ ] Restore de backup testado em ambiente dev

**Dependências:** 0.1
**Estimativa:** S
**Arquivos prováveis:** `infra/terraform/databases.tf`, `infra/terraform/storage.tf`

#### 0.3 Solution .NET 8 + estrutura Clean Architecture

**Descrição:** criar repositório git, solution `.sln` com projetos Domain / Application / Infrastructure / API / Tests. Configurar EF Core 8 + migrations Code First.

**Critérios de aceite:**
- [ ] Solution compila localmente (`dotnet build`)
- [ ] Pacotes-base instalados (FluentValidation, MediatR, Serilog, NodaTime, Testcontainers)
- [ ] Endpoint `/health/live` e `/health/ready` retornam 200
- [ ] Migration inicial cria tabela `__schema_history`
- [ ] EditorConfig + analyzers (Roslyn) configurados, build falha em warnings críticos

**Verificação:**
- [ ] `dotnet test` executa (mesmo sem testes ainda)
- [ ] `dotnet ef migrations list` mostra migration inicial

**Dependências:** 0.2
**Estimativa:** M (5 projetos + libs + first migration)
**Arquivos prováveis:** `src/Sgcf.Domain/`, `src/Sgcf.Application/`, `src/Sgcf.Infrastructure/`, `src/Sgcf.Api/`, `tests/`

#### 0.4 Auth + RBAC base

**Descrição:** OAuth 2.0 + OIDC via Identity Platform GCP. Claims-based authorization com policies. 6 papéis: tesouraria, contabilidade, gerente, diretor, auditor, admin.

**Critérios de aceite:**
- [ ] Login via OIDC funciona contra Identity Platform
- [ ] JWT validado por `[Authorize]` em endpoints
- [ ] Policies por papel implementadas (`PolicyTesouraria`, `PolicyAdmin`, etc.)
- [ ] Refresh token com expiração 15min/access + 24h/refresh

**Verificação:**
- [ ] Endpoint `/api/v1/me` autenticado retorna claims do usuário
- [ ] Endpoint protegido com role errado retorna 403
- [ ] Token expirado retorna 401

**Dependências:** 0.3
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Api/Auth/`, `src/Sgcf.Application/Authorization/Policies/`

#### 0.5 Audit trail (interceptor EF Core)

**Descrição:** interceptor EF Core que captura SaveChanges e grava na tabela `AUDIT_LOG` (entidade, id, ação, before/after JSONB, usuário, timestamp, correlation id).

**Critérios de aceite:**
- [ ] Tabela `AUDIT_LOG` criada via migration
- [ ] CRUD de qualquer entidade gera linha no AUDIT_LOG
- [ ] Campo `before`/`after` em JSONB com diff completo
- [ ] Retenção configurada para 5 anos

**Verificação:**
- [ ] Teste de integração: `INSERT/UPDATE/DELETE` em entidade dummy gera 3 linhas no AUDIT_LOG com payload correto
- [ ] PII mascarada (nenhum CPF/CNPJ em log de aplicação, apenas em AUDIT_LOG protegido)

**Dependências:** 0.4
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Infrastructure/Audit/AuditInterceptor.cs`

#### 0.6 CI/CD Cloud Build + deploy Cloud Run

**Descrição:** pipeline `cloudbuild.yaml` com etapas build → test → container → deploy. Ambientes dev (auto), staging (auto após PR merge), prod (gate manual).

**Critérios de aceite:**
- [ ] PR aberto dispara build + test
- [ ] Merge na `main` faz deploy em dev e staging automaticamente
- [ ] Deploy em prod exige aprovação manual via Cloud Deploy
- [ ] Rollback em 1 clique (Cloud Run revisões)

**Verificação:**
- [ ] PR de "hello world" passa pelo pipeline e chega em staging
- [ ] Endpoint `/health/live` em staging retorna 200
- [ ] Rollback testado: revisão anterior é restaurada em <1min

**Dependências:** 0.5
**Estimativa:** M
**Arquivos prováveis:** `cloudbuild.yaml`, `infra/terraform/cloudrun.tf`

#### 0.7 Observabilidade base (logs estruturados + métricas + tracing)

**Descrição:** Serilog com sink GCP, OpenTelemetry para Cloud Trace, métricas custom no Cloud Monitoring. Dashboard inicial e alertas críticos (5xx > 1%, p99 > 2s, deploy failure).

**Critérios de aceite:**
- [ ] Logs em JSON estruturado com correlation id
- [ ] Trace distribuído visível no Cloud Trace
- [ ] Dashboard "SGCF — Saúde da API" no Cloud Monitoring
- [ ] 3 alertas configurados disparando para canal Slack/email

**Verificação:**
- [ ] Request fim-a-fim (browser → API) gera trace com 3+ spans
- [ ] Falha forçada (500) dispara alerta em <5min

**Dependências:** 0.6
**Estimativa:** S
**Arquivos prováveis:** `src/Sgcf.Api/Telemetry/`, `infra/terraform/monitoring.tf`

---

### Checkpoint Sprint 0
- [ ] Hello World autenticado entregue em prod via pipeline
- [ ] Audit log grava CRUD de teste
- [ ] Logs estruturados visíveis no Cloud Logging
- [ ] Observabilidade com 3 alertas ativos
- [ ] **Stream M arrancou:** controladoria começou limpeza da planilha em paralelo
- [ ] **Gate humano:** sponsor aprova ir para Fase 1

---

### Fase 1 — Cadastros base (1 semana, parte da Sprint 1)

**Objetivo:** entidades de configuração (sem regra de negócio complexa) prontas e populadas — pré-requisito para cadastro de contratos.

#### 1.1 BANCO + BANCO_CONFIG

**Descrição:** entidade `BANCO` com config polimórfica conforme Anexo B seção 3 (aceita REFINIMP, % máx REFINIMP, % CDB cativo, exige outras garantias, prazo máx FINIMP, dispensa NDF, etc.). CRUD via API com role `admin`.

**Critérios de aceite:**
- [ ] Tabela `BANCO` + `BANCO_CONFIG` criadas via migration
- [ ] Endpoint `GET/POST/PUT /api/v1/bancos` com FluentValidation
- [ ] Seed inicial dos 10 bancos da Proxys com configs do Anexo B tabela 3.2 (mesmo com "A confirmar" pendentes)
- [ ] Audit log captura mudanças

**Verificação:**
- [ ] `GET /api/v1/bancos` retorna 10 bancos
- [ ] Teste de integração: criar BB e validar % CDB cativo = 30%, aceita REFINIMP = true, % máx = 70%

**Dependências:** Checkpoint Sprint 0
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Domain/Bancos/`, `src/Sgcf.Application/Bancos/`, `src/Sgcf.Api/Controllers/BancosController.cs`

#### 1.2 PLANO_CONTAS_GERENCIAL

**Descrição:** árvore hierárquica de contas conforme Anexo A seção 8.5 (3 níveis). Campo `codigo_sap_b1` nullable (preenchido na Fase 2 pós-MVP).

**Critérios de aceite:**
- [ ] Tabela `PLANO_CONTAS` com auto-relacionamento (parent_id) e seed das ~30 contas do Anexo A
- [ ] Endpoint `GET /api/v1/plano-contas` retorna árvore aninhada
- [ ] Endpoint `PUT /api/v1/plano-contas/{id}/codigo-sap` (admin) preenche `codigo_sap_b1`

**Verificação:**
- [ ] Árvore completa carregada em <100ms
- [ ] Teste verifica integridade: nenhum nó órfão, raízes com `parent_id = NULL`

**Dependências:** 1.1
**Estimativa:** S
**Arquivos prováveis:** `src/Sgcf.Domain/PlanoContas/`

#### 1.3 PARAMETRO_COTACAO (configuração de cotação por momento)

**Descrição:** entidade conforme Anexo A seção 8.3 — define qual tipo de cotação (PTAX_D-1, PTAX_D0, SPOT_INTRADAY) usar em cada momento (DESEMBOLSO, FIXING_JUROS, LIQUIDACAO_PRINCIPAL, LIQUIDACAO_NDF, MTM_GERENCIAL, MTM_CONTABIL). Override por banco/modalidade. Vigência temporal.

**Critérios de aceite:**
- [ ] Tabela criada com FK opcional para BANCO e ENUM de modalidade
- [ ] Seed da configuração inicial da Proxys (tabela do Anexo A 8.3)
- [ ] Endpoint admin para gerenciar parâmetros e suas vigências
- [ ] Função de domínio `ResolveTipoCotacao(momento, bancoId?, modalidade?, data)` retorna o tipo correto

**Verificação:**
- [ ] Teste unitário: `ResolveTipoCotacao(DESEMBOLSO, null, FINIMP, hoje)` → `PTAX_D_MENOS_1`
- [ ] Teste de override: parâmetro específico para BB tem precedência sobre o global

**Dependências:** 1.2
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Domain/Cotacoes/ParametroCotacao.cs`

---

### Checkpoint Fase 1
- [ ] Bancos cadastrados, plano de contas seedado, parâmetros de cotação configurados
- [ ] Stream M (controladoria) preencheu "A confirmar" do BANCO_CONFIG (Sicredi, Daycoval, Santander, etc.)
- [ ] **Gate humano:** sponsor valida configs com a realidade da Proxys

---

### Fase 2 — Vertical slice "FINIMP USD bullet" (3 semanas, Sprints 1-2)

**Objetivo:** entregar **uma operação de FINIMP USD bullet ponta a ponta** — cadastro, cronograma, saldo devedor em USD e BRL, tabela completa exportável. Esta fase prova que a arquitetura está correta antes de replicar para outras modalidades.

#### 2.1 Schema CONTRATO master + FINIMP_DETAIL

**Descrição:** tabela `CONTRATO` com campos comuns do Anexo B seção 2.1 + extensão `FINIMP_DETAIL` (ROF, exportador, fatura, incoterm, market flex). Estratégia polimórfica: tabela master + 1:1 com extensions.

**Critérios de aceite:**
- [ ] Migration cria `CONTRATO` + `FINIMP_DETAIL`
- [ ] Constraints: `modalidade ENUM`, `valor_principal > 0`, `data_vencimento > data_assinatura`
- [ ] Índices: `banco_id`, `status`, `modalidade`, `data_vencimento`
- [ ] Sequência `codigo_interno` formato `FIN-YYYY-NNNN`

**Verificação:**
- [ ] Migration aplicada em dev sem erros
- [ ] `INSERT` com modalidade FINIMP exige criação de `FINIMP_DETAIL` (validação aplicacional, não constraint DB)

**Dependências:** Fase 1
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Domain/Contratos/Contrato.cs`, `src/Sgcf.Domain/Contratos/Modalidades/FinimpDetail.cs`

#### 2.2 API CRUD de contrato FINIMP

**Descrição:** endpoints `POST/GET/PUT /api/v1/contratos` com validações (banco aceita modalidade, prazo dentro do limite, moeda válida, jurisdição compatível com IRRF). Idempotência via header `Idempotency-Key`.

**Critérios de aceite:**
- [ ] FluentValidation cobre todas as regras do Anexo B seções 2.1 e 4.2
- [ ] Idempotência: mesma `Idempotency-Key` em 24h retorna o mesmo recurso
- [ ] OpenAPI 3.1 gerado e publicado em `/swagger`
- [ ] Erro padrão (envelope `{type, title, status, detail, instance, errors[]}`)

**Verificação:**
- [ ] Teste E2E: cadastrar FINIMP BB Tóquio USD 200k, 180 dias, taxa 5,879% — retorna 201 com `codigo_interno` gerado
- [ ] Teste de idempotência: 2 POSTs com mesma chave retornam o mesmo id
- [ ] Teste de validação: FINIMP em Bradesco com `tem_refinimp = true` é bloqueado (Bradesco não aceita REFINIMP)

**Dependências:** 2.1
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Api/Controllers/ContratosController.cs`, `src/Sgcf.Application/Contratos/Commands/`

#### 2.3 CRONOGRAMA_PAGAMENTO + tipos de evento

**Descrição:** tabela conforme Anexo B seção 6.3, com 12 tipos de evento (PRINCIPAL, JUROS, IRRF_RETIDO, IOF_CAMBIO, COMISSAO_*, TARIFA_*, BREAK_FUNDING_FEE, MULTA_MORATORIA). Campo `numero_evento` agrupa parcelas da mesma data.

**Critérios de aceite:**
- [ ] Migration cria `CRONOGRAMA_PAGAMENTO`
- [ ] Constraint: `valor_moeda_original >= 0`
- [ ] Status default `PREVISTO`
- [ ] Índice composto `(contrato_id, data_prevista, tipo)`

**Verificação:**
- [ ] Inserir 8 linhas (4 eventos × principal+juros) compila e respeita FK
- [ ] Query "próxima parcela do contrato X" retorna em <50ms

**Dependências:** 2.2
**Estimativa:** S

#### 2.4 Gerador de cronograma — estratégia Bullet

**Descrição:** serviço de domínio `IGeradorCronograma` com implementação `BulletStrategy`. Dado um contrato FINIMP, gera as linhas do cronograma (juros provisionados + principal no vencimento + IOF no desembolso e na liquidação + IRRF gross-up sobre juros + tarifas fixas).

**Critérios de aceite:**
- [ ] Strategy pattern com interface clara para futuras estratégias (SAC, Price, Custom)
- [ ] Bullet gera N linhas: 1 principal + 1 juros + IOF entrada + IOF saída + IRRF + tarifas (ROF, CADEMP)
- [ ] Cálculo de juros: `Principal × Taxa × Prazo / Base` (base configurável 360/365/252)
- [ ] IRRF gross-up: `Juros × aliq / (1 - aliq)`

**Verificação:**
- [ ] **Golden test**: contrato BB Tóquio USD 200k, 180d, taxa 5,879% — cronograma gerado bate com cálculo manual da analista (validado contra `Analise_FINIMP_BB_vs_Itau.xlsx`)
- [ ] Property-based test: `SUM(parcelas tipo PRINCIPAL) == valor_principal_inicial`

**Dependências:** 2.3
**Estimativa:** M (núcleo do motor de cálculo)
**Arquivos prováveis:** `src/Sgcf.Domain/Cronograma/Strategies/BulletStrategy.cs`

#### 2.5 Cálculo de saldo devedor

**Descrição:** serviço `ICalculadorSaldo` que retorna, para um contrato em uma data, os 3 componentes do Anexo B seção 6.4: saldo principal aberto, juros provisionados pro rata, comissões a pagar — em moeda original e BRL.

**Critérios de aceite:**
- [ ] Fórmulas exatamente conforme Anexo B seção 6.4
- [ ] Juros provisionados pro rata diária desde último pagamento
- [ ] Suporte a parametrização de cotação (usa `ResolveTipoCotacao` da Fase 1)
- [ ] Performance: cálculo de saldo de um contrato em <50ms

**Verificação:**
- [ ] **Golden test**: 4131 BB do Anexo B seção 6.6 — em 01/03/2026, saldo total USD 1.010.000, BRL 5.353.000 (com PTAX 5,30)
- [ ] **Golden test**: mesmo contrato em 15/09/2026 (após 1ª parcela) — saldo USD 759.625, BRL 4.026.012,50
- [ ] Property: `saldo_total = saldo_principal + juros_provisionados + comissoes_a_pagar`

**Dependências:** 2.4
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Domain/Calculo/CalculadorSaldo.cs`

#### 2.6 Endpoint "Tabela completa do contrato"

**Descrição:** `GET /api/v1/contratos/{id}/tabela-completa` retorna estrutura JSON com os 8 blocos do Anexo B seção 7.1: Identificação, Valores, Encargos, Resumo financeiro, Cronograma, Garantias (placeholder Fase 5), Hedge (placeholder Fase 6), Histórico de pagamentos.

**Critérios de aceite:**
- [ ] DTO completo serializado em <200ms
- [ ] Inclui indicadores operacionais (% adimplência, próxima parcela, % principal amortizado, % prazo decorrido)
- [ ] Render HTML server-side disponível em `Accept: text/html`

**Verificação:**
- [ ] Resposta JSON do contrato exemplo do Anexo B seção 7.2 corresponde campo a campo ao mockup ASCII
- [ ] Teste de regressão: snapshot do JSON é versionado

**Dependências:** 2.5
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Application/Contratos/Queries/GetTabelaCompleta.cs`

---

### Checkpoint Fase 2
- [ ] FINIMP USD bullet cadastrado, cronograma correto, saldo devedor em USD/BRL preciso, tabela completa retorna JSON
- [ ] **Cobertura de testes do motor de cálculo ≥ 95%** (financeiro exige zero erro)
- [ ] Demo ao sponsor: cadastrar uma operação real e comparar com `Analise_FINIMP_BB_vs_Itau.xlsx`
- [ ] **Gate humano:** sponsor aprova arquitetura do motor antes de replicar para outras modalidades

---

### Fase 3 — Multi-moeda + ingestão PTAX (2 semanas, Sprint 3)

**Objetivo:** sistema usa cotações reais do BCB com 3 visões (intraday, contábil oficial, fixing) conforme Anexo A seção 5.

#### 3.1 Schema COTACAO_FX + ingestão PTAX (BCB API)

**Descrição:** tabela conforme Anexo A seção 4.1. Cron jobs para ingestão automática: PTAX parciais (10h/11h/12h/13h), PTAX D0 oficial (~13h15), spot intraday (5-15min).

**Critérios de aceite:**
- [ ] Cliente HTTP para API SCB-BCB (`OlinDaPTAX`) com retry e circuit breaker
- [ ] Cloud Scheduler dispara jobs nas frequências definidas
- [ ] Idempotência: re-ingestão da mesma cotação não duplica
- [ ] Suporte a 4 moedas (USD, EUR, JPY, CNY) + cross via BCB se necessário

**Verificação:**
- [ ] PTAX D0 ingerida automaticamente em horário de mercado
- [ ] Histórico de 30 dias importado e validado contra fonte oficial
- [ ] Job falhando dispara alerta

**Dependências:** Fase 2
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Infrastructure/Bcb/PtaxIngestor.cs`, `src/Sgcf.Jobs/IngestaoPtaxJob.cs`

#### 3.2 Cache intraday Redis para spot

**Descrição:** cotações spot intraday em Memorystore com TTL 30s. Fallback para PTAX D0 ou D-1 se cache vazio.

**Critérios de aceite:**
- [ ] Cache hit ratio > 95% em horário comercial
- [ ] Fallback documentado e testado

**Verificação:**
- [ ] Teste de carga: 100 req/s de saldo BRL não estressa BCB API

**Dependências:** 3.1
**Estimativa:** S

#### 3.3 Saldo BRL com cotação configurável por momento

**Descrição:** integrar `ResolveTipoCotacao` (Fase 1) ao `CalculadorSaldo` (Fase 2). Retornar saldo BRL com 3 visões: gerencial intraday, contábil oficial PTAX D0, fixing.

**Critérios de aceite:**
- [ ] Resposta da API inclui `tipo_cotacao_aplicada`, `valor_cotacao`, `data_hora_cotacao`
- [ ] Tela "tabela completa" mostra label "Posição estimada" se intraday
- [ ] Suporte a JPY com nota visual (multiplicar por 1.000) — Anexo A seção 8.4

**Verificação:**
- [ ] Saldo BRL de FINIMP USD 200k com PTAX D-1 = 5,00 → R$ 1.000.000
- [ ] Mesmo contrato com spot 5,30 → R$ 1.060.000 + label "Posição estimada"

**Dependências:** 3.2
**Estimativa:** M

---

### Checkpoint Fase 3
- [ ] Sistema mostra saldo BRL preciso para 4 moedas com cotação correta por momento
- [ ] PTAX ingerida automaticamente sem intervenção manual
- [ ] **Gate humano:** sponsor valida cotações contra fonte oficial

---

### Fase 4 — Outras modalidades (2 semanas, Sprints 3-4)

**Objetivo:** replicar a arquitetura validada na Fase 2 para 4131, REFINIMP, NCE, Balcão Caixa, FGI. Cada modalidade adiciona apenas extensão + estratégia de cronograma.

#### 4.1 Modalidade 4131 + estratégia SAC e Bullet

**Descrição:** `LEI_4131_DETAIL` (SBLC, market flex, break funding). Suporte a cronograma com semestrais (SAC) — caso BB do Anexo B seção 6.6.

**Critérios de aceite:**
- [ ] Tabela `LEI_4131_DETAIL` + endpoint POST específico
- [ ] Estratégia `SacStrategy` gera cronograma com amortização constante e juros decrescentes
- [ ] Validação: prazo até 720+ dias permitido, market flex flag

**Verificação:**
- [ ] **Golden test**: 4131 BB Anexo B seção 6.6 (USD 1MM, 4 semestrais) — gera 8 linhas exatas conforme tabela
- [ ] Saldo em qualquer data bate com Anexo B seção 6.6

**Dependências:** Fase 3
**Estimativa:** M

#### 4.2 Modalidade REFINIMP + relacionamento contrato-mãe

**Descrição:** `REFINIMP_DETAIL` com `contrato_mae_id`, `percentual_refinanciado`, `valor_quitado_no_refi`. Validações do Anexo B seção 4.2 (banco aceita, % máx, moeda igual, prazo).

**Critérios de aceite:**
- [ ] Cadastro de REFINIMP marca contrato-mãe como `REFINANCIADO_PARCIAL` ou `REFINANCIADO`
- [ ] Validação: Bradesco/Santander rejeitam (`aceita_refinimp = false`)
- [ ] Validação: BB rejeita REFINIMP > 70% do principal original
- [ ] Suporte a cadeia recursiva (REFINIMP de REFINIMP)

**Verificação:**
- [ ] Teste E2E: criar FINIMP BB USD 200k → REFINIMP 70% (USD 140k) — original muda para `REFINANCIADO_PARCIAL`, novo contrato vincula via `contrato_mae_id`
- [ ] Teste negativo: tentativa de REFINIMP 100% no BB retorna 400

**Dependências:** 4.1
**Estimativa:** M

#### 4.3 Modalidades NCE/CCE, Balcão Caixa, FGI

**Descrição:** extensões `NCE_DETAIL`, `BALCAO_CAIXA_DETAIL`, `FGI_DETAIL` + endpoints POST específicos.

**Critérios de aceite:**
- [ ] 3 modalidades cadastráveis com seus campos específicos
- [ ] NCE: sem IRRF, sem IOF câmbio (validação automática)
- [ ] Balcão Caixa: estratégia `CustomScheduleStrategy` aceita lista de parcelas manuais
- [ ] FGI: campo `taxa_fgi_aa` adicionado ao cálculo de despesa total

**Verificação:**
- [ ] 3 contratos cadastrados, cronogramas corretos
- [ ] NCE não tem linhas de IRRF/IOF no cronograma

**Dependências:** 4.2
**Estimativa:** M

---

### Checkpoint Fase 4
- [ ] 6 modalidades cadastráveis e calculáveis
- [ ] Cronogramas corretos validados contra exemplos do Anexo B
- [ ] **Gate humano:** controladoria valida com 1 contrato real de cada modalidade

#### 4.4 Motor de antecipação de pagamento (Strategy pattern, 5 padrões)

**Descrição:** implementar `IEstrategiaAntecipacao` em `Sgcf.Domain.Antecipacao/` com 5 implementações puras (Padrões A-E do Anexo C). Ampliação do `BANCO_CONFIG` com os 11 novos campos da §5.1 do Anexo C (`padrao_antecipacao`, `break_funding_fee_pct`, `tla_pct_sobre_saldo`, etc.). Endpoint `POST /api/v1/contratos/{id}/simular-antecipacao` com idempotência. Persistência em `simulacao_antecipacao` com payload JSONB de componentes.

**Critérios de aceite:**
- [ ] 5 strategies (`PadraoA`, `PadraoB`, `PadraoC`, `PadraoD`, `PadraoE`) implementadas como funções puras (sem `DateTime.Now`, sem I/O)
- [ ] Migration adiciona campos de antecipação ao `BANCO_CONFIG`
- [ ] Migration cria `SIMULACAO_ANTECIPACAO`
- [ ] Endpoint REST aceita 5 tipos (T1-T5) e valida restrições por banco (aviso prévio, valor mínimo parcial, exigência de parcela inteira, anuência expressa)
- [ ] Resposta inclui detalhamento dos 15 componentes de custo aplicáveis + comparativo "sem antecipar + CDI"
- [ ] **Alerta crítico** específico para Sicredi FINIMP: "antecipação não economiza juros — banco cobra período total contratado"
- [ ] Audit log captura cada simulação com `actor_id` e `source`

**Verificação:**
- [ ] **Golden tests dos 5 padrões** (Anexo C §7) passam exatamente
- [ ] Caso BB FINIMP: principal USD 367.315,30, antecipação em 119 dias — break funding 1% calculado corretamente sobre `(C1+C2)`
- [ ] Caso Sicredi FINIMP: alerta crítico disparado e juros do período total cobrados (`C3`)
- [ ] Caso FGI BV: fórmula MTM `(1 + taxa_contratada)^t / (1 + taxa_mercado)^t` aplicada com tolerância ≤ 0,01
- [ ] Caso Caixa Balcão: `max(2% × VTD; 0,1% × Pzr × VTD)` cobrado; isenção em refinanciamento interno funcionando
- [ ] Caso Caixa abatimento prefixado: juros futuros embutidos descontados proporcionalmente

**Dependências:** 4.3 (todas modalidades cadastráveis)
**Estimativa:** L (5 strategies + endpoint + persistência + golden tests)
**Arquivos prováveis:** `src/Sgcf.Domain/Antecipacao/`, `src/Sgcf.Application/Antecipacao/Commands/SimularAntecipacao.cs`, `src/Sgcf.Api/Controllers/AntecipacaoController.cs`

---

### Fase 5 — Garantias polimórficas (1,5 semana, Sprint 4-5)

**Objetivo:** modelar e expor os 8 tipos de garantia conforme Anexo B seção 8.

#### 5.1 GARANTIA master + extensões polimórficas

**Descrição:** padrão similar a CONTRATO — `GARANTIA` com FK para contrato + 8 tabelas de extensão (CDB cativo, SBLC, recebíveis cartão, boleto, duplicatas, FGI, aval, alienação fiduciária).

**Critérios de aceite:**
- [ ] 9 migrations (1 master + 8 extensions)
- [ ] Endpoint `POST /api/v1/contratos/{id}/garantias` com discriminator no payload
- [ ] Cálculo automático de `percentual_principal` (valor_brl / saldo_devedor_brl)
- [ ] Lifecycle: ATIVA → LIBERADA / EXECUTADA / CANCELADA

**Verificação:**
- [ ] Cadastrar contrato 4131 BB com 3 garantias (CDB cativo + recebíveis Cielo + boletos Daycoval) — todas listadas na tabela completa do contrato
- [ ] Cobertura total = 36,8% (caso do Anexo B seção 7.2)

**Dependências:** Fase 4
**Estimativa:** L (8 sub-entidades — considerar quebra em 5.1a/5.1b)

#### 5.2 Validações automáticas + indicadores

**Descrição:** validações do Anexo B seção 8.5 (cobertura, % CDB mínimo, prazo, faturamento cartão).

**Critérios de aceite:**
- [ ] Alerta (não bloqueio) se cobertura < 100%
- [ ] Alerta crítico se moeda estrangeira sem NDF (gancho para Fase 6)
- [ ] Bloqueio se garantia vence antes do contrato
- [ ] Indicador `cobertura_total_brl`, `cobertura_liquida_sem_cdb`, `% faturamento cartão comprometido`

**Verificação:**
- [ ] Teste: contrato FINIMP USD sem NDF e sem outra cobertura cambial dispara alerta crítico
- [ ] Teste: tentar cadastrar CDB cativo com vencimento anterior ao contrato → 400

**Dependências:** 5.1
**Estimativa:** M

---

### Checkpoint Fase 5
- [ ] Tabela completa do contrato agora inclui bloco F (Garantias) populado
- [ ] Painel de garantias com agregação por tipo/banco (placeholder até Fase 7)

---

### Fase 6 — NDFs + Mark-to-Market (3 semanas, Sprints 5-6) ⚠ FASE DE MAIOR RISCO TÉCNICO

**Objetivo:** Forward simples e Collar com MTM intraday, alertas de exposição, liquidação no fixing PTAX D0.

#### 6.1 INSTRUMENTO_HEDGE + Forward simples

**Descrição:** schema do Anexo A seção 4.1, vinculação 1:1 com contrato (NOT NULL aplicacional, nullable em DB para evolução futura — Anexo A 8.2). Estrutura `FORWARD_SIMPLES` com strike único.

**Critérios de aceite:**
- [ ] Tabela criada com FK + tipo extensível
- [ ] Validações: notional ≤ saldo do contrato, vencimento ≤ vencimento do contrato, moeda igual
- [ ] Endpoint `POST /api/v1/contratos/{id}/hedges`

**Verificação:**
- [ ] Cadastrar Forward USD 200k strike 5,50 vinculado a FINIMP USD 200k — sucesso
- [ ] Mismatch de moeda → 400

**Dependências:** Fase 5
**Estimativa:** M

#### 6.2 MTM Forward simples

**Descrição:** fórmula do Anexo A seção 3.2 — `Payoff = Notional × (S_atual − K)`. Suporte a 4 moedas. Cotação spot via cache Redis.

**Critérios de aceite:**
- [ ] Função pura `CalcularMtmForward(notional, strike, spot)` testável
- [ ] Endpoint `GET /api/v1/hedges/{id}/mtm` retorna `{valor_brl, posicao (RECEBER/PAGAR), data_hora_cotacao, tipo_cotacao}`
- [ ] Tempo resposta < 100ms

**Verificação:**
- [ ] **Golden test 1**: USD 200k, strike 5,50, spot 4,80 → payoff −R$ 140.000 (empresa paga)
- [ ] **Golden test 2**: spot 5,80 → payoff +R$ 60.000 (empresa recebe)
- [ ] **Golden test 3**: spot = strike → payoff 0

**Dependências:** 6.1
**Estimativa:** M

#### 6.3 Collar (Range Forward) + MTM

**Descrição:** estrutura `COLLAR` com `strike_put` e `strike_call`. Fórmula condicional do Anexo A seção 3.2.

**Critérios de aceite:**
- [ ] Cadastro exige `strike_put` e `strike_call` com `put < call`
- [ ] Função `CalcularMtmCollar` cobre os 3 ramos (S<put / put≤S≤call / S>call)

**Verificação:**
- [ ] **Golden test**: cenários A/B/C do Anexo A seção 2.1 (USD 200k, put 5,10, call 5,40) — bate exatamente com tabela
- [ ] Property: dentro da banda, payoff sempre 0

**Dependências:** 6.2
**Estimativa:** M

#### 6.4 Job de MTM intraday

**Descrição:** Cloud Scheduler dispara recálculo de MTM de todos os hedges abertos a cada 5min em horário de mercado. Persiste em `POSICAO_SNAPSHOT` para histórico e alertas.

**Critérios de aceite:**
- [ ] Job idempotente (mesma rodada não duplica snapshot)
- [ ] Processa 1.200 hedges em <30s
- [ ] Métrica custom `sgcf_mtm_recalculado_total` no Cloud Monitoring

**Verificação:**
- [ ] Job executado manualmente em ambiente staging atualiza todos os MTMs
- [ ] Falha de BCB API não derruba job (fallback para última PTAX conhecida)

**Dependências:** 6.3
**Estimativa:** M

#### 6.5 Liquidação NDF + alertas de exposição

**Descrição:** ao chegar data de fixing (PTAX D0), congela MTM como `valor_liquidacao` e marca hedge como `LIQUIDADO`. Alertas conforme Anexo A seção 3.4.

**Critérios de aceite:**
- [ ] Job diário às 14h busca NDFs vencendo hoje, calcula liquidação com PTAX D0 oficial
- [ ] 5 alertas implementados: dívida em moeda X passou Y%, cobertura <80%, NDF próximo do strike, vencimento sem rolagem (D-15), mismatch NDF/FINIMP
- [ ] Alertas via email + Slack + dashboard

**Verificação:**
- [ ] Simular NDF vencendo hoje → liquidação registrada com PTAX D0 do dia
- [ ] Simular spot a 0,5% do strike call → alerta disparado

**Dependências:** 6.4
**Estimativa:** M

---

### Checkpoint Fase 6
- [ ] Forward + Collar com MTM intraday em produção
- [ ] Liquidação automática no fixing
- [ ] 5 alertas de exposição ativos
- [ ] **Cobertura de testes ≥ 95% nas funções de MTM**
- [ ] **Gate humano:** sponsor + analista validam MTM contra cálculo manual em 5 hedges reais (golden dataset)

---

### Fase 7 — Painéis, dashboards e exportações (2 semanas, Sprints 6-7)

**Objetivo:** entregar visões consolidadas para tesouraria e diretoria — a "vitrine" do MVP.

#### 7.1 Painel consolidado de dívida (multi-moeda)

**Descrição:** endpoint `GET /api/v1/painel/divida` retorna a estrutura do Anexo A seção 3.1 — breakdown por moeda, dívida bruta BRL, ajuste MTM, dívida líquida pós-hedge, caixa, dívida líquida final. Drill-down até contrato.

**Critérios de aceite:**
- [ ] 4 moedas + BRL agregadas
- [ ] MTM de NDFs separado em "a receber" e "a pagar"
- [ ] Performance < 500ms para 1.200 contratos (usar materialized view se necessário)
- [ ] Drill-down via `?banco=X` ou `?modalidade=Y`

**Verificação:**
- [ ] Painel reproduz exatamente a estrutura do exemplo do Anexo A seção 3.1
- [ ] Tempo de resposta sob carga (10 req/s) < 500ms p99

**Dependências:** Fase 6
**Estimativa:** M

#### 7.2 Painel de garantias

**Descrição:** estrutura do Anexo B seção 8.4 (visão 2): por tipo, por banco, alertas críticos.

**Critérios de aceite:**
- [ ] Distribuição por tipo (CDB, recebíveis, boleto, SBLC, aval) com %
- [ ] Distribuição por banco
- [ ] Alertas listados (CDB vencendo, % faturamento estourado, boleto vencendo)

**Verificação:**
- [ ] Painel reproduz exemplo do Anexo B seção 8.4

**Dependências:** 7.1
**Estimativa:** S

#### 7.3 Calendário consolidado de vencimentos + curva de amortização

**Descrição:** `GET /api/v1/painel/vencimentos?ano=2026` retorna calendário mensal de pagamentos previstos. Curva de amortização agregada por contrato/portfolio.

**Critérios de aceite:**
- [ ] Calendário 12 meses com totais por mês em BRL
- [ ] Curva de amortização: histórico real + projeção
- [ ] Filtros por banco/modalidade/moeda

**Verificação:**
- [ ] Soma do calendário = soma das parcelas previstas + atrasadas

**Dependências:** 7.2
**Estimativa:** M

#### 7.4 Exportação PDF e Excel da tabela completa

**Descrição:** `GET /api/v1/contratos/{id}/tabela-completa?formato=pdf|xlsx`. Render server-side, marca d'água com usuário/data/hora.

**Critérios de aceite:**
- [ ] PDF reproduz layout do Anexo B seção 7.2 (8 blocos)
- [ ] Excel respeita formato compatível com modelos da Proxys
- [ ] Log de exportação (quem, quando, qual contrato, qual formato)

**Verificação:**
- [ ] PDF renderizado contém marca d'água com nome do usuário
- [ ] Excel abre sem erro no Excel 365 e LibreOffice

**Dependências:** 7.3
**Estimativa:** M
**Arquivos prováveis:** lib `QuestPDF` para PDF, `ClosedXML` ou `EPPlus` para Excel

#### 7.5 Dashboard executivo (KPIs)

**Descrição:** Dívida Total, Dívida Líquida, Dívida/EBITDA (EBITDA cadastrado manualmente até integração SAP), Share por banco, Custo Médio Ponderado, Prazo Médio.

**Critérios de aceite:**
- [ ] 6 KPIs calculados e expostos via endpoint
- [ ] Comparativo mês atual vs mês anterior
- [ ] Endpoint admin para input de EBITDA mensal

**Verificação:**
- [ ] KPIs batem com cálculo manual em 1 mês de teste
- [ ] Sponsor valida que números são "apresentáveis em comitê"

**Dependências:** 7.4
**Estimativa:** M

#### 7.6 Simulador de cenários (estresse cambial + antecipação de portfolio)

**Descrição:** Anexo A seção 3.3 + 3.5 (estresse cambial) + Anexo C §6 (simulação de antecipação). `POST /api/v1/simulador/cenario` com payload `{cotacao_usd: 5.50, cotacao_eur: 6.20, ...}` retorna posição projetada. Adicionalmente, `POST /api/v1/simulador/antecipacao-portfolio` com payload `{caixa_disponivel_brl: X}` ranqueia contratos por economia gerada se antecipados (usa motor da tarefa 4.4).

**Critérios de aceite:**
- [ ] 3 cenários cambiais pré-calculados (pessimista −10%, realista, otimista +10%)
- [ ] Cenário cambial customizado via payload
- [ ] Resposta de cenário cambial inclui delta vs realista
- [ ] Simulador de antecipação de portfolio retorna ranking ordenado por economia líquida (descontando custo de antecipação vs rentabilidade de aplicar o caixa em CDI)
- [ ] Considera restrições por banco (aviso prévio, valor mínimo parcial, anuência) ao gerar recomendações

**Verificação:**
- [ ] Cenário −10% no USD reduz dívida USD em 10%, propaga para dívida líquida
- [ ] Simulação de portfolio: dado caixa de R$ 5MM, retorna top 5 contratos com maior economia, considerando custos exatos por banco
- [ ] Sicredi FINIMP **nunca** aparece no top de recomendações (cobra juros totais — não economiza)

**Dependências:** 7.5, 4.4
**Estimativa:** M

---

### Checkpoint Fase 7
- [ ] 4 painéis consolidados em produção
- [ ] Tabela completa exportável em PDF e Excel
- [ ] Simulador de cenários respondendo em <1s
- [ ] **Gate humano:** sponsor faz reunião de comitê de tesouraria usando o sistema (caso 1 do Anexo A seção 6) e valida fluxo

---

### Fase 7B — Servidor MCP + Baseline A2A (1,5 semana, Sprint 7)

**Objetivo:** expor o SGCF como cidadão de primeira classe no ecossistema de agentes — servidor MCP read-only com 8 tools + Agent Card A2A + 1 skill demonstrativa. **Tudo reutilizando os Application Services já implementados** — zero duplicação de lógica.

#### 7B.1 Infra do servidor MCP (auth, transport, scaffolding)

**Descrição:** subir endpoint MCP (`/mcp` ou subdomínio) usando o SDK escolhido na Fase B.5. Auth OAuth 2.1 com escopos por tool. Transport HTTP+SSE.

**Critérios de aceite:**
- [ ] Endpoint MCP responde a `tools/list` com lista vazia inicial
- [ ] Auth OAuth 2.1 obrigatória — request sem token retorna 401
- [ ] Logs estruturados com `source: "mcp"` em todos os spans
- [ ] Audit log captura cada chamada MCP (mesmo trail do REST)
- [ ] Rate limiting separado: 60 req/min por usuário (configurável)

**Verificação:**
- [ ] Cliente Claude Desktop conecta ao endpoint e lista tools (vazio)
- [ ] Tentativa sem token retorna 401 com erro padrão MCP
- [ ] Audit log de teste mostra `source: "mcp"` corretamente

**Dependências:** Checkpoint Fase 7 + B.5
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Mcp/`, `src/Sgcf.Mcp/Server.cs`, `src/Sgcf.Mcp/Auth/`

#### 7B.2 Tools MCP read-only (8 tools)

**Descrição:** implementar os 8 tools listados no ADR-012 como adaptadores finos sobre Application Services existentes (queries MediatR).

**Tools:**
1. `list_contratos(filters)` → lista resumida
2. `get_contrato(id)` → contrato + extensão por modalidade
3. `get_tabela_completa(id, formato)` → estrutura completa (8 blocos), suporta `markdown` e `json`
4. `get_posicao_divida(data?)` → posição consolidada multi-moeda
5. `get_calendario_vencimentos(periodo)` → próximos vencimentos
6. `get_cotacao_fx(moeda, data?)` → cotação atual ou histórica
7. `get_mtm_hedge(hedge_id)` → MTM atual de um NDF
8. `simular_cenario_cambial(deltas_por_moeda)` → estresse cambial sem persistir
9. `simular_antecipacao(contrato_id, tipo, data, valor?, taxa_mercado?)` → simulação de pré-pagamento com detalhamento dos componentes de custo (Anexo C)
10. `buscar_clausula_contratual(query, filtros)` → busca semântica em cláusulas indexadas (ADR-015), retorna trechos com citação literal
11. `comparar_clausulas(contrato_id_1, contrato_id_2, topico)` → comparação textual lado a lado entre 2 contratos (ADR-015)

**Critérios de aceite:**
- [ ] 11 tools registrados e descobertos via `tools/list`
- [ ] Cada tool tem schema JSON Schema válido + descrição clara para LLM
- [ ] Cada tool reutiliza handler MediatR existente (zero lógica duplicada)
- [ ] Erros padronizados conforme spec MCP (com `isError: true` no resultado)

**Verificação:**
- [ ] Suite de testes de integração com cliente MCP de teste invoca os 8 tools com payloads válidos e inválidos
- [ ] Demo via Claude Desktop: "qual a dívida atual em USD?" → resposta correta usando `get_posicao_divida`
- [ ] Auditoria: cada chamada gera linha no `AUDIT_LOG`

**Dependências:** 7B.1
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.Mcp/Tools/*.cs`

#### 7B.3 Agent Card A2A + endpoint de descoberta + 1 skill demonstrativa

**Descrição:** publicar `/.well-known/agent.json` descrevendo o SGCF como agente A2A. Implementar 1 skill (`consulta_posicao_divida`) traduzindo de mensagem A2A para Application Service interno.

**Critérios de aceite:**
- [ ] Agent Card válido conforme spec A2A escolhida na Fase B.5
- [ ] Endpoint `tasks/send` aceita request da skill demonstrativa
- [ ] Endpoint `tasks/get` retorna estado da task
- [ ] Auth OAuth 2.1 (mesmo IdP do REST/MCP)

**Verificação:**
- [ ] Cliente A2A de teste descobre o agente via Agent Card
- [ ] Cliente envia task `consulta_posicao_divida` e recebe resposta correta
- [ ] Task aparece no `AUDIT_LOG` com `source: "a2a"`

**Dependências:** 7B.2
**Estimativa:** M
**Arquivos prováveis:** `src/Sgcf.A2a/`, `src/Sgcf.Api/wwwroot/.well-known/agent.json` (gerado dinamicamente)

#### 7B.4 Documentação dos tools + onboarding de agentes externos

**Descrição:** documentação para "como conectar Claude Desktop / Claude Code / Gemini ao SGCF via MCP". Catálogo de tools com exemplos.

**Critérios de aceite:**
- [ ] `docs/mcp/README.md` com setup passo-a-passo
- [ ] Cada tool documentado com payload de exemplo + resposta esperada
- [ ] `docs/a2a/README.md` com Agent Card + exemplo de cliente
- [ ] Política de auth/escopos documentada (como obter token, escopos por papel)

**Verificação:**
- [ ] Sponsor (não-dev) conecta Claude Desktop ao SGCF seguindo apenas a documentação
- [ ] Conseguir consultar posição de dívida via Claude em <5min de setup

**Dependências:** 7B.3
**Estimativa:** S

#### 7B.6 Camada RAG complementar — schema, indexação e 2 tools (ADR-015)

**Descrição:** implementar a infraestrutura de busca semântica em cláusulas contratuais conforme ADR-015 e SPEC §8.6. Schema `clausula_contratual` com pgvector (extension já habilitada na Sprint 0). Cliente HTTP para Gemini text-embedding-005 com retry/circuit breaker. Parser de PDF que segmenta por cláusula e gera embeddings em batch. 2 tools MCP read-only.

**Critérios de aceite:**
- [ ] Migration cria tabela `clausula_contratual` com índice `hnsw` em `embedding` e índice composto em metadados (`banco_id`, `modalidade`, `topico`)
- [ ] Cliente Gemini embeddings em `Sgcf.Infrastructure/Embeddings/` com cache de 24h e retry exponencial
- [ ] Parser de PDF por cláusula em `tools/IndexadorContratos/` (modo batch + modo "indexar contrato individual")
- [ ] Tool `buscar_clausula_contratual` aceita `query` (texto livre) + filtros opcionais (`banco_id`, `modalidade`, `topico`, `data_min`)
- [ ] Tool `comparar_clausulas` aceita 2 `contrato_id` + `topico`, retorna trechos lado a lado
- [ ] **Boundary técnico**: ambos os tools retornam APENAS texto literal e metadados — nenhum cálculo financeiro, nenhuma síntese inferida
- [ ] Audit log captura cada chamada com `source: "mcp"` e `tool` correspondente
- [ ] Documentação em `docs/mcp/rag-tools.md` com exemplos

**Verificação:**
- [ ] Indexação de 5 contratos de teste gera ~50-100 chunks; busca por "break funding fee" retorna a cláusula 12 do BB FINIMP no top-3
- [ ] Comparação entre BB FINIMP e Sicredi FINIMP no tópico "antecipacao" retorna corretamente a cláusula 12 vs §5º Cl. 2ª
- [ ] Latência p99 da busca < 200ms para corpus de 1.200 contratos
- [ ] Demo via Claude Desktop: pergunta "o que diz o contrato BB sobre pré-pagamento?" retorna trecho com citação

**Dependências:** 7B.5 (ou 7B.4 se 7B.5 for postergada)
**Estimativa:** M (~3-5 dias — schema, parser, tools, testes)
**Arquivos prováveis:** `src/Sgcf.Domain/Clausulas/`, `src/Sgcf.Infrastructure/Embeddings/`, `src/Sgcf.Mcp/Tools/BuscarClausulaTool.cs`, `src/Sgcf.Mcp/Tools/CompararClausulasTool.cs`, `tools/IndexadorContratos/`

#### 7B.5 (Opcional) Tools MCP de escrita

**Descrição:** se sponsor decidir, expor 3 tools de escrita: `create_contrato`, `registrar_pagamento`, `cadastrar_hedge`. Audit reforçado e idempotência obrigatória.

**Critérios de aceite (se executado):**
- [ ] Cada tool exige `Idempotency-Key` no payload
- [ ] Audit log marca `source: "mcp"` + `actor_type: "agent"` (vs "user")
- [ ] Escopos OAuth dedicados (`mcp:write:contratos`, etc.)
- [ ] Validações idênticas às da REST API

**Verificação:**
- [ ] Tentativa de escrita sem escopo correto retorna 403
- [ ] Mesma `Idempotency-Key` em 24h não duplica registro

**Dependências:** 7B.4
**Estimativa:** S (decisão sponsor)

---

### Checkpoint Fase 7B
- [ ] Servidor MCP read-only em produção com 11 tools funcionando (8 estruturados + simular_antecipacao + 2 RAG)
- [ ] Agent Card A2A público + 1 skill demonstrativa
- [ ] **Camada RAG operacional**: pgvector indexando ≥ 5 contratos de teste com busca semântica funcionando
- [ ] Sponsor consulta dívida via Claude Desktop em demo formal
- [ ] **Demo RAG**: sponsor faz pergunta textual ("o que diz o contrato X sobre pré-pagamento?") via Claude Desktop e recebe trecho citado corretamente
- [ ] **Gate humano:** sponsor decide ir para tools de escrita (7B.5) agora ou postergar para Fase 2

---

### Fase 8 — Alertas, snapshots e provisão mensal (1 semana, Sprint 7)

**Objetivo:** automações temporais que sustentam a operação contínua.

#### 8.1 Alertas de vencimento

**Descrição:** D-7, D-3, D-0 para parcelas previstas. D-15 para NDFs sem rolagem. Email + Slack + dashboard.

**Critérios de aceite:**
- [ ] Cron job diário às 8h
- [ ] Configuração de canais por usuário (preferência)
- [ ] Suprimir alertas redundantes (já enviado hoje)

**Verificação:**
- [ ] Parcela D-3 dispara email + Slack 1 vez

**Dependências:** Fase 7
**Estimativa:** S

#### 8.2 Alertas de limite de crédito por banco

**Descrição:** quando saldo + operações em curso > 80% do limite por banco → alerta. Ao atingir 100% → alerta crítico.

**Critérios de aceite:**
- [ ] Cadastro de limite por banco (admin)
- [ ] Recalculado automaticamente após cada cadastro/baixa de contrato
- [ ] Endpoint `GET /api/v1/painel/limites` com semáforo

**Verificação:**
- [ ] Cadastrar contrato que ultrapassa 80% → alerta
- [ ] 100% bloqueia novo cadastro (ou requer aprovação admin)

**Dependências:** 8.1
**Estimativa:** M

#### 8.3 Snapshot mensal posicional

**Descrição:** último dia útil do mês, gerar `POSICAO_SNAPSHOT` de cada contrato com PTAX D0 oficial. Imutável (auditoria).

**Critérios de aceite:**
- [ ] Job mensal idempotente
- [ ] Snapshot reproduzível (consulta retorna mesmo valor sempre)
- [ ] Disponível via `GET /api/v1/contratos/{id}/posicao?data=2026-04-30`

**Verificação:**
- [ ] Snapshot do mês 04/2026 gerado, valores conferem com `CalculadorSaldo` na data

**Dependências:** 8.2
**Estimativa:** S

#### 8.4 Provisão mensal de juros (lançamentos gerenciais)

**Descrição:** Anexo A seção 8 + Anexo B seção 6.10. Job mensal calcula juros provisionados e gera lançamentos no plano de contas gerencial. Reverte na liquidação efetiva.

**Critérios de aceite:**
- [ ] Lançamentos gerenciais com débito/crédito por conta
- [ ] Reversão automática quando juros são pagos
- [ ] Endpoint `GET /api/v1/lancamentos?periodo=2026-04` exporta para Excel (formato livre, gerencial)

**Verificação:**
- [ ] Janeiro/2026 do exemplo BB do Anexo B seção 6.10 — débito 3.2.1 = USD 5.166,67 = R$ 27.383,33 (com PTAX 5,30)
- [ ] Pagamento em 30/06/2026 reverte provisões acumuladas

**Dependências:** 8.3
**Estimativa:** M

---

### Checkpoint Fase 8
- [ ] Alertas operacionais ativos sem ruído (taxa de falso positivo < 5%)
- [ ] Snapshot mensal automatizado
- [ ] Provisões geradas e reconciliáveis com extrato bancário/contábil
- [ ] **Gate humano:** sponsor + contabilidade validam lançamentos do mês

---

### Fase 9 — Migração de 1.200+ contratos + UAT + Go-live (2 semanas, Sprint 8)

**Objetivo:** sistema em produção com a base histórica completa, planilha aposentada.

#### 9.0 Decisão final: ETL determinístico vs Agente Migrador (LLM)

**Descrição:** com base na análise de qualidade dos dados feita na Fase B (Stream M arrancou cedo), tomar a decisão registrada no ADR-011: usar ETL puro (.NET console app) **ou** acoplar um Agente Migrador (Gemini Fast) que extrai/normaliza linhas problemáticas.

**Critérios de aceite:**
- [ ] Relatório de qualidade da planilha apresentado pelo Stream M (% campos preenchidos, % linhas estruturadas, padrões de erro)
- [ ] Decisão registrada (sim/não para Agente Migrador)
- [ ] Se sim: arquitetura definida — Agente extrai → SGCF valida → Aprovação humana → POST API

**Verificação:**
- [ ] Decisão e justificativa em ata; orçamento de tokens estimado se "sim"

**Dependências:** Fase 8 + Stream M maduro
**Estimativa:** S (decisão executiva)

#### 9.1 ETL planilha → SGCF

**Descrição:** ferramenta de migração (.NET console app) lê planilha (DÍVIDA_2026 + RESUMO_ENDIVIDAMENTO), aplica regras de mapping, importa contratos via API. Se 9.0 = "sim", o ETL invoca o Agente Migrador via MCP/A2A para extração de linhas problemáticas.

**Critérios de aceite:**
- [ ] Parser robusto (skip linhas sujas, log de cada erro)
- [ ] Mapeamento documentado linha-a-linha (planilha → schema)
- [ ] Modo `--dry-run` que valida sem persistir
- [ ] Relatório final: total importado, total rejeitado com motivo

**Verificação:**
- [ ] Dry-run em planilha completa — taxa de erro < 5%
- [ ] Importação real em ambiente staging — total contratos importados ≥ 95% do esperado

**Dependências:** Stream M (limpeza de dados controladoria, em paralelo desde Sprint 0)
**Estimativa:** L (script + validação + reconciliação manual)
**Arquivos prováveis:** `tools/Migracao/Program.cs`

#### 9.2 Reconciliação de saldos

**Descrição:** script que compara saldo total da planilha (data de corte) vs saldo calculado pelo SGCF na mesma data — flag divergências > R$ 1.000 ou > 0,1%.

**Critérios de aceite:**
- [ ] Relatório de divergências com drill-down até contrato
- [ ] Divergências aprovadas (sponsor) registradas com observação no SGCF

**Verificação:**
- [ ] Reconciliação mostra ≥ 99% de match em saldo BRL total
- [ ] Cada divergência tem causa documentada

**Dependências:** 9.1
**Estimativa:** M

#### 9.3 Dual-run controlado

**Descrição:** 2 semanas operando sistema + planilha em paralelo. Tesouraria cadastra novas operações em ambos. Comparação semanal.

**Critérios de aceite:**
- [ ] 2 semanas de dual-run sem divergências críticas
- [ ] Ata semanal de comparação assinada por sponsor

**Verificação:**
- [ ] Última semana do dual-run: zero divergências críticas

**Dependências:** 9.2
**Estimativa:** M (calendário, pouco código)

#### 9.4 UAT formal

**Descrição:** roteiro de 30-50 cenários (criados pela controladoria desde Sprint 4) executado por usuários reais. Bug tracking dedicado.

**Critérios de aceite:**
- [ ] 100% dos cenários P0/P1 passam
- [ ] Bugs P0 resolvidos antes do go-live
- [ ] Bugs P2/P3 priorizados em backlog pós-MVP

**Verificação:**
- [ ] Sign-off formal do sponsor + tesouraria

**Dependências:** 9.3
**Estimativa:** M

#### 9.5 Documentação + runbooks + treinamento

**Descrição:** documentação operacional para o squad (runbook de incidentes, deploy, restore) + manual do usuário (tesouraria, gerente, diretor).

**Critérios de aceite:**
- [ ] 5 runbooks (deploy, rollback, restore DB, incident response, alertas)
- [ ] Manual do usuário com screenshots e fluxos principais
- [ ] Sessão de treinamento gravada (vídeo de referência)

**Verificação:**
- [ ] Dev faz teste de restore DB seguindo apenas o runbook → sucesso
- [ ] Sponsor + 2º analista de tesouraria executam fluxo completo após treinamento

**Dependências:** 9.4
**Estimativa:** M

#### 9.5b Indexação batch dos 1.200 contratos no pgvector (RAG)

**Descrição:** rodar o `tools/IndexadorContratos/` (criado em 7B.6) em modo batch sobre os PDFs migrados em 9.1. Gera embeddings de cada cláusula, persiste em `clausula_contratual` com metadata correta. Aproveita o momento da migração para popular a camada RAG sem custo adicional de operação posterior.

**Critérios de aceite:**
- [ ] ≥ 95% dos contratos com PDF disponível têm cláusulas indexadas
- [ ] Contratos sem PDF (apenas dados na planilha) ficam como `indexado = false` para indexação futura
- [ ] Custo total de embeddings em batch documentado (esperado < R$ 50 one-time)
- [ ] Verificação por amostragem: 10 contratos aleatórios — busca por cláusula conhecida retorna o resultado correto

**Verificação:**
- [ ] Total de chunks indexados > 50.000
- [ ] Busca de spot check: "break funding fee" retorna apenas contratos com essa cláusula
- [ ] Custo real de embeddings dentro do esperado

**Dependências:** 9.1 (contratos importados), 7B.6 (infra RAG operacional)
**Estimativa:** S (script roda uma vez; tempo dominado por throughput da API de embeddings)

#### 9.6 Go-live e aposentadoria da planilha

**Descrição:** congelar planilha (read-only), comunicar stakeholders, monitorar 1ª semana de produção com on-call reforçado.

**Critérios de aceite:**
- [ ] Planilha em modo read-only com nota redirecionando para SGCF
- [ ] Comunicação enviada para diretoria, contabilidade, auditoria
- [ ] On-call do dev em horário comercial na 1ª semana

**Verificação:**
- [ ] 1 semana de operação com SLA ≥ 99% de uptime
- [ ] Zero rollbacks para a planilha

**Dependências:** 9.5
**Estimativa:** S

---

### Checkpoint final — MVP em produção
- [ ] 1.200+ contratos importados e conferidos
- [ ] Planilha aposentada
- [ ] Sponsor faz fechamento de mês usando apenas o SGCF
- [ ] Documentação completa
- [ ] Backlog Fase 2 (integração SAP B1) iniciado

---

## 3. Streams paralelos (controladoria, sem bloquear dev)

### Stream M — Migração e qualidade de dados (Sprints 0 a 8)

| Atividade | Quando | Responsável |
|---|---|---|
| Limpeza inicial da planilha (eliminar duplicidades, normalizar bancos) | Sprint 0-2 | Controladoria 1 |
| Preencher "A confirmar" em BANCO_CONFIG (REFINIMP, % CDB, prazo Sicredi/Daycoval/Santander) | Sprint 0-1 | Controladoria 2 |
| **Coletar contratos pendentes para Anexo C (BB 4131 termo, Itaú FINIMP CPGI, REFINIMP Itaú, Santander 4131, Bradesco/Daycoval/Safra/ABC)** | **Sprint 0-1** | **Tesouraria** |
| **Coletar 3-5 emails reais com cotação de break funding fee BB FINIMP** (componente C7 Anexo C) | **Sprint 0-1** | **Tesouraria** |
| **Coletar 2-3 cotações reais BV de taxa de mercado para FGI** (componente C5 Anexo C) | **Sprint 1** | **Tesouraria** |
| Anexar contratos do Santander 4131 (Google Drive) — pendência aberta no Anexo B seção 9 | Sprint 0 | Welysson |
| Coleta de golden dataset (5-10 contratos por modalidade com cálculos validados) | Sprint 1-3 | Controladoria 1+2 |
| Mapeamento planilha → schema documentado | Sprint 2-3 | Controladoria 1 |
| Validação de regras tributárias (IRRF Lux 15% vs 25%, jurisdições) | Sprint 2 | Welysson |
| Definição de plano de contas gerencial final | Sprint 3-4 | Controladoria + sponsor |

### Stream Q — Qualidade e UAT (Sprints 4 a 8)

| Atividade | Quando | Responsável |
|---|---|---|
| Roteiros de UAT (30-50 cenários) | Sprint 4-5 | Controladoria 2 |
| Casos de teste para o motor de cálculo (golden tests) | Sprint 1-3 | Controladoria 1 + dev |
| Validação cruzada com analista contra cálculo manual em ≥ 20 contratos | Sprint 5-7 | Controladoria 1+2 |
| Bug bash semanal a partir da Sprint 5 | Sprint 5-8 | Squad inteiro |

### Stream A — Habilitação de Agentes (Sprints 1 a 7, leve)

Stream complementar para preparar a Fase 2 sem competir com o build core.

| Atividade | Quando | Responsável |
|---|---|---|
| Catalogar capacidades do SGCF a expor via MCP/A2A (lista priorizada de tools) | Sprint 1-2 | Controladoria + sponsor |
| Specs detalhadas dos 8 tools MCP read-only (input/output, exemplos) | Sprint 3-4 | Sponsor + dev (consult) |
| Cenários de uso de Tesouraria via Claude Desktop (5-10 perguntas típicas) | Sprint 4-5 | Sponsor + Controladoria |
| Avaliação contínua da qualidade da planilha (decisão Agente Migrador) | Sprint 0-7 | Stream M alimenta Stream A |
| Backlog Fase 2 — agentes funcionais (Comparador, Gravador, Parecerista) | Sprint 6-7 | Sponsor + futuro arquiteto de agentes |

---

## 4. Riscos e mitigações priorizados

| # | Risco | Prob × Impacto | Mitigação |
|---|---|---|---|
| 1 | **1 dev como SPOF em sistema financeiro complexo** | Alta × Crítico | Documentação contínua, pair-programming com sponsor em decisões-chave, reforço pontual orçado, frameworks opinionated (.NET 8 + libs maduras) — ADR-002, ADR-007 |
| 2 | **Motor de cálculo com bugs sutis (1 centavo errado em juros pode propagar)** | Média × Crítico | Cobertura ≥ 95% no motor, golden dataset desde Fase 0, property-based tests, code review obrigatório por sponsor + 2º controladoria nas estratégias de cálculo |
| 3 | **MTM dos NDFs adiciona 1-2 semanas ao cronograma** (ADR-010 já flagga) | Alta × Médio | Buffer de 1 semana já incluído na Fase 6; se estourar, postergar Simulador (7.6) e Dashboard executivo (7.5) para pós-MVP |
| 4 | **Migração com 1.200+ contratos sujos** | Alta × Alto | Stream M arranca na Sprint 0; dry-runs sucessivos; reconciliação automática; aceitar < 5% de divergências documentadas |
| 5 | **PTAX BCB indisponível em momento crítico** | Baixa × Alto | Cache Redis, fallback para D-1, alerta de staleness, retry exponencial |
| 6 | **Resistência da contabilidade ao "plano de contas gerencial separado"** | Média × Médio | Comunicação clara desde a Fase 1: SGCF é fonte gerencial, SAP segue oficial; Fase 2 fará a ponte (ADR-008) |
| 7 | **Front-end demora a ser definido e atrasa a Sprint 1** | Média × Alto | Decisão na Fase B.4 (gate antes da Sprint 0); se atrasar, dev arranca Sprint 0 e Fase 1 (puro backend) sem front-end |
| 8 | **Vazamento de dados sensíveis (contratos)** | Baixa × Crítico | CMEK, LGPD pelo design, audit log, RBAC granular, mascaramento em logs (ADR-006) |
| 9 | **Cobertura de testes sacrificada por pressão de cronograma** | Alta × Crítico | CI gate hard: PR não merge sem coverage ≥ 80% (95% no motor de cálculo) |
| 10 | **Mudança de regra fiscal (IRRF, IOF) durante o build** | Média × Médio | Tabelas de configuração com vigência temporal (já modelado em PARAMETRO_COTACAO); IRRF/IOF modelados como configuração, não código |
| 11 | **.NET 11 ainda em preview no início da Sprint 0** | Média × Alto | Fallback documentado para .NET 10 LTS (ADR-003 v1.1); decisão definitiva no fim da Fase B; troca entre versões = atualização de TFM |
| 12 | **Spec MCP ou A2A muda durante o build** | Média × Médio | Spike na Fase B.5 valida versão; pinning de versão da spec; isolar adapter MCP/A2A em projetos próprios (`Sgcf.Mcp`, `Sgcf.A2a`) |
| 13 | **Squad subestima esforço de MCP/A2A (tecnologia nova)** | Média × Médio | Spike obrigatório na Fase B.5 antes de comprometer Fase 7B; buffer de 2-3 dias na 7B |
| 14 | **Tools MCP de escrita abrem vetor de risco operacional** | Média × Alto | Manter 7B.5 opcional; se executar, exigir review humano por contrato; idempotência + audit reforçados |

---

## 5. Decisões em aberto que precisam de input humano

| # | Decisão | Quem decide | Quando |
|---|---|---|---|
| 1 | Stack do front-end headless (Next.js / Nuxt 3 / Blazor / outro) | Sponsor + dev | Fase B.4 |
| 2 | Perfil exato do dev (sênior .NET generalista vs especialista financeiro) | Sponsor + TI | Antes Sprint 0 |
| 3 | Reforço pontual de dev em picos (sim/não, qual orçamento) | Sponsor | Conforme necessário |
| 4 | Política exata de retenção LGPD para contratos e dados pessoais | Sponsor + Compliance | Fase B.4 |
| 5 | Critérios de UAT (cenários obrigatórios, métricas de aceite, quem assina) | Sponsor + Tesouraria | Sprint 4 |
| 6 | Particularidades de NDFs específicos (alguns podem usar média de PTAX em vez de D0) — Anexo A 8.6 | Welysson | Fase 6 |
| 7 | Layout final do Excel exportado da tabela completa (compatibilidade SAP B1 — Fase 2) | Sponsor + contabilidade | Sprint 7 |
| 8 | Lista definitiva de contas gerenciais (Anexo A 8.5 é sugestão) | Sponsor | Fase 1 |
| 9 | Definição de SLA produção (uptime, RTO, RPO) | Sponsor + TI | Sprint 7 |
| 10 | Política de "auto-post vs review-then-post" para lançamentos (relevante na Fase 2) | Sponsor + contabilidade | Pós-MVP |
| 11 | **Versão definitiva do .NET (.NET 11 GA vs .NET 10 LTS)** | Sponsor + dev | Final Fase B |
| 12 | **SDK MCP (Anthropic oficial vs implementação própria)** | Dev + sponsor | Sprint 0 |
| 13 | **Versão da spec A2A a adotar** | Dev | Sprint 0 |
| 14 | **Inclusão do Agente Migrador na Fase 1** (depende da qualidade da planilha) | Sponsor + dev | Final Fase B / Fase 9.0 |
| 15 | **Tools MCP de escrita no MVP** (7B.5) ou postergar para Fase 2 | Sponsor | Após Checkpoint Fase 7B |

---

## 6. Critérios de pronto (Definition of Done) por tarefa

Para qualquer tarefa ser considerada concluída:

- [ ] Código em PR mergeado com revisão
- [ ] **Princípios de codificação aplicados (ADR-014):** clean code, simple > clever, sem cleverness sem justificativa
- [ ] **Métricas de qualidade no CI passam:** complexidade ciclomática <10, arquivo <400 linhas, duplicação <3% (alertas — não bloqueiam, mas justificar)
- [ ] Testes unitários ≥ 80% (95% para motor de cálculo)
- [ ] Testes de integração para endpoints novos
- [ ] OpenAPI atualizado (REST) **+ schemas MCP atualizados** quando tool for adicionado/modificado
- [ ] Audit log validado para entidades novas (com `source` correto: `rest`, `mcp`, `a2a`)
- [ ] Logs estruturados sem PII
- [ ] Migration aplicada em dev e staging
- [ ] Documentação de uso atualizada (manual usuário, runbook ops, **catálogo MCP** quando aplicável)
- [ ] Demo em ambiente staging para sponsor
- [ ] Métricas custom expostas (quando aplicável)
- [ ] Para tarefas no caminho de cálculo financeiro: **funções puras**, sem dependência de tempo/contexto fora dos parâmetros

---

## 7. Métricas de sucesso do MVP

### Técnicas
- Cobertura geral ≥ 80% / motor de cálculo ≥ 95%
- p99 de endpoints de leitura < 500ms
- Uptime ≥ 99% em produção
- Zero incidentes P0/P1 nos primeiros 30 dias

### Negócio (Business Case seção 9.1, calibrado)
- Tempo de fechamento mensal: 3-4 dias → 0,5-1 dia
- Tempo de relatório executivo: 4-8h → < 30min
- Erros de conciliação: 2-5/mês → < 1/trimestre
- Tempo de resposta a auditoria: 2-5 dias → < 1h
- Capacidade de simulação de cenário: inexistente → segundos

### Adoção
- 100% dos cadastros novos via SGCF a partir do go-live
- Planilha em modo read-only após Sprint 8
- Sponsor faz 100% do fechamento mensal no SGCF a partir do mês +1

---

## 8. Revisão e governança do plano

- **Revisão semanal**: sponsor + dev revisam progresso e bloqueios (15 min)
- **Revisão de checkpoint**: ao final de cada Fase, gate humano formal (sponsor decide go/no-go)
- **Atualização do plano**: este documento é vivo — atualizar quando decisões mudarem (registrar em changelog no fim do arquivo)

---

## Changelog

- **v1.0 — 08/maio/2026**: versão inicial baseada em Business Case + Anexo A + Anexo B + ADR.
- **v1.1 — 08/maio/2026**: incorporação dos requisitos adicionais do sponsor:
  - Stack atualizada para .NET 11 (com fallback .NET 10 LTS)
  - Estratégia de Agentes (Fase 1 = backend agent-ready; Fase 2 = agentes funcionais)
  - Servidor MCP read-only no MVP (8 tools) — ADR-012
  - Baseline A2A (Agent Card + 1 skill demo) — ADR-013
  - Princípios de codificação adicionados (clean code, simple > clever) — ADR-014
  - Nova Fase 7B (~1,5 sem) → cronograma 22 → ~24 semanas
  - Tarefa 9.0 — decisão sobre Agente Migrador
  - Spike B.5 — POC MCP/A2A na Fase de Arquitetura
  - Stream A — Habilitação de Agentes
  - Riscos 11-14 adicionados (preview .NET, evolução de specs, esforço subestimado, tools de escrita)
  - DoD reforçado com métricas de qualidade e schemas MCP
- **v1.4 — 10/maio/2026**: inclusão da **camada RAG complementar com pgvector** (ADR-015):
  - Sprint 0.2 ampliada: habilitar extension `pgvector` no Cloud SQL
  - Nova tarefa **7B.6** Camada RAG (~3-5 dias) — schema `clausula_contratual`, parser de PDF por cláusula, 2 tools MCP (`buscar_clausula_contratual`, `comparar_clausulas`)
  - Tools MCP totalizam **11** (8 originais + simular_antecipacao + 2 RAG)
  - Nova tarefa **9.5b** Indexação batch dos 1.200 contratos durante a migração
  - Checkpoint Fase 7B inclui demo RAG via Claude Desktop
- **v1.3 — 10/maio/2026**: inclusão do **Anexo C — Regras de Antecipação de Pagamento**:
  - Nova tarefa **4.4 Motor de antecipação de pagamento** (Strategy pattern com 5 padrões A-E)
  - Tarefa **7.6 Simulador** ampliada com simulação de antecipação de portfolio
  - **9º tool MCP** `simular_antecipacao` adicionado à Fase 7B
  - Stream M ganhou 3 atividades de coleta (contratos pendentes, cotações reais de break funding e taxa de mercado FGI)
  - Critérios de aceite incluem **golden tests dos 5 padrões** (BB FINIMP, Sicredi FINIMP, FGI BV, Caixa Balcão, Caixa abatimento prefixado)
  - Alerta crítico obrigatório para Sicredi FINIMP: "antecipação não economiza juros — banco cobra período total"
- **v1.2 — 08/maio/2026**: inclusão do SPEC.md como documento âncora:
  - SPEC.md adicionado como input obrigatório da Fase B.1 (1ª leitura)
  - Critérios de aceite da Fase B.1 expandidos: 3 novas seções no Documento de Arquitetura (Camada MCP, Camada A2A, Aderência ao SPEC)
  - Tipos numéricos do schema PostgreSQL agora explicitamente vinculados ao SPEC §8 (`numeric(20,6)` para monetário, etc.)
  - `Prompt_Agente_Arquiteto_SGCF.md` precisa ser atualizado para v1.1 antes de B.1 — esta atualização virou parte dos critérios de aceite
  - Hierarquia de precedência entre documentos definida (SPEC > ADR > Anexos > Business Case)
  - Estimativa do Documento de Arquitetura ajustada de 12-25 → 15-30 páginas equivalente
