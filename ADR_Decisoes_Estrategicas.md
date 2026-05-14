# ADR — Decisões Arquiteturais Estratégicas

**Sistema:** SGCF — Sistema de Gestão de Contratos de Financiamento
**Empresa:** Proxys Comércio Eletrônico
**Sponsor:** Welysson Soares (Tesouraria/Controladoria)
**Status:** Aprovado
**Data:** 08/maio/2026 (v1.0) · 08/maio/2026 (v1.1 — adições de agentes e MCP)
**Versão:** 1.1

**Changelog v1.2 — 10/maio/2026:**

- ADR-009 ampliado: MVP inclui **motor de antecipação de pagamento** (5 padrões A-E) e tool MCP `simular_antecipacao` — detalhamento em `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`
- **ADR-015** Camada RAG complementar com pgvector (busca semântica em texto contratual)

**Changelog v1.1:**

- ADR-003 revisado: stack .NET 8 → **.NET 11** (com nota de fallback para .NET 10 LTS)
- ADR-009 revisado: escopo do MVP inclui camada MCP + A2A baseline e (opcionalmente) Agente Migrador
- ADR-011 Estratégia de agentes (Fase 1 vs Fase 2)
- ADR-012 Servidor MCP do SGCF (acesso de agentes externos)
- ADR-013 Protocolo A2A para comunicação inter-agentes
- ADR-014 Princípios de codificação (clean code, simple > clever)

---

## Propósito deste documento

Registrar de forma auditável as **decisões arquiteturais estratégicas** tomadas antes do desenho técnico detalhado. Cada decisão segue o formato ADR (Architecture Decision Record): contexto, opções consideradas, decisão tomada, consequências.

Este documento alimenta a próxima fase: o desenho técnico detalhado pelo agente Arquiteto + agentes Críticos.

---

## ADR-001 — Sponsor e orçamento

**Contexto:** projeto multi-modalidade de gestão de dívida exige patrocinador executivo claro e orçamento garantido.

**Decisão:** Welysson Soares assume papel de sponsor executivo do projeto. Orçamento aprovado para Fase 1 (MVP standalone).

**Consequências:**

- Decisões de produto vêm direto do sponsor (sem comitês intermediários)
- Sponsor também é Product Owner — agiliza decisões mas exige tempo dedicado
- Risco a mitigar: sponsor precisa preservar 30-40% do tempo durante o build

---

## ADR-002 — Modelo de execução: squad interno enxuto

**Contexto:** equipes pequenas precisam de foco extremo e ferramentas certas para entregar projetos complexos.

**Decisão:** squad interno composto por:

- 2 profissionais de Controladoria (Welysson + 1) atuando como Product Owner / Analistas de Negócio
- 1 desenvolvedor (preferencialmente full-stack sênior)

**Opções descartadas:**

- ❌ Fábrica de software externa (tempo de onboarding, perda de conhecimento)
- ❌ SaaS pronto adaptado (engessa requisitos específicos da Proxys)

**Consequências:**

- ✅ Conhecimento permanece dentro da empresa
- ✅ Iteração rápida com sponsor presente
- ⚠️ **Risco crítico:** 1 dev para sistema de complexidade média/alta carrega 12-18 meses de trabalho greenfield. **Mitigações obrigatórias:**
  1. Adotar framework opinionated (.NET 11 + EF Core + libs maduras) para acelerar 30-40%
  2. Priorização rigorosa de escopo (MVP magro, evoluções iterativas)
  3. Considerar reforço pontual (dev terceirizado) em momentos de pico
  4. Avaliar low-code para CRUD/telas básicas (foco do dev no motor de cálculo)

**Pendência:** validar perfil exato do dev (sênior em .NET? em Cloud GCP? em domínio financeiro?). Recomenda-se sênior generalista com afinidade financeira.

---

## ADR-003 — Stack técnico: .NET Core + GCP

**Contexto:** stack precisa equilibrar velocidade de desenvolvimento, maturidade de mercado, fit com domínio financeiro e alinhamento com a empresa.

**Decisão:**

| Camada                      | Tecnologia                                                      | Justificativa                                                                   |
| --------------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| **Backend (API)**           | **.NET 11** + ASP.NET Core Web API | Performance, tipagem forte, maturidade em domínio financeiro                    |
| **Banco de dados primário** | Cloud SQL PostgreSQL 16                                         | Relacional, ACID, suporte a JSONB para campos polimórficos, gerenciado pelo GCP |
| **ORM**                     | Entity Framework Core 11                                        | Produtividade, integração nativa .NET 11                                        |
| **Auth**                    | GCP IAM + JWT (OAuth 2.0 / OIDC)                                | Padrão de mercado, integra com Identity Platform do GCP                         |
| **Storage**                 | Cloud Storage                                                   | PDFs de contratos, documentos, anexos                                           |
| **Cache**                   | Memorystore (Redis)                                             | Cotações intraday, sessões, dados quentes                                       |
| **Filas/Jobs**              | Cloud Tasks + Cloud Scheduler                                   | MTM periódico, cálculo de provisão de juros, alertas                            |
| **Containers/Deploy**       | Cloud Run (managed)                                             | Serverless, pay-per-use, escala automática                                      |
| **Secrets**                 | Secret Manager                                                  | Credenciais bancárias, chaves de API                                            |
| **CI/CD**                   | Cloud Build + Cloud Deploy                                      | Pipeline nativo GCP                                                             |
| **Observabilidade**         | Cloud Logging + Cloud Monitoring                                | Logs centralizados, métricas, alertas                                           |
| **Front-end**               | Headless (a definir) — consumir API REST                        | Desacoplamento total back/front                                                 |

**Opções descartadas:**

- ❌ Java/Spring (stack maior, sem vantagem clara para o porte)
- ❌ Node.js (ecossistema menos maduro para domínio financeiro)
- ❌ Python (escolhido apenas para componentes de IA/ML futuros)
- ❌ Hospedagem on-premise ou outras clouds (Proxys já tem GCP)

**Consequências:**

- ✅ Stack moderna, com forte ecossistema, bibliotecas para tudo
- ✅ Tipagem forte reduz bugs em domínio com muitas regras
- ✅ Integração nativa com GCP simplifica deploy/operação
- ⚠️ Dev precisa ter expertise em .NET 11

**Nota (v1.2) sobre versão do .NET — decisão encerrada:**
**Decisão confirmada em 10/maio/2026 pelo sponsor: usar .NET 11.** TFM = `net11.0`. Stack: .NET 11 + EF Core 11 + NodaTime. A questão de fallback para .NET 10 LTS não se aplica mais — código novo iniciado diretamente em .NET 11.

---

## ADR-004 — Arquitetura API-driven

**Contexto:** sistema deve ser consumido por front-end headless (definição posterior) e potencialmente por outros sistemas (BI, agentes de IA, integrações futuras com SAP).

**Decisão:** sistema desenhado como **API-first**:

- Toda funcionalidade exposta via REST API (OpenAPI 3.1 documentada)
- Sem acoplamento entre lógica de negócio e UI
- Versionamento de API explícito (`/api/v1/...`)
- Padrão de respostas (HATEOAS opcional, paginação cursor-based, error envelope padrão)
- Autorização por escopo (read/write/admin) e por recurso (multi-tenant futuro)

**Consequências:**

- ✅ Front-end pode ser substituído sem refazer backend
- ✅ Múltiplos clientes possíveis (web, mobile, agentes IA, BI)
- ✅ Testes automatizados mais simples (testar API independentemente)
- ⚠️ Esforço maior em design de API (contratos, versionamento, documentação)

---

## ADR-005 — Hospedagem 100% GCP

**Contexto:** Proxys já é cliente Google Workspace e Google Cloud (Gemini Enterprise Plus). Múltiplos clouds aumentariam complexidade sem ganho.

**Decisão:** hospedagem inteiramente no Google Cloud Platform, na região `southamerica-east1` (São Paulo) — proximidade aos usuários e bancos brasileiros.

**Consequências:**

- ✅ Latência baixa para usuários no Brasil
- ✅ Compliance LGPD facilitado (dados em território nacional)
- ✅ Faturamento consolidado com licenças Google existentes
- ⚠️ Lock-in moderado a GCP (mitigado pelo uso de tecnologias padrão como PostgreSQL e containers)

---

## ADR-006 — Segurança: padrão de mercado

**Contexto:** dados financeiros sensíveis (contratos, valores, garantias). Aplicável LGPD.

**Decisão:** seguir **padrão de mercado** para sistemas financeiros corporativos, sem certificação específica (PCI-DSS, ISO 27001) no MVP.

**Práticas mínimas obrigatórias:**

| Aspecto                      | Prática                                                                                                |
| ---------------------------- | ------------------------------------------------------------------------------------------------------ |
| **Criptografia em trânsito** | TLS 1.3 obrigatório, sem fallback para versões antigas                                                 |
| **Criptografia em repouso**  | CMEK (Customer-Managed Encryption Keys) no Cloud SQL e Cloud Storage                                   |
| **Autenticação**             | OAuth 2.0 + OIDC, tokens com expiração curta (15min) + refresh tokens                                  |
| **Autorização**              | RBAC (Role-Based Access Control) — papéis: tesouraria, contabilidade, gerente, diretor, auditor, admin |
| **Auditoria**                | Audit log completo (quem, quando, o quê, valor antes/depois) — retenção 5 anos                         |
| **Secrets**                  | Nunca em código ou env vars — sempre Secret Manager                                                    |
| **PII/Dados sensíveis**      | Mascaramento em logs, dados de bancos com tokenização quando possível                                  |
| **LGPD**                     | Política de retenção, direito de portabilidade, registro de tratamento                                 |
| **Backup**                   | Diário automático no Cloud SQL, retenção 30 dias, teste de restore mensal                              |
| **Defesas básicas**          | Rate limiting, WAF (Cloud Armor), proteção contra OWASP Top 10                                         |

**Opções consideradas e descartadas no MVP:**

- ❌ Certificações formais (PCI-DSS, SOC 2) — custo desproporcional ao porte
- ❌ Multi-fator obrigatório para todos os usuários — adiado para Fase 2

**Pendência:** validar com Compliance/Jurídico se há requisitos adicionais específicos para gestão de dívida (BACEN, CVM se aplicável).

---

## ADR-007 — DevOps interno

**Contexto:** time pequeno precisa de operação simples e sustentável.

**Decisão:** operação 100% interna (não MSP). Squad cuida de:

- Deploy via Cloud Build (CI/CD automatizado)
- Monitoramento via Cloud Monitoring (dashboards e alertas)
- Resposta a incidentes (on-call do dev em horário comercial; após-hora best-effort)

**Consequências:**

- ✅ Sem custo recorrente de MSP
- ✅ Aprendizado interno acumulado
- ⚠️ **Risco:** 1 dev como ponto único de falha. Mitigações:
  - Documentação operacional clara (runbooks)
  - Cloud Run "self-healing" reduz incidentes manuais
  - Se squad crescer, dividir on-call

---

## ADR-008 — Plano de contas gerencial (sem SAP no MVP)

**Contexto:** decisão registrada nos Anexos A e B.

**Decisão:** SGCF terá plano de contas gerencial próprio, em padrão de mercado, **sem integração ou exportação para SAP no MVP**. Cada conta tem campo `codigo_sap_b1` nullable, preenchido apenas na Fase 2.

**Consequências:**

- ✅ Independência de TI/contabilidade para iniciar
- ✅ Tesouraria começa a usar imediatamente
- ⚠️ Dupla escrituração temporária (gerencial + contábil oficial no SAP) durante MVP

---

## ADR-009 — Escopo do MVP (Fase 1)

**Decisão:** o MVP entrega:

✅ **Inclui:**

- Cadastro completo de contratos (todas as modalidades: FINIMP, REFINIMP, 4131, NCE, Balcão Caixa, FGI)
- Motor de cronograma (bullet, mensal, trimestral, semestral, customizado)
- Cálculo de saldo devedor (principal, juros provisionados, comissões) em moeda original e BRL
- Cadastro e gestão de NDFs (Forward simples + Collar)
- Mark-to-Market dos NDFs em tempo real
- Cadastro polimórfico de garantias (8 tipos: CDB cativo, SBLC, recebíveis cartão, boleto, duplicatas, FGI, aval, alienação fiduciária)
- Plano de contas gerencial e provisão mensal de juros
- Tabela completa do contrato sob demanda (HTML + PDF + Excel)
- Painel consolidado de dívida (saldo total em BRL multi-moeda)
- Painel consolidado de garantias
- Cronograma consolidado de vencimentos
- Importação de PTAX automática (API BCB)
- Configuração por banco (REFINIMP, % CDB, prazo máximo)
- Audit trail completo
- Auth + autorização por papel
- **Servidor MCP read-only expondo dados do SGCF para agentes externos** (ADR-012, adicionado v1.1)
- **Baseline A2A (Agent Card + endpoint de descoberta)** para suporte a futuros agentes (ADR-013, adicionado v1.1)
- **(Opcional) Agente Migrador da planilha**, se análise custo-benefício na Fase B confirmar ganho (ADR-011, adicionado v1.1)
- **Motor de antecipação de pagamento** com 5 padrões (A-E) configuráveis por banco/modalidade — endpoint REST + tool MCP `simular_antecipacao` (adicionado v1.2; detalhamento em `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`)

❌ **Não inclui (Fase 2 ou posterior):**

- Integração SAP (ADR-008)
- OCR + IA para extração de PDFs de contratos novos
- **Agentes funcionais de negócio**: Comparador de Cotações, Gravador de Cotações, Parecerista (ADR-011)
- Open Finance / saldos automáticos dos bancos
- Multi-tenant
- Mobile nativo

---

## ADR-010 — Cronograma estimado (a calibrar)

**Premissa:** squad de 1 dev sênior + 2 controladoria, em paralelo.

| Fase                                   | Duração                      | Marco                             |
| -------------------------------------- | ---------------------------- | --------------------------------- |
| **Discovery e desenho técnico**        | 4 semanas                    | Documento de Arquitetura aprovado |
| **Sprint 0: setup e infra**            | 2 semanas                    | CI/CD, banco, deploy básico       |
| **Sprint 1-3: cadastros + cronograma** | 6 semanas                    | Tesouraria já cadastra contratos  |
| **Sprint 4-5: NDFs + MTM**             | 4 semanas                    | Posição em tempo real disponível  |
| **Sprint 6-7: garantias + dashboards** | 4 semanas                    | Painel consolidado funcional      |
| **Sprint 8: refinamento + UAT**        | 2 semanas                    | Aceite final                      |
| **Migração de dados (paralelo)**       | Iterativa                    | 1.200+ contratos importados       |
| **Total**                              | **~22 semanas** (~5,5 meses) | MVP em produção                   |

**Riscos no cronograma:**

- ⚠️ Complexidade do MTM dos NDFs pode adicionar 1-2 semanas
- ⚠️ Migração dos 1.200+ contratos da planilha pode ter dados sujos
- ⚠️ Cobertura de teste necessária pode adicionar 2-3 semanas (financeiro exige zero erro)

---

## ADR-011 — Estratégia de agentes (Fase 1 vs Fase 2)

**Contexto:** o ecossistema de Tesouraria/Controladoria da Proxys envolverá múltiplos agentes especialistas (Comparador de Cotações, Gravador de Cotações, Parecerista, Validador de Contrato, Otimizador, etc. — ver `Plano_Agentes_FINIMP.md`). A pergunta é: o que entra na Fase 1 (MVP backend) e o que fica para Fase 2.

**Decisão:**

**Fase 1 — Backend é o "sistema de registro" (system of record):**

- Foco em construir um backend sólido, API-first, **agent-ready**: APIs REST + Servidor MCP + baseline A2A já estruturados, mas **sem agentes funcionais de negócio implementados**.
- Toda funcionalidade essencial (cadastros, cronograma, MTM, painéis) é implementada como serviço determinístico em .NET — sem dependência de LLM no caminho crítico.

**Fase 1 — exceção autorizada (avaliar custo-benefício na Fase B):**

- **Agente Migrador da planilha**: 1.200+ contratos com dados sujos. Tarefa onde extração probabilística (LLM) supera regex puro. Recomendação técnica: usar Gemini Fast para extrair JSON estruturado da planilha + validação determinística no SGCF (rejeitar contratos com campos inconsistentes para revisão humana).
- **Decisão de inclusão na Fase 1**: tomar na Fase B após análise de qualidade dos dados da planilha. Se a planilha estiver razoavelmente padronizada, ETL determinístico basta; se estiver caótica, agente justifica.

**Fase 2 — Agentes funcionais de negócio (escopo separado, após MVP em produção):**

| Agente                               | Modelo recomendado | Função                                                                                  |
| ------------------------------------ | ------------------ | --------------------------------------------------------------------------------------- |
| **Gravador de Cotações**             | Gemini Fast        | Lê PDF/email/screenshot de cotação → JSON estruturado → POST `/api/v1/cotacoes` no SGCF |
| **Comparador de Cotações**           | Gemini Reasoning   | Aplica DRE de FINIMP comparativa (lógica do `Prompt_Comparacao_FINIMP.md`)              |
| **Parecerista (Recomendador Final)** | Claude Opus        | Consolida análises e emite parecer com pleitos de negociação                            |
| **Validador de Contrato**            | Claude Sonnet      | Compara proposta (JSON) vs contrato (PDF) → divergências                                |
| **Compliance Tributário**            | Claude Sonnet      | Valida IRRF, IOF, jurisdição, NDF                                                       |
| **Otimizador de Portfólio**          | Gemini Reasoning   | Combina N propostas vs teto mensal vs limites                                           |

(Lista alinhada ao `Plano_Agentes_FINIMP.md`.)

**Premissa-chave:** os agentes de Fase 2 **consomem o SGCF como cliente** via API REST + MCP. O SGCF é a fonte da verdade; os agentes são camadas de UX/inteligência sobre ele.

**Consequências:**

- ✅ Backend é estável e testável antes de qualquer dependência de LLM
- ✅ Agentes podem ser desenvolvidos em paralelo na Fase 2 sem esperar refatoração de backend
- ✅ Falha de LLM nunca derruba o sistema de registro
- ⚠️ Disciplina exigida: nenhum atalho do tipo "LLM faz isso pra gente" no caminho crítico de cálculo

---

## ADR-012 — Servidor MCP do SGCF

**Contexto:** o ecossistema de agentes (interno e externo) precisa de uma forma padronizada de descobrir e invocar capacidades do SGCF. O **Model Context Protocol (MCP)** da Anthropic é o padrão emergente para conectar LLMs a fontes de dados/sistemas, e já é suportado nativamente por Claude Desktop, Claude Code, e via SDKs por Gemini, OpenAI e outros.

**Decisão:** o SGCF expõe um **Servidor MCP** como segunda interface de consumo (a primeira é a REST API).

**Arquitetura:**

- **Endpoint dedicado**: `https://mcp.sgcf.proxys.com.br` (ou subpath `/mcp` no domínio principal)
- **Transporte**: HTTP+SSE (streamable HTTP) — padrão MCP atual; STDIO opcional para casos de desenvolvimento local
- **Auth**: OAuth 2.1 com escopos por tool (read/write); tokens emitidos pelo Identity Platform GCP (mesmo do REST)
- **Implementação**: SDK MCP oficial (Anthropic) para .NET ou wrapper próprio sobre a especificação MCP — decisão técnica do dev na Sprint 0
- **Mesma camada de aplicação**: tools MCP **não duplicam lógica** — são adaptadores finos sobre os mesmos handlers MediatR/Application Services usados pela REST API

**Tools expostos no MVP (read-only):**

| Tool                         | Função                                                     | Auth   |
| ---------------------------- | ---------------------------------------------------------- | ------ |
| `list_contratos`             | Listar contratos com filtros (banco, modalidade, status)   | leitor |
| `get_contrato`               | Obter contrato completo com cronograma e garantias         | leitor |
| `get_tabela_completa`        | Tabela completa do contrato (8 blocos) em JSON ou Markdown | leitor |
| `get_posicao_divida`         | Posição de dívida consolidada (multi-moeda, MTM)           | leitor |
| `get_calendario_vencimentos` | Próximos N dias de vencimentos                             | leitor |
| `get_cotacao_fx`             | Cotação atual ou histórica de FX                           | leitor |
| `get_mtm_hedge`              | MTM atual de um NDF específico                             | leitor |
| `simular_cenario_cambial`    | Estresse cambial (read-only, não persiste)                 | leitor |

**Tools de escrita (Fase 1 ou Fase 2 — decidir após MVP gerencial estável):**

| Tool                  | Função                                                     | Auth             |
| --------------------- | ---------------------------------------------------------- | ---------------- |
| `create_contrato`     | Criar contrato (será usado pelo Agente Gravador na Fase 2) | escritor + audit |
| `registrar_pagamento` | Baixar parcela do cronograma                               | escritor + audit |
| `cadastrar_hedge`     | Cadastrar NDF                                              | escritor + audit |

**Garantias obrigatórias do servidor MCP:**

- Cada chamada é registrada no `AUDIT_LOG` (mesmo audit trail da REST API) com origem identificada (`source: "mcp"`)
- Rate limiting próprio (mais restrito que REST)
- Schemas dos tools versionados e documentados
- Erros padronizados conforme spec MCP

**Consequências:**

- ✅ Agentes externos (Claude Desktop, Gemini, custom agents) acessam o SGCF de forma padronizada
- ✅ Equipe de Tesouraria pode consultar o SGCF via Claude Desktop sem depender da UI web
- ✅ Fundação para a Fase 2 (agentes funcionais consomem MCP)
- ⚠️ Manter MCP e REST API em paridade requer testes duplicados
- ⚠️ Spec MCP ainda evolui — adoção implica acompanhar versões

---

## ADR-013 — Protocolo A2A para comunicação inter-agentes

**Contexto:** quando múltiplos agentes (Fase 2) interagirem entre si — por exemplo, Comparador chama Validador, que chama Compliance — é necessário um padrão de descoberta e comunicação. O **Agent2Agent (A2A)** é um protocolo aberto (Google + parceiros, anunciado abr/2025) desenhado especificamente para interoperabilidade entre agentes de diferentes fabricantes/modelos.

**Decisão:** adotar **A2A como protocolo de interoperabilidade entre agentes** do ecossistema Proxys.

**Escopo na Fase 1:**

- Publicar **Agent Card do SGCF** em `/.well-known/agent.json` descrevendo:
  - Identidade (nome, descrição, versão)
  - Skills oferecidas (lista de capabilities — ex: "consulta posição de dívida", "calcula MTM de NDF")
  - Endpoints A2A (`tasks/send`, `tasks/get`, etc.)
  - Mecanismo de autenticação (OAuth 2.1)
- Endpoint A2A de descoberta + 1 skill demonstrativa (ex: `consulta_posicao_divida` traduzindo de A2A para Application Service interno)
- **Não construir agentes funcionais** na Fase 1 — apenas a infra para que agentes futuros falem A2A com o SGCF

**Escopo na Fase 2:**

- Cada agente de negócio (Comparador, Validador, Parecerista, etc.) é também servidor A2A com Agent Card próprio
- Orquestrador (Agent Builder ou custom .NET) descobre agentes via A2A e os compõe
- Fluxos longos via `tasks/sendSubscribe` com SSE para streaming

**Por que A2A e não somente MCP:**

| Aspecto             | MCP                   | A2A                                           |
| ------------------- | --------------------- | --------------------------------------------- |
| Propósito           | LLM ↔ ferramenta/dado | Agente ↔ agente                               |
| Modelo de interação | Tool calling síncrono | Tarefas assíncronas, longa duração, streaming |
| Estado              | Stateless por chamada | Stateful (conceito de Task)                   |
| Descoberta          | Lista de tools        | Agent Card com skills                         |
| Quem consome        | LLM tomando decisão   | Outro agente coordenando trabalho             |

**No SGCF eles convivem:**

- **MCP** expõe **dados e operações determinísticas** do SGCF para agentes consumirem como ferramenta
- **A2A** será como o SGCF (e seus agentes futuros) **conversam entre si** quando há orquestração

**Consequências:**

- ✅ Fundação aberta e padronizada para o ecossistema multi-agente da Fase 2
- ✅ Agentes podem ser desenvolvidos em qualquer linguagem/framework (Python, .NET, Go) e ainda interoperar
- ⚠️ Spec A2A ainda evolui (v0.x); manter dependências atualizadas
- ⚠️ Curva de aprendizado para o squad — investir em prova de conceito na Fase B

---

## ADR-014 — Princípios de codificação

**Contexto:** com 1 dev sênior responsável por sistema de complexidade média/alta, qualidade de código e manutenibilidade são de risco crítico. Codebase precisa permanecer lível e modificável quando 2º dev entrar (Fase 2) ou para handover.

**Decisão:** adotar como princípios não-negociáveis:

1. **Clean Code** (Martin) — nomes que revelam intenção, funções pequenas, classes coesas, sem comentários redundantes
2. **"Simple instead of clever"** — preferir solução boring/óbvia à solução engenhosa; cleverness exige contexto que o próximo leitor pode não ter
3. **Domain-Driven Design (DDD) tático** — entidades, value objects, agregados, repositórios; **estratégico opcional** (bounded contexts ficam claros, mas sem over-engineering em microsserviços)
4. **Clean Architecture** já em ADR-003 — dependências apontam para o domínio
5. **YAGNI agressivo** — não construir abstração até a 3ª duplicação; não construir "extensibility" hipotética
6. **Imutabilidade onde fizer sentido** — `record`/`record struct` para DTOs e VOs; objetos de domínio com mutações controladas
7. **Pure functions no motor de cálculo** — qualquer função financeira (juros, MTM, gross-up) é pura, testável isoladamente, sem side effects
8. **Sem mocks no domínio** — testes de domínio rodam contra implementações reais (in-memory ou Testcontainers); mocks só no boundary I/O quando inevitável

**Aplicação operacional:**

- **Code review obrigatório** com gate de simplicidade: revisor pergunta "isso poderia ser mais simples?" e exige justificativa para código não-óbvio
- **Refatoração contínua** alocada (~10% do tempo de cada sprint)
- **Métricas de qualidade no CI** (sem falhar build, mas reportando):
  - Complexidade ciclomática (alerta > 10 por método)
  - Tamanho de arquivo (alerta > 400 linhas)
  - Duplicação de código (limite 3%)
- **Revisão arquitetural** ao final de cada fase — sponsor + dev avaliam se estão seguindo o princípio

**Consequências:**

- ✅ Código sustentável por equipe pequena
- ✅ Onboarding do 2º dev mais rápido
- ✅ Modificações em domínio financeiro com baixo risco de regressão
- ⚠️ Disciplina exigida — facil cair em "vou só dar um jeitinho aqui" sob pressão de cronograma

---

## ADR-015 — Camada RAG complementar com pgvector (busca semântica em texto contratual)

**Contexto:** o motor estruturado de antecipação (`Anexo C`) cobre os ~80% das perguntas calculáveis com precisão determinística. Restam ~20% de perguntas **textuais long-tail** — cláusulas atípicas (market flex, cross-default, jurisdição), validação de contratos novos vs benchmark, busca de padrões ("quais contratos têm cláusula X?"). Essas perguntas **não são bem servidas** por estrutura relacional, mas são bem servidas por busca semântica.

A pergunta natural é: VectorDB dedicado (Vertex AI Vector Search) ou pgvector no Postgres existente?

### Decisão

Adotar **pgvector** como extension do PostgreSQL Cloud SQL **já provisionado para o SGCF** — sem novo serviço dedicado.

A camada RAG é **complementar e secundária** ao motor estruturado:

| Cenário | Camada usada |
|---|---|
| Pergunta calculável (custo de antecipação, MTM, juros) | **Estruturada** (Anexo C / Strategy pattern) |
| Pergunta textual (o que diz cláusula X) | **RAG** (pgvector) |
| Validação de contrato novo vs histórico | **RAG** + comparação |
| Discovery / busca de padrões em cláusulas | **RAG** |
| Pergunta híbrida | **Estruturada como fonte primária**, RAG cita cláusula que justifica |

### Boundary não-negociável

- **RAG nunca responde sozinho sobre valores financeiros calculáveis**
- **LLM nunca calcula** — quando a pergunta envolve número, o orquestrador chama o motor estruturado e usa o texto apenas para citar a cláusula que justifica
- **Determinismo do MVP financeiro permanece intacto** (SPEC §17.3 / ADR-014)

### Stack

| Componente | Escolha | Justificativa |
|---|---|---|
| Vector store | **pgvector** no Cloud SQL existente | Zero novo serviço; transações ACID com restante do SGCF; backup unificado; suporta milhões de vetores com índice `hnsw` |
| Embeddings | **Gemini text-embedding-005** (ou versão mais recente disponível na data de implementação) | Multilíngue (PT/EN), barato (~$0,025/1M tokens), nativo no GCP |
| Chunking | Por **cláusula** (não por tamanho fixo) | Cláusulas têm fronteiras semânticas claras; preserva contexto |
| Metadata | `banco_id`, `modalidade`, `data_assinatura`, `clausula_numero`, `topico`, `versao` | Permite filtros estruturados + busca vetorial híbrida |
| Re-ranking | Opcional (não no MVP) | Avaliar se precisão for insuficiente |
| Reindexação | Trimestral + sob demanda quando contrato é cadastrado | Mantém embeddings atualizados |

### Por que **não** Vertex AI Vector Search no MVP

- Corpus da Proxys: 1.200 contratos × ~500 chunks ≈ 600k vetores hoje, 2,5M no horizonte de 3 anos. **pgvector com índice `hnsw` cobre confortavelmente até 10M+ vetores**.
- Vertex AI Vector Search adiciona um serviço dedicado, com index endpoint e managed deployment — overhead operacional desproporcional para o porte.
- Latência: pgvector ~50ms p99 para o porte; Vertex ~10ms p99 — diferença irrelevante para o caso de uso (não está em hot path real-time).
- Migração futura é troca de adapter, não refatoração de domínio — opcionalidade preservada.

### Tools MCP adicionados ao MVP (Fase 7B)

| Tool | Função |
|---|---|
| `buscar_clausula_contratual(query, filtros)` | Busca semântica em cláusulas indexadas, retorna trechos com citação literal e contrato/cláusula de origem |
| `comparar_clausulas(contrato_id_1, contrato_id_2, topico)` | Recupera cláusulas do mesmo tópico em 2 contratos diferentes para comparação textual |

### Agente Extrator de Cláusulas (Fase 2 do projeto, pós-MVP)

Alinhado ao **Validador de Contrato** do `Plano_Agentes_FINIMP.md`, o agente da Fase 2:

1. Recebe PDF de contrato novo
2. Extrai cláusulas estruturadas (Gemini Fast / Sonnet)
3. Gera embeddings e indexa em pgvector
4. **Sugere atualizações no `BANCO_CONFIG`** se detectar parâmetros que ainda não estão cadastrados (ex: novo banco, novo padrão de antecipação)
5. Sinaliza divergências contrato vs proposta

No MVP, a indexação é feita **uma vez em batch durante a migração** dos 1.200 contratos existentes (Fase 9) — sem agente. O agente fica para Fase 2.

### Custos estimados

| Item | Custo mensal |
|---|---|
| Storage vetorial em pgvector | ~R$ 0 (incluso no Cloud SQL existente) |
| Embeddings iniciais (one-time) | ~R$ 8 |
| Re-indexação trimestral | ~R$ 2/mês prorratado |
| Queries (1.000/mês com Gemini Pro) | ~R$ 30 |
| **Total adicional** | **~R$ 40/mês** |

### Consequências

- ✅ Capacidade de Q&A textual sobre contratos sem comprometer determinismo do motor financeiro
- ✅ Stack consolidada (PostgreSQL com pgvector cobre relacional + vetorial)
- ✅ Fundação para o agente Validador de Contrato da Fase 2
- ✅ Custo marginal baixo (~R$ 40/mês)
- ⚠️ Se corpus crescer >10M vetores ou latência exigir <10ms p99, migrar para Vertex AI Vector Search (decisão da Fase 2+)
- ⚠️ Disciplina de boundary: revisão de PR rejeita qualquer caminho onde RAG/LLM responde sozinho sobre cálculo financeiro

---

## ADR-016 — Calendário ANBIMA como referência de dias úteis

**Contexto:** contratos bancários no Brasil seguem a convenção *Following Business Day* — vencimentos que recaiam em sábado, domingo ou feriado são ajustados para o próximo dia útil. Para derivativos e contratos cambiais é comum a variante *Modified Following* (não atravessar fronteira de mês). O motor de cronograma do SGCF precisa contar dias úteis (base 252 do CDI) e ajustar datas previstas — sem isso, parcelas seriam geradas em datas inválidas e provisões de juros não fechariam com o que o banco efetivamente cobra.

A pergunta natural: qual fonte de calendário adotar? Há três opções de mercado: BACEN, ANBIMA e B3. Há ainda bibliotecas externas (`Nager.Date`, `Holidays.NET`) e a opção de tabela própria manual.

### Decisão

Adotar o **calendário ANBIMA** (Associação Brasileira das Entidades dos Mercados Financeiro e de Capitais) como referência primária de dias úteis, materializado em uma tabela `sgcf.feriado` administrada internamente.

| Eixo | Escolha |
|------|---------|
| Fonte canônica | **ANBIMA** (planilha anual oficial em `anbima.com.br/feriados`) |
| Mecanismo | **Tabela `sgcf.feriado` persistida**, atualizada anualmente por upload de admin |
| Atualização | **Operacional** — não há sincronização automática em runtime; novos anos entram via migration ou endpoint admin |
| Escopo aplicado pelo motor | **Brasil (nacional)** — feriados regionais/municipais ficam apenas registrados, não influenciam cronograma |
| Convenções suportadas | `Following`, `ModifiedFollowing`, `Preceding`, `ModifiedPreceding`, `Unadjusted` (ISDA 2006 §4.12) |

### Por que ANBIMA, não BACEN nem B3

- **ANBIMA** publica calendário oficial usado por todo o mercado financeiro brasileiro; cobre feriados nacionais civis, feriados bancários e dias sem expediente nos principais centros.
- **BACEN** não publica calendário formal via API estável; sua tabela é derivada das mesmas portarias federais que a ANBIMA segue.
- **B3** é o calendário de pregão, equivalente ao ANBIMA na prática mas com escopo limitado ao mercado de capitais.
- **ANBIMA** marca convenientemente quais feriados são bancários (Carnaval, Corpus Christi) e quais são nacionais civis (Lei 662/49) — diferenciação útil para relatórios.

### Por que tabela interna em vez de lib externa

- `Nager.Date` cobre feriados nacionais mas **não trata Carnaval e Corpus Christi como dias não úteis** (não são feriados civis, são bancários). Para o mercado financeiro brasileiro isso é incorreto.
- Tabela interna permite **emendas e ajustes manuais** (feriados extraordinários, decisões de mercado pontuais como pandemias) sem depender de release externo.
- O custo é uma tarefa anual de atualização — aceitável.

### Implementação

| Componente | Escolha |
|------------|---------|
| Domínio | `Feriado` entity + enums `TipoFeriado`, `EscopoFeriado`, `FonteFeriado`, `ConvencaoDataNaoUtil` |
| Pure functions | `BusinessDayCalculator` (Sgcf.Domain.Calendario) — sem I/O |
| Service | `IBusinessDayCalendar` (Application) com cache em memória por ano |
| Persistência | Tabela `sgcf.feriado` com índice único `(data, tipo, escopo)` e secundário `(ano_referencia, escopo)` |
| Seed inicial | Feriados nacionais ANBIMA 2026 e 2027 via migration `CalendarioFeriado` |
| API | `GET /api/v1/feriados?ano={ano}&escopo={escopo}` |

### Decisões pendentes (próxima rodada)

- Endpoint admin para upload de planilha ANBIMA anual (POST multipart) — Sprint que adicionar Provisão de Juros base 252
- Suporte a feriados regionais (UF) — fora do MVP; cliente pediu para ignorar por ora
- Integração com BACEN-SGS para validação cruzada — opcional, baixa prioridade

### Consequências

- ✅ Geração de cronograma fiel ao mercado brasileiro
- ✅ Cálculo de juros base 252 (CDI) tem fonte determinística
- ✅ Convenções ISDA suportadas para derivativos (NDF/swap)
- ⚠️ Atualização anual é tarefa manual — deve entrar no calendário operacional
- ⚠️ Carnaval e Corpus Christi marcados como `Bancario`, não `Nacional` — relatórios contábeis que diferenciam precisam respeitar essa nuance

---

## Decisões pendentes para próxima rodada

| #   | Decisão                                                                                | Quem decide          | Quando              |
| --- | -------------------------------------------------------------------------------------- | -------------------- | ------------------- |
| 1   | Perfil exato do desenvolvedor (sênior, .NET 8, GCP, finance)                           | Sponsor + TI         | Antes do kickoff    |
| 2   | Tecnologia do front-end headless                                                       | Sponsor + TI         | Antes da Sprint 1   |
| 3   | Reforço temporário de dev em momentos de pico (sim/não)                                | Sponsor              | Conforme necessário |
| 4   | Política exata de retenção LGPD                                                        | Sponsor + Compliance | Antes da Sprint 0   |
| 5   | UAT — quem participa, critérios de aceite                                              | Sponsor + Tesouraria | Antes da Sprint 8   |
| 6   | Confirmar versão definitiva do .NET (.NET 11 GA vs .NET 10 LTS — ver nota ADR-003)     | Sponsor + dev        | Final da Fase B     |
| 7   | Inclusão do Agente Migrador na Fase 1 (sim/não conforme qualidade dos dados) — ADR-011 | Sponsor + dev        | Final da Fase B     |
| 8   | SDK MCP a usar (oficial Anthropic vs implementação própria) — ADR-012                  | Dev + sponsor        | Sprint 0            |
| 9   | Versão do A2A spec a adotar (qual minor version) — ADR-013                             | Dev                  | Sprint 0            |

---

**Próximo passo imediato:** rodar o agente Arquiteto (Fase B do plano) com este ADR como contexto, junto com Business Case + Anexos A e B.
