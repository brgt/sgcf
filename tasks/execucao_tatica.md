# Plano Tático de Execução — SGCF MVP

**Versão:** v1.0 — 10/maio/2026
**Foco:** primeiras **8 semanas** (Fase B + Sprint 0 + Sprint 1) com granularidade dia a dia, calendário absoluto e checklist de readiness.
**Documento âncora estratégico:** `tasks/plan.md` v1.4 (não substituído — este plano **complementa** com camada tática).
**Documento de requisitos:** `SPEC.md` v1.2.

---

## 1. Status atual

| Item | Status |
|---|---|
| **Data de início** | Segunda-feira, **11/maio/2026** |
| **Squad** | ✅ Sponsor (Welysson) + dev sênior + 2º controladoria — todos prontos |
| **Stack .NET** | ✅ **.NET 11** — decisão confirmada pelo sponsor em 10/maio/2026. TFM `net11.0`. |
| **Frontend** | ❌ Fora de escopo nos 3 primeiros meses (Sprint 0–5). Demos via Swagger UI + Claude Desktop (MCP) + Postman |
| **Documentação base** | ✅ SPEC v1.2, ADR v1.2, Anexos A/B/C, Plan v1.4 |
| **Próximo gate** | Documento de Arquitetura aprovado (alvo: **5/jun/2026**) |

---

## 2. Pré-kickoff — esta semana (10/mai domingo → 11/mai segunda)

Items que precisam estar prontos **antes** de Day 1 da Fase B (segunda-feira 11/mai 09h00):

### 2.1 Provisões e acessos (sponsor + TI)
- [ ] Conta Google Cloud da Proxys com **billing account** ativa
- [ ] Permissão de **Owner ou Project Creator** para o sponsor e o dev na organização GCP
- [ ] Repositório git criado (sugestão: GitHub privado `proxys/sgcf-backend`) com permissão para o squad
- [ ] Slack/canal de comunicação `#sgcf-build` criado
- [ ] Drive compartilhado `Governança/Projetos/SGCF/` com sponsor e dev
- [ ] Calendário Google: criar 4 reuniões fixas semanais (Sprint Planning, Daily, Demo, Retro — ver §7)

### 2.2 Aprovação formal
- [ ] **Patrocínio executivo** assinado pela Diretoria Financeira (pode ser email)
- [ ] **Orçamento** aprovado para Ano 1 (R$ 280-340k de build + R$ 60k operação)
- [ ] **Disponibilidade** do sponsor (Welysson) confirmada em 30-40% do tempo durante o build

### 2.3 Materiais para a Fase B
- [ ] SPEC.md, ADR, Anexos A/B/C, Plan compartilhados em formato consumível pelo agente Arquiteto (Claude Opus, Gemini Pro ou similar)
- [ ] Acesso a Claude Desktop ou plataforma equivalente para rodar agentes
- [ ] Pasta `architecture/` criada no repo para receber o Documento de Arquitetura

### 2.4 Stream M (paralelo) — pode arrancar imediatamente
- [ ] 2º controladoria recebe brief do Stream M
- [ ] Cópia da planilha atual de dívida em local seguro para análise
- [ ] Lista de "A confirmar" do BANCO_CONFIG transformada em plano de coleta

> **Gate de "go":** os 12 itens acima precisam estar ✅ até **sexta-feira 8/mai 18h00** (já passada — verificar status agora) ou **segunda-feira 11/mai 09h00**. Se algum item bloqueia, ajustar Day 1 para resolver primeiro.

---

## 3. Calendário macro com milestones

> Calendário em dias úteis, considerando feriados nacionais (Corpus Christi 4/jun, Independência 7/set, N.Sra Aparecida 12/out, Finados 2/nov, Consciência Negra 20/nov).

| Marco | Data alvo | Critério objetivo |
|---|---|---|
| **M0 — Kickoff** | seg 11/mai | Pré-kickoff §2 ✅ + reunião de arrancada |
| **M1 — Doc Arquitetura aprovado** | sex 5/jun | Doc v1.1 com 6 reviews críticos incorporados; backlog 30-50 stories no tracker |
| **M2 — Hello World em prod** | sex 19/jun | Endpoint autenticado em prod via pipeline; audit log + logs estruturados |
| **M3 — FINIMP USD bullet end-to-end** | sex 17/jul | Cadastro + cronograma + saldo + tabela completa funcionando; coverage motor ≥ 95%; **gate crítico** do sponsor |
| **M4 — 6 modalidades + motor antecipação** | sex 28/ago | Todas modalidades + 5 padrões A-E; golden tests passam; alerta crítico Sicredi funcionando |
| **M5 — NDFs + MTM em produção** | sex 25/set | Forward + Collar com MTM intraday a cada 5min; 5 alertas de exposição ativos |
| **M6 — Painéis + camadas MCP/A2A/RAG** | sex 23/out | 4 painéis + 11 tools MCP + Agent Card A2A + busca semântica em cláusulas |
| **M7 — 1.200 contratos migrados** | sex 6/nov | ETL + reconciliação ≥ 99% match + indexação batch RAG concluída |
| **M8 — MVP em produção (go-live)** | sex 20/nov | Planilha read-only; primeira reunião de comitê 100% via SGCF; SLA atingido |

**Total:** 11/mai/2026 → 20/nov/2026 = **28 semanas calendário** (~24 semanas úteis descontando feriados).

---

## 4. Fase B — Arquitetura técnica detalhada (11/mai → 5/jun) — dia a dia

### Semana 1 — 11-15/maio: Atualização do prompt + Arquiteto + Spike POC

| Dia | Data | Atividade | Quem | Saída |
|---|---|---|---|---|
| Day 1 | seg 11/mai | **Kickoff meeting** (1h): apresentação SPEC, ADR, plano, expectativas, papéis | Squad inteiro | Ata de kickoff |
| Day 1 | seg 11/mai | **B.1a** Atualizar `Prompt_Agente_Arquiteto_SGCF.md` para v1.1 (incluir SPEC, .NET 11, MCP, A2A, padrões precisão, ADR-015 RAG) | Sponsor | Prompt v1.1 commitado |
| Day 2 | ter 12/mai | **B.1b** Executar agente Arquiteto (Claude Opus); receber retorno de 200 palavras com dúvidas | Sponsor | 5 dúvidas críticas respondidas |
| Day 2-3 | ter-qua 12-13/mai | **B.1b** Arquiteto produz Documento de Arquitetura v1.0 (15-30 páginas com 18 seções) | Agente | `docs/architecture/Documento_Arquitetura_v1.md` |
| Day 3 | qua 13/mai | **B.1b** Sponsor revisa Doc v1.0; lista observações | Sponsor | Lista de ajustes |
| Day 4-5 | qui-sex 14-15/mai | **B.5** Spike POC MCP + A2A em paralelo (em sandbox local — não no repo principal) | Dev | `spikes/mcp-poc/` + `spikes/a2a-poc/` + decisão SDK escolhido |
| Day 4-5 | qui-sex 14-15/mai | **Stream M** Welysson começa: limpeza planilha + lista contratos pendentes Anexo C | Controladoria 2 | Relatório inicial |

**Saída da Semana 1:** Documento de Arquitetura v1.0 + Spike MCP/A2A com decisão técnica de SDK + Stream M iniciado.

### Semana 2 — 18-22/maio: Revisão crítica por 6 agentes

| Dia | Data | Atividade | Quem | Saída |
|---|---|---|---|---|
| Day 6 | seg 18/mai | **B.2** Disparar 6 agentes críticos em paralelo (Security, DBA, DevOps, QA, Frontend, Code Reviewer) com Doc v1.0 | Sponsor | 6 jobs em execução |
| Day 6-7 | seg-ter 18-19/mai | Agentes produzem 6 relatórios de 300-500 palavras cada | Agentes | `docs/architecture/critical_reviews/` |
| Day 8 | qua 20/mai | **B.2** Sponsor consolida lacunas críticas em backlog; classifica severidade alta/média/baixa | Sponsor | Lista priorizada |
| Day 9-10 | qui-sex 21-22/mai | **B.2** Arquiteto (em segunda rodada) incorpora lacunas críticas → Doc v1.1 | Agente | `Documento_Arquitetura_v1.1.md` |
| Paralelo | toda semana | **Stream M** Coleta de contratos pendentes (BB 4131, Itaú FINIMP CPGI, REFINIMP Itaú, Bradesco/Santander/Daycoval/Safra/ABC) | Controladoria | PDFs em `CONTRATOS_MODELOS/` |

**Saída da Semana 2:** Doc Arquitetura v1.1 com revisões incorporadas + 5+ contratos modelo a mais coletados.

### Semana 3 — 25-29/maio: Backlog + decisões pendentes

| Dia | Data | Atividade | Quem | Saída |
|---|---|---|---|---|
| Day 11-12 | seg-ter 25-26/mai | **B.3** Produzir backlog inicial: 30-50 user stories priorizadas com t-shirt size, agrupadas por épico | Sponsor + dev | Backlog em GitHub Projects (ou Linear/Jira) |
| Day 13 | qua 27/mai | **B.4** Reunião de decisões pendentes: stack frontend (mantém out-of-scope 3m), perfil dev confirmado, reforço, LGPD, UAT, SDK MCP, A2A spec, Agente Migrador | Squad + sponsor | Decisões registradas como ADR-016 a ADR-020 |
| Day 14 | qui 28/mai | **B.4** Análise de qualidade da planilha (input do Stream M) → decisão final sobre **Agente Migrador na Fase 1**: sim/não | Sponsor + controladoria | Decisão registrada |
| Day 15 | sex 29/mai | **Stream M** Welysson valida configurações `BANCO_CONFIG` "A confirmar" para Sicredi/Daycoval/Santander | Controladoria | BANCO_CONFIG seed atualizado |
| Paralelo | toda semana | **B.5** Documentação dos spikes MCP/A2A + recomendação técnica registrada | Dev | Doc de spikes |

**Saída da Semana 3:** Backlog priorizado + 5 decisões pendentes resolvidas + qualidade da planilha avaliada + Stream M maduro.

### Semana 4 — 1-5/junho: Buffer + aprovação final + setup Sprint 0

> **Atenção:** Corpus Christi cai em **quinta 4/jun/2026** — feriado nacional. Semana com 4 dias úteis.

| Dia | Data | Atividade | Quem | Saída |
|---|---|---|---|---|
| Day 16 | seg 1/jun | **B.4** Ajustes finais no Doc Arquitetura v1.1 conforme feedback dos 6 reviews | Sponsor + agente | Doc v1.1 final |
| Day 17 | ter 2/jun | **B.4** Validação final do `BANCO_CONFIG` seed completo (10 bancos × modalidades × campos antecipação) | Controladoria + dev | Seed JSON validado |
| Day 18 | qua 3/jun | **Setup do Sprint 0** Provisão GCP inicial (projetos dev/staging/prod), billing alerts, IAM roles draft | Dev | Terraform stub |
| FERIADO | qui 4/jun | Corpus Christi | — | — |
| Day 19 | sex 5/jun | **Sprint Planning Sprint 0** + cerimônia de **aprovação do Doc Arquitetura v1.1** pelo sponsor | Squad + sponsor | **M1 atingido** ✅ + sprint board pronto |

**Saída da Semana 4:** Documento de Arquitetura aprovado, Sprint 0 com board pronto, setup de GCP iniciado, time alinhado para Sprint 0 segunda-feira.

### Definition of Done da Fase B (gate para Sprint 0)
- [ ] Doc Arquitetura v1.1 aprovado pelo sponsor (M1)
- [ ] 6 reviews críticos arquivados em `docs/architecture/critical_reviews/`
- [ ] Backlog 30-50 stories priorizadas no tracker
- [ ] 5 decisões pendentes registradas como ADR
- [ ] Spike MCP/A2A decidiu SDK; spikes arquivados em `spikes/`
- [ ] Stream M produziu relatório de qualidade da planilha
- [ ] Sprint 0 board pronto com 7 stories prioritárias
- [ ] **Gate humano:** sponsor aprova "go" para Sprint 0

---

## 5. Sprint 0 — Infra e fundações (8/jun → 19/jun) — dia a dia

**Sprint Goal:** "Hello World autenticado em produção com audit log, observabilidade e CI/CD." 10 dias úteis.

### Semana 5 — 8-12/junho

| Dia | Data | Story | Tarefas | Critério de pronto do dia |
|---|---|---|---|---|
| Day 1 | seg 8/jun | **0.1** GCP setup | Provisão Terraform de 3 projetos (dev/staging/prod) + IAM básico + billing alerts | `gcloud projects list` mostra 3 projetos |
| Day 2 | ter 9/jun | **0.1** GCP setup | Service accounts + redes + firewall rules + cross-project deny test | Tentativa cross-projeto retorna 403 |
| Day 3 | qua 10/jun | **0.2** Cloud SQL + pgvector | Provisão Cloud SQL Postgres 16 com CMEK; **habilitar `pgvector`**; backup config | `SELECT * FROM pg_extension WHERE extname='vector'` retorna 1 linha |
| Day 4 | qui 11/jun | **0.2** Memorystore + Storage | Redis 1GB + Cloud Storage bucket com versionamento + retenção 5 anos | Conexão de teste do laptop OK |
| Day 5 | sex 12/jun | **0.3** Solution .NET | Solution skeleton com 7 projetos (Domain/Application/Infrastructure/Api/Mcp/A2a/Jobs); Directory.Build.props; analyzers | `dotnet build` passa em CI local |

### Semana 6 — 15-19/junho

| Dia | Data | Story | Tarefas | Critério de pronto do dia |
|---|---|---|---|---|
| Day 6 | seg 15/jun | **0.3** EF Core + migrations | Migration inicial cria `__schema_history` + tabela placeholder; health endpoints `/health/live` e `/health/ready` | Endpoints retornam 200 localmente |
| Day 7 | ter 16/jun | **0.4** Auth OAuth + RBAC | Identity Platform GCP setup; JWT validation; 6 policies (tesouraria/contabilidade/gerente/diretor/auditor/admin) | `/api/v1/me` autenticado retorna claims |
| Day 8 | qua 17/jun | **0.5** Audit interceptor | EF Core SaveChangesInterceptor → tabela `audit_log` com before/after JSONB + correlation id + source | Teste integração: CRUD dummy gera 3 linhas |
| Day 9 | qui 18/jun | **0.6** CI/CD Cloud Build | Pipeline com 12 steps (build, test, lint, container, deploy dev, smoke, deploy staging, manual gate prod, rollback) | PR de Hello World chega em staging via pipeline |
| Day 10 | sex 19/jun | **0.7** Observabilidade + checkpoint | Serilog estruturado + OpenTelemetry/Cloud Trace + dashboard "SGCF API Health" + 3 alertas; **demo formal ao sponsor** | **M2 atingido** ✅ |

### Definition of Done da Sprint 0
- [ ] Hello World autenticado em prod via pipeline (M2)
- [ ] `/health/live`, `/health/ready`, `/health/startup` retornam 200 em dev/staging/prod
- [ ] Audit log captura CRUD em entidade dummy com `source: "rest"`
- [ ] Logs estruturados visíveis no Cloud Logging com correlation id
- [ ] Trace distribuído visível no Cloud Trace
- [ ] Dashboard com 3 alertas ativos (5xx > 1%, p99 > 2s, deploy fail)
- [ ] Coverage do skeleton ≥ 80% (mesmo que tests sejam triviais ainda)
- [ ] Rollback testado: revisão anterior restaurada em < 1min
- [ ] Sprint Demo realizada para sponsor; sprint Retrospective feita
- [ ] **Gate humano:** sponsor aprova "go" para Sprint 1

---

## 6. Sprint 1 — Cadastros base + start FINIMP (22/jun → 3/jul) — stories

**Sprint Goal:** "Cadastrar um contrato de FINIMP com extensão polimórfica e validações."
10 dias úteis.

### 6.1 Stories priorizadas

| # | Story | T-shirt | Critério de pronto |
|---|---|---|---|
| **1.1** | Como admin, posso cadastrar bancos com configuração polimórfica (BANCO + BANCO_CONFIG) | M | Endpoint POST/GET/PUT `/api/v1/bancos`; seed com 10 bancos + configs antecipação (Anexo C); audit log captura mudanças |
| **1.2** | Como contabilidade, posso ver o plano de contas gerencial em estrutura hierárquica | S | Endpoint `GET /api/v1/plano-contas` retorna árvore aninhada das ~30 contas do Anexo A |
| **1.3** | Como sistema, resolve qual cotação aplicar baseado no momento da operação (PARAMETRO_COTACAO) | M | Função `ResolveTipoCotacao(momento, banco?, modalidade?, data)` com testes unitários cobrindo 10+ cenários |
| **1.4** | Como dev, tenho schema CONTRATO master + FINIMP_DETAIL com constraints e índices | M | Migration EF Core; índices em `banco_id`, `status`, `modalidade`, `data_vencimento`; sequência `codigo_interno` formato `FIN-YYYY-NNNN` |
| **1.5** | Como tesouraria, posso cadastrar um contrato FINIMP via API com validações | M | POST `/api/v1/contratos` com `Idempotency-Key`; FluentValidation para regras Anexo B 2.1 + 4.2; OpenAPI 3.1 publicado |
| **1.6** | Como tesouraria, recebo erro padronizado RFC 7807 quando validação falha | S | Tentativa de FINIMP em Bradesco com REFINIMP retorna 400 com erro estruturado |

**Story points totais (estimativa):** ~32 pontos para 10 dias úteis (média 3,2 pts/dia para squad de 1 dev sênior).

### 6.2 Distribuição diária (sugestão — squad pode ajustar)

| Dia | Data | Stories |
|---|---|---|
| Day 1 | seg 22/jun | 1.1 — schema BANCO + migration + seed |
| Day 2 | ter 23/jun | 1.1 — endpoint CRUD + audit + testes |
| Day 3 | qua 24/jun | 1.2 — plano de contas + seed |
| Day 4 | qui 25/jun | 1.3 — PARAMETRO_COTACAO schema |
| Day 5 | sex 26/jun | 1.3 — função ResolveTipoCotacao + testes |
| Day 6 | seg 29/jun | 1.4 — schema CONTRATO master |
| Day 7 | ter 30/jun | 1.4 — FINIMP_DETAIL extension + migration |
| Day 8 | qua 1/jul | 1.5 — endpoint POST contratos + validation |
| Day 9 | qui 2/jul | 1.5 — testes de integração + idempotência |
| Day 10 | sex 3/jul | 1.6 — error envelope + demo + retro |

### 6.3 Definition of Ready (DoR) — antes de pegar a story

- [ ] Story tem critério de aceite explícito (testável)
- [ ] Story tem t-shirt size estimado pelo dev
- [ ] Dependências resolvidas
- [ ] Mockup/contrato de API em OpenAPI já desenhado quando aplicável

### 6.4 Definition of Done (DoD) — para mergear story

(Conforme SPEC §6 — resumo dos pontos não-negociáveis)
- [ ] Código em PR com revisão (sponsor revisa em até 24h)
- [ ] Testes unitários ≥ 80% (95% se motor financeiro)
- [ ] Testes de integração com Testcontainers (sem mocks no domínio)
- [ ] OpenAPI atualizado
- [ ] Audit log validado (campo `source` correto)
- [ ] Logs sem PII; CPF/CNPJ mascarados
- [ ] Migration aplicada em dev e staging via pipeline
- [ ] Princípios ADR-014 aplicados (clean code, simple > clever)
- [ ] Métricas qualidade CI: complexidade < 10, arquivo < 400 linhas, duplicação < 3%
- [ ] DoD checklist anexada no PR

---

## 7. Cadência de sprints e rituais

### 7.1 Calendário fixo de cerimônias

| Cerimônia | Quando | Duração | Quem | Output |
|---|---|---|---|---|
| **Sprint Planning** | Segunda 1º dia da sprint, 09h-10h30 | 1h30 | Squad + sponsor | Sprint goal + stories committed |
| **Daily Standup** | Toda terça-quinta-sexta, 09h00-09h15 | 15min | Squad | Bloqueios identificados |
| **Mid-sprint Review** | Quarta meio da sprint, 14h-14h30 | 30min | Squad + sponsor | Course-correct se necessário |
| **Sprint Demo** | Sexta último dia da sprint, 15h-16h | 1h | Squad + sponsor + stakeholders | Demo gravada do que foi entregue |
| **Sprint Retro** | Sexta último dia, 16h-17h | 1h | Squad | Action items para próxima sprint |

### 7.2 Métricas acompanhadas

| Métrica | Frequência | Owner |
|---|---|---|
| **Velocity** (story points concluídos / sprint) | Por sprint | Sponsor |
| **Burn-down** | Diário no daily | Squad |
| **Coverage geral / motor financeiro** | A cada PR | Dev |
| **Bugs P0/P1 abertos** | Diário | Sponsor |
| **% tempo do sponsor alocado ao projeto** | Semanal | Welysson |
| **Risk register status** | Semanal no demo | Sponsor |

### 7.3 Comunicação com stakeholders

| Stakeholder | Frequência | Canal | Conteúdo |
|---|---|---|---|
| **Diretoria Financeira** | Mensal | Email + reunião 30min | Status macro, milestones, riscos críticos |
| **Contabilidade** | Quinzenal | Reunião 30min | Plano de contas, conciliação, integração SAP futura |
| **Auditoria interna** | Trimestral | Reunião 1h | Audit trail, compliance LGPD |
| **TI** | Quinzenal | Reunião 30min | Infra, observabilidade, segurança, custos GCP |
| **Squad** | Diário | Slack `#sgcf-build` | Operacional |
| **Sponsor (Welysson)** | Diário (envolvido) | Slack + cerimônias | Tudo |

---

## 8. Riscos das primeiras 8 semanas

| # | Risco | Prob × Impacto | Mitigação imediata | Owner |
|---|---|---|---|---|
| 1 | **Atraso na provisão do GCP** (depende de TI) | Média × Alto | Solicitar billing/IAM **HOJE 10/mai**; se até dia 11/mai não estiver pronto, escalar à diretoria | Sponsor |
| 2 | **Documento de Arquitetura precisar de 2+ iterações** | Alta × Médio | Buffer da Semana 4 absorve 1 iteração extra; se mais que isso, postergar Sprint 0 em 1 semana — não há atalho aqui | Sponsor |
| 3 | **Dev sênior não disponível em tempo integral nas semanas 5-6** | Média × Alto | Confirmar 100% dedicação do dev nas 4 primeiras sprints; alinhar com gestor de origem do dev | Sponsor + dev manager |
| 4 | **Spike MCP/A2A revela problemas técnicos** | Média × Médio | Buffer de 2-3 dias na Semana 1; se SDK MCP oficial não funciona em .NET, fallback para implementação própria sobre spec MCP HTTP+SSE | Dev |
| 5 | **Stream M descobre planilha mais "suja" do que esperado** | Alta × Médio | Estender semana de coleta; isso afeta decisão Agente Migrador (B.4) e migração da Fase 9, mas **não bloqueia Sprint 0/1** | Controladoria 2 |
| 6 | **Sponsor não consegue manter 30-40% do tempo** | Alta × Crítico | Bloquear no calendário desde já; se outras demandas surgirem, **proteger o tempo SGCF** — está no caminho crítico de toda decisão | Welysson |
| 7 | **Custos GCP estourarem no Sprint 0 por configuração errada** | Baixa × Médio | Alerta de orçamento em 80%; revisar gastos diariamente nos primeiros 3 dias do Sprint 0 | Dev |
| 8 | **Decisões pendentes ADR-016+ não convergirem em 1 reunião** | Média × Médio | Pré-disseminar opções 48h antes da reunião; cada decisão tem owner + decisor único | Sponsor |
| 9 | ~~**.NET 11 ainda em preview no fim da Fase B**~~ — **RISCO ENCERRADO** | — | Sponsor decidiu .NET 11 em 10/mai/2026. Código iniciado diretamente em `net11.0`. | — |
| 10 | **Backlog 30-50 stories → 80+ stories após reviews** | Alta × Baixo | Priorização rigorosa: MVP-mandatory vs nice-to-have; nice-to-have vai para Fase 2 sem cerimônia | Sponsor |

---

## 9. Pontos de decisão críticos das primeiras 8 semanas

| # | Decisão | Quem decide | Quando | Default se não decidir |
|---|---|---|---|---|
| D1 | SDK MCP escolhido | Dev + sponsor | Final Semana 1 (B.5) | Implementação própria sobre spec HTTP+SSE |
| D2 | Versão da spec A2A | Dev | Final Semana 1 (B.5) | Última estável da spec Google A2A |
| D3 | Inclusão Agente Migrador na Fase 1 | Sponsor + dev | Day 14 (qui 28/mai) | NÃO — ETL determinístico basta |
| D4 | ~~Versão final do .NET~~ | ~~Sponsor + dev~~ | ~~Day 19~~ | **DECIDIDO: .NET 11** ✅ (10/mai/2026) |
| D5 | Política exata LGPD anonimização | Sponsor + Compliance | Day 13 (qua 27/mai) | Substituir CPF por hash SHA-256 nos registros, manter audit |
| D6 | Tools MCP de escrita no MVP (7B.5) | Sponsor | Após Checkpoint Fase 7B | NÃO — postergar para Fase 2 |
| D7 | Estado RASCUNHO em contratos | Sponsor | Day 13 (qua 27/mai) | SIM — workflow gerente para ATIVO |
| D8 | Critérios formais de UAT | Sponsor + tesouraria | Day 13 (qua 27/mai) | Sponsor + 2º controladoria assinam; mín. 30 cenários P0/P1 |

---

## 10. Onboarding do dev (primeiros 2 dias)

Material que o dev recebe ao chegar no Day 1 (seg 11/mai):

### 10.1 Leitura obrigatória (em ordem) — ~6 horas
1. **SPEC.md v1.2** (45min) — visão geral
2. **ADR.md v1.2** (45min) — decisões e por quês
3. **Anexo A** (1h) — NDFs e MTM
4. **Anexo B** (1h) — modalidades e dados
5. **Anexo C** (1h30) — antecipação
6. **plan/Plano_Agentes_FINIMP.md** (45min) — visão Fase 2
7. **plan/Prompt_Comparacao_FINIMP.md** (15min) — caso de uso final

### 10.2 Setup técnico — ~4 horas
- [ ] Acessos GCP, repo, Slack, Drive
- [ ] Instalar **.NET 11 SDK** (`global.json` pin `net11.0`), Docker, Cloud SDK, Cloud SQL Auth Proxy
- [ ] Clonar repo (vazio ainda) e configurar local dev environment
- [ ] Validar acesso ao GCP via CLI: `gcloud auth login`, `gcloud config set project`

### 10.3 Reuniões D1-D2
- [ ] Kickoff meeting (D1 09h, com sponsor)
- [ ] Walkthrough do SPEC + ADR (D1 14h, com sponsor — 2h)
- [ ] Walkthrough dos 3 Anexos com 2º controladoria (D2 09h — 3h)
- [ ] Walkthrough dos contratos modelo (D2 14h — 1h)

### 10.4 Princípios para o dev (ADR-014 reforçado)
- **Clean code, simple > clever**: revisar PRs próprios sob essa lente antes de pedir review
- **Funções puras no motor financeiro**: nada de `DateTime.Now` em código de domínio
- **Money, Percentual, FxRate são VOs** — nunca `decimal` cru
- **Mocks proibidos no domínio**: Testcontainers + in-memory
- **Boundary inviolável**: RAG/LLM nunca responde sozinho sobre cálculo financeiro

---

## 11. Checklist de "go" para Sprint 1 (sex 19/jun, fim do Sprint 0)

- [ ] M2 atingido: Hello World autenticado em prod
- [ ] DoD da Sprint 0 (§5) integralmente cumprida
- [ ] Sprint 1 board pronto com as 6 stories da §6.1
- [ ] Decisões D1-D5 da §9 fechadas
- [ ] Stream M produziu BANCO_CONFIG seed completo
- [ ] Cobertura de testes ≥ 80% no skeleton
- [ ] Sprint Demo realizada e gravada
- [ ] Sprint Retro com action items
- [ ] Sponsor aprova "go" para Sprint 1

---

## 12. O que vem depois (visão antecipada Sprints 2-11)

Cronograma macro com referência ao `tasks/plan.md` v1.4 para detalhamento:

| Sprint | Datas | Foco | Marco |
|---|---|---|---|
| **2** | 6/jul - 17/jul | Fase 2 — FINIMP cronograma + cálculo saldo + tabela completa | **M3** ✅ |
| **3** | 20/jul - 31/jul | Fase 3 — Multi-moeda + ingestão PTAX | — |
| **4** | 3/ago - 14/ago | Fase 4 — 4131 + REFINIMP | — |
| **5** | 17/ago - 28/ago | Fase 4 cont. — NCE + Balcão + FGI + **motor antecipação** | **M4** ✅ |
| **6** | 31/ago - 11/set | Fase 5 — Garantias polimórficas + arrancar Fase 6 | — |
| **7** | 14/set - 25/set | Fase 6 — NDFs + MTM | **M5** ✅ |
| **8** | 28/set - 9/out | Fase 7 — Painéis + dashboards + simulador | — |
| **9** | 12/out - 23/out | Fase 7B — MCP + A2A + RAG (11 tools) | **M6** ✅ |
| **10** | 26/out - 6/nov | Fase 8 — alertas + snapshots + provisão mensal + ETL migração + indexação batch RAG | **M7** ✅ |
| **11** | 9/nov - 20/nov | Fase 9 — UAT formal + dual-run + go-live + aposentadoria planilha | **M8** ✅ |

---

## 13. Status check semanal (template)

A ser preenchido toda sexta no Sprint Demo.

```
SEMANA: <N>
DATA: <dd/mmm/yyyy>
SPRINT: <X>
SPRINT GOAL: <texto>

✅ ENTREGUE
- Story X.Y: ...
- Story X.Z: ...

🚧 EM ANDAMENTO
- Story X.W: <% concluído>

⚠️ BLOQUEIOS
- <descrição> | Owner: <nome> | ETA: <data>

📊 MÉTRICAS
- Velocity: <pts>
- Coverage motor: <%>
- Coverage geral: <%>
- Bugs P0/P1 abertos: <n>
- % tempo sponsor: <%>

🎯 PRÓXIMA SEMANA
- <foco>

🔥 RISCOS NOVOS
- <descrição> | Mitigação: <ação>
```

---

## 14. Quando este documento é atualizado

Este plano tático é **vivo**:
- **Após cada Sprint Retro:** atualizar §6 ou §12 com lições aprendidas
- **Quando uma decisão (D1-D8) é fechada:** atualizar §9 e marcar como ✅
- **Quando uma data desliza:** atualizar §3 milestones e §12 cronograma macro; comunicar à diretoria
- **Mensalmente:** revisão completa pelo sponsor + arquivamento da versão anterior

---

## Changelog

- **v1.0 — 10/maio/2026** — versão inicial. Plano tático para as primeiras 8 semanas (Fase B + Sprint 0 + Sprint 1) com calendário absoluto a partir de 11/maio/2026. Premissas confirmadas pelo sponsor: início imediato, .NET 11 condicional (fallback .NET 10 LTS), backend puro 3 primeiros meses (sem frontend).
