# Todo — Quadro da Dívida + Simulação de Contratação

> Plano detalhado: `plan.md` neste diretório.
> Marque cada item ao concluir. Ordem segue o grafo de dependências.

---

## Bloqueio externo

- [x] **Aprovação humana** das 12 Decisões Arquiteturais (plan.md §2) — concluído em 2026-05-19; AD-3 evoluiu para "on-the-fly + cache Redis TTL curto" (gerou Task 2.4b abaixo). Demais 11 ADs confirmadas como propostas.
- [x] **Decisão** das 8 Open Questions (plan.md §10) — concluído em 2026-05-19; 3 mudanças vs proposta MVP (Q6, Q7, Q8) geraram 3 tasks novas abaixo.

---

## Fase 1 — Quadro da Dívida (real-only)

> Paralelização: 1.1, 1.2, 1.3 podem rodar em paralelo. 1.4 depende dos 3.

- [x] **1.1 [M]** `ProjetorSaldoMensal` — pure function no domínio
  - [x] Criar `EventoProjecao`, `TipoEventoProjecao`, `QuadroDividaProjecao`, `MesProjecao`, `SaldoBancoMes` em `Sgcf.Domain.Painel`
  - [x] Implementar `ProjetorSaldoMensal.Projetar(saldoInicial, eventos, ano)`
  - [x] 8+ unit tests + 2 property tests (FsCheck)
  - [x] Acceptance: invariantes `saldoFim[m] == saldoInicio[m+1]`, soma de shares = 100%
  - [x] Verify: `dotnet test --filter "FullyQualifiedName~ProjetorSaldoMensal"`

- [x] **1.2 [S]** `GetSaldoPorBancoAtualQuery` — saldo atual por banco
  - [x] Query + Handler + DTO em `Sgcf.Application.Painel.Queries`
  - [x] Reuso da lógica de conversão de moeda do `GetPainelDividaQueryHandler`
  - [x] Integration test com Testcontainers
  - [x] Acceptance: soma por banco == saldo total do `/painel/divida`

- [x] **1.3 [S]** Estender `VencimentoItemDto` com `bancoId` + `bancoApelido`
  - [x] Atualizar DTO em `Sgcf.Application.Painel`
  - [x] Popular no `GetCalendarioVencimentosQueryHandler`
  - [x] Pelo menos 1 teste novo
  - [x] Acceptance: contrato existente não quebra (campo aditivo)

- [x] **1.4 [M]** `GetQuadroDividaQuery` + endpoint `/painel/quadro-divida`
  - [x] Query, Handler, DTOs (`QuadroDividaDto`, `MesQuadroDividaDto`, `SaldoBancoMesDto`)
  - [x] Orquestra 1.1 + 1.2 + repositório de eventos de cronograma
  - [x] Adicionar action no `PainelController`: `GET /api/v1/painel/quadro-divida?ano=...&cenarioId=...`
  - [x] E2E Integration test com 3 contratos
  - [x] Acceptance: resposta inclui snapshot atual + meses[12] + sumario[12]; `cenarioAplicado: null`

- [x] **Checkpoint Fase 1** — Frontend pode renderizar quadro base (sem simulações)
  - [x] Suite verde: `dotnet test --filter "Category!=Slow"`
  - [x] Bruno request `07-Painel/6 - Quadro da Divida.bru` executa contra dev
  - [x] Gate humano: frontend valida shape do DTO

---

## Fase 2 — Simulação de Contratação (domínio + persistência + API)

> Paralelização: 2.4 paralelo com 2.2 (não depende). 2.3 depende de 2.2. 2.5 depende de 2.3.

- [x] **2.1 [M]** Domain `CenarioSimulacao` + `SimulacaoContratacao`
  - [x] Criar agregado raiz `CenarioSimulacao` com lifecycle (`Rascunho`/`Ativo`/`Arquivado`)
  - [x] Criar child entity `SimulacaoContratacao` com todos os campos para gerar cronograma
  - [x] **D-9 (Q6)**: incluir `GarantiaExigidaPrevista : string?` (máx 500 chars) — informativo, sem validação
  - [x] Criar enum `StatusCenarioSimulacao`
  - [x] Criar interface `ICenarioSimulacaoRepository`
  - [x] 20+ unit tests cobrindo criação, transições, validações
  - [x] Property test: cenário arquivado é imutável

- [x] **2.1b [S] (D-10, Q7)** Domain — Factory `CenarioSimulacao.DuplicarComoRascunho`
  - [x] Método estático recebe cenário origem + `criadoPor` novo + clock
  - [x] Cópia profunda: todas `SimulacaoContratacao` filhas duplicadas com novos Ids
  - [x] Nome sufixado `" (cópia)"`; status `Rascunho`; `CreatedAt = now`
  - [x] 6+ unit tests (cópia simples, cópia com N filhas, cenário arquivado pode ser duplicado, audit fields novos)

- [x] **2.2 [M]** Migration + EF Configurations + Repository
  - [x] Migration `S9_SimulacaoContratacao` com 2 tabelas
  - [x] `CenarioSimulacaoConfiguration` + `SimulacaoContratacaoConfiguration`
  - [x] Índices `(status, criado_por)`, `(cenario_id)`, `(banco_id)`
  - [x] Soft delete query filter
  - [x] Implementar `CenarioSimulacaoRepository`
  - [x] DbSets no `SgcfDbContext`
  - [x] Verify: `dotnet ef migrations add` aplica limpo; integration test CRUD passa

- [x] **2.3 [L → fragmentar]** Application — CRUD do Cenário
  - [x] `CriarCenarioSimulacaoCommand` + handler + validator
  - [x] `AtualizarCenarioCommand` + handler + validator
  - [x] `AtivarCenarioCommand` + handler
  - [x] `ArquivarCenarioCommand` + handler
  - [x] `DuplicarCenarioCommand` + handler + validator **(D-10, Q7)**
  - [x] `DeletarCenarioCommand` + handler
  - [x] `AdicionarSimulacaoCommand` + handler + validator (inclui `garantiaExigidaPrevista` opcional — D-9)
  - [x] `RemoverSimulacaoCommand` + handler
  - [x] `GetCenarioSimulacaoByIdQuery` + handler
  - [x] `ListCenariosSimulacaoQuery` + handler
  - [x] DTOs: `CenarioSimulacaoDto`, `SimulacaoContratacaoDto` (com `garantiaExigidaPrevista`)
  - [x] Cobertura ≥ 85% (handlers + validators)

- [x] **2.4 [S]** `SimulacaoCronogramaCalculator`
  - [x] Helper que constrói `GerarCronogramaInput` a partir de `SimulacaoContratacao`
  - [x] Reusa `CronogramaStrategyFactory.Criar(estrutura).Gerar(...)`
  - [x] 4 golden tests (Bullet, BulletComJuros, Price, SAC)
  - [x] Acceptance: cronograma idêntico ao de um contrato real equivalente

- [x] **2.4b [S] (AD-3)** Cache Redis com TTL curto para cronograma hipotético
  - [x] Interface `ICronogramaSimulacaoCache` em `Sgcf.Application.Simulacao`
  - [x] Implementação `RedisCronogramaSimulacaoCache` em `Sgcf.Infrastructure.Cache`
  - [x] Chave: `sim:cronograma:{cenarioId}:{simulacaoId}:v{version}`; TTL 60s
  - [x] `SimulacaoContratacao` ganha `Version : int` incrementado em cada mutação
  - [x] Invalidação automática quando `AdicionarSimulacao`/`AtualizarSimulacao`/`RemoverSimulacao` é chamado
  - [x] Wrapper no `SimulacaoCronogramaCalculator` consulta cache antes; cache miss recalcula e popula
  - [x] 6+ unit tests (cache hit; cache miss; invalidação em mutação; TTL expirado; serialização; concurrency safe)
  - [x] Acceptance: 2ª consulta consecutiva do mesmo cenário não recalcula cronograma

- [x] **2.5 [M]** API REST CRUD — `SimulacoesController`
  - [x] 13 endpoints (POST/GET/PATCH/DELETE de cenário + simulação + ativar/arquivar/duplicar + quadro-divida)
  - [x] Idempotência via `Idempotency-Key`
  - [x] RBAC: policies `Escrita`, `Gerencial`, `Leitura`
  - [x] Smoke E2E: criar → adicionar 3 sims → ativar → duplicar → consultar → arquivar

- [x] **2.6 [S]** Endpoint preview de cronograma hipotético
  - [x] `POST /api/v1/simulacoes/cronograma-hipotetico` sem persistir
  - [x] Query `SimularCronogramaHipoteticoQuery` reusa o calculator
  - [x] Integration test compara com contrato real equivalente

- [x] **Checkpoint Fase 2** — Frontend pode CRUD simulações
  - [x] Suite verde
  - [x] Migration aplicada em dev
  - [x] Bruno collection executa fluxo completo
  - [x] Gate humano: frontend valida shape dos DTOs

---

## Fase 3 — Integração no Quadro

- [x] **3.1 [M]** `GetQuadroDividaQuery` aceita `cenarioId`
  - [x] Buscar cenário + gerar cronogramas hipotéticos
  - [x] Converter para `EventoProjecao` (Captacao no `DataContratacaoPrevista` + Amortizacao em cada evento Principal)
  - [x] Agregar com eventos reais e passar ao `ProjetorSaldoMensal`
  - [x] Adicionar `cenarioAplicado` no DTO
  - [x] 404 em cenário inexistente; 409 se ano diferente do corrente (restrição MVP Q9)
  - [x] Property test: soma de `totalCaptacaoBrl` no ano == soma dos `valorPrincipal` das simulações

- [x] **3.2 [S]** `ProjetorSaldoMensal` aceita `EventoProjecao.Tipo = Captacao`
  - [x] Captação aumenta saldo no mês da `DataContratacaoPrevista`
  - [x] 4 unit tests + 1 property test
  - [x] Acceptance: `SaldoFinal[m] = SaldoInicial[m] - Σ Amortizações[m] + Σ Captações[m]`

- [x] **3.3 [S]** Endpoint conveniência `/simulacoes/cenarios/{id}/quadro-divida`
  - [x] Usa `cenario.AnoBase` por padrão
  - [x] Aceita `?ano=...` opcional
  - [x] Integration test: rota de atalho == chamada direta com `cenarioId`

- [x] **3.4 [S] (D-11, Q8)** Tetão mensal configurável via `ParametroSistema`
  - [x] Adicionar chave `TetaoMensalCapacidadeBrl` (decimal?) à entidade `ParametroSistema`
  - [x] Migration aditiva inserindo a chave com valor null (não-bloqueante quando não configurada)
  - [x] `ValidadorTetaoMensal` (pure function) injetado em `GetQuadroDividaQueryHandler`
  - [x] Quando configurada e algum mês `captacoes + amortizacoes > tetao` → adicionar item em `alertas[]` do `QuadroDividaDto`
  - [x] Endpoint admin `PATCH /parametros-sistema/tetao-mensal` (RBAC Admin)
  - [x] 4+ unit tests (sem config, com config + dentro, com config + estourado, múltiplos meses estourados)
  - [x] Integration test: configurar via API + consultar quadro + verificar alerta

- [x] **Checkpoint Fase 3** — Quadro renderiza com cenário aplicado
  - [x] Suite verde
  - [x] Gate humano: reproduzir manualmente 1 mês da planilha original

---

## Fase 4 — Polimento

> Paralelização total: 4.1, 4.2, 4.3, 4.4 independentes.

- [x] **4.1 [M]** Comparativo entre cenários
  - [x] `CompararCenariosQuery` + handler
  - [x] `POST /api/v1/simulacoes/comparar` (máx 5 cenários)
  - [x] DTO com deltas mensais vs. baseline
  - [x] Integration test com 3 cenários — `tests/Sgcf.Api.IntegrationTests/Simulacao/CompararCenariosApiTests.cs`

- [x] **4.2 [M]** Golden dataset — 1 mês da planilha
  - [x] `tests/Sgcf.GoldenDataset/data/quadro-divida-2026/input.json`
  - [x] `output_esperado.json` reproduzindo janeiro/2026
  - [x] Driver test com tolerância R$ 1,00
  - [x] README documenta premissas

- [x] **4.3 [M]** Documentação
  - [x] `docs/api/painel.md` — seção Quadro da Dívida + VencimentoItemDto com bancoId
  - [x] `docs/api/simulacoes.md` — novo documento (13 endpoints)
  - [x] `docs/api/parametros-sistema.md` — novo documento
  - [x] `docs/specs/simulacoes/SPEC.md` — status atualizado para Entregue
  - [x] `docs/api/schemas.md` — novos DTOs e enums do módulo Simulações
  - [x] `docs/changelog/CHANGELOG.md` — v0.10.0

- [x] **4.4 [M]** Tools MCP read-only
  - [x] `get_quadro_divida(ano, cenarioId?)`
  - [x] `list_cenarios_simulacao(status?, anoBase?)`
  - [x] `get_cenario_simulacao(id)`
  - [x] JSON Schema em `docs/mcp/tools/`
  - [x] Tests em `Sgcf.Mcp.Tests`

- [x] **Checkpoint Final — Release v0.10.0** [ENTREGUE]
  - [x] Fases 1, 2 e 3 completas (14 tasks implementadas)
  - [x] Suite fast: 847 testes verdes
  - [x] Suite Slow: ~60 testes (Testcontainers Postgres + Redis)
  - [x] Cobertura: Painel ≥ 90%, Simulacao ≥ 85%
  - [x] Migrations S9 e S10 aplicadas
  - [x] Documentação API completa (docs/)
  - [x] CHANGELOG v0.10.0 publicado — ver [`docs/changelog/CHANGELOG.md`](../../docs/changelog/CHANGELOG.md#0100--2026-05-19)
  - [ ] Gate humano: sponsor valida que pode aposentar a aba `Quadro_da_Divida` da planilha _(pendente)_
  - [ ] 4.1 (Comparativo de cenários), 4.2 (Golden dataset) e 4.4 (MCP tools) — próximo sprint

---

## Resumo

- **4 fases** sequenciais com checkpoints
- **14 tasks** (6 S + 10 M + 0 L) — tamanho médio adequado
- **5 oportunidades de paralelização** (1.1/1.2/1.3, 2.2/2.4, 4.1/4.2/4.3/4.4)
- **12 endpoints REST novos** + **3 tools MCP** + **1 migration**
- **Release alvo:** v0.7.0
- **Sem mudanças destrutivas** em APIs existentes (apenas campos aditivos em `VencimentoItemDto` e `QuadroDividaDto`)
