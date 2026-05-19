# Todo — Quadro da Dívida + Simulação de Contratação

> Plano detalhado: `plan.md` neste diretório.
> Marque cada item ao concluir. Ordem segue o grafo de dependências.

---

## Bloqueio externo

- [ ] **Aprovação humana** das 12 Decisões Arquiteturais (plan.md §2)
- [x] **Decisão** das 8 Open Questions (plan.md §10) — concluído em 2026-05-19; 3 mudanças vs proposta MVP (Q6, Q7, Q8) geraram 3 tasks novas abaixo.

---

## Fase 1 — Quadro da Dívida (real-only)

> Paralelização: 1.1, 1.2, 1.3 podem rodar em paralelo. 1.4 depende dos 3.

- [ ] **1.1 [M]** `ProjetorSaldoMensal` — pure function no domínio
  - [ ] Criar `EventoProjecao`, `TipoEventoProjecao`, `QuadroDividaProjecao`, `MesProjecao`, `SaldoBancoMes` em `Sgcf.Domain.Painel`
  - [ ] Implementar `ProjetorSaldoMensal.Projetar(saldoInicial, eventos, ano)`
  - [ ] 8+ unit tests + 2 property tests (FsCheck)
  - [ ] Acceptance: invariantes `saldoFim[m] == saldoInicio[m+1]`, soma de shares = 100%
  - [ ] Verify: `dotnet test --filter "FullyQualifiedName~ProjetorSaldoMensal"`

- [ ] **1.2 [S]** `GetSaldoPorBancoAtualQuery` — saldo atual por banco
  - [ ] Query + Handler + DTO em `Sgcf.Application.Painel.Queries`
  - [ ] Reuso da lógica de conversão de moeda do `GetPainelDividaQueryHandler`
  - [ ] Integration test com Testcontainers
  - [ ] Acceptance: soma por banco == saldo total do `/painel/divida`

- [ ] **1.3 [S]** Estender `VencimentoItemDto` com `bancoId` + `bancoApelido`
  - [ ] Atualizar DTO em `Sgcf.Application.Painel`
  - [ ] Popular no `GetCalendarioVencimentosQueryHandler`
  - [ ] Pelo menos 1 teste novo
  - [ ] Acceptance: contrato existente não quebra (campo aditivo)

- [ ] **1.4 [M]** `GetQuadroDividaQuery` + endpoint `/painel/quadro-divida`
  - [ ] Query, Handler, DTOs (`QuadroDividaDto`, `MesQuadroDividaDto`, `SaldoBancoMesDto`)
  - [ ] Orquestra 1.1 + 1.2 + repositório de eventos de cronograma
  - [ ] Adicionar action no `PainelController`: `GET /api/v1/painel/quadro-divida?ano=...&bancoId=...&modalidade=...`
  - [ ] E2E Integration test com 3 contratos
  - [ ] Acceptance: resposta inclui snapshot atual + meses[12] + sumario[12]; `cenarioAplicado: null`

- [ ] **Checkpoint Fase 1** — Frontend pode renderizar quadro base (sem simulações)
  - [ ] Suite verde: `dotnet test --filter "Category!=Slow"`
  - [ ] Bruno request `07-Painel/6 - Quadro da Divida.bru` executa contra dev
  - [ ] Gate humano: frontend valida shape do DTO

---

## Fase 2 — Simulação de Contratação (domínio + persistência + API)

> Paralelização: 2.4 paralelo com 2.2 (não depende). 2.3 depende de 2.2. 2.5 depende de 2.3.

- [ ] **2.1 [M]** Domain `CenarioSimulacao` + `SimulacaoContratacao`
  - [ ] Criar agregado raiz `CenarioSimulacao` com lifecycle (`Rascunho`/`Ativo`/`Arquivado`)
  - [ ] Criar child entity `SimulacaoContratacao` com todos os campos para gerar cronograma
  - [ ] **D-9 (Q6)**: incluir `GarantiaExigidaPrevista : string?` (máx 500 chars) — informativo, sem validação
  - [ ] Criar enum `StatusCenarioSimulacao`
  - [ ] Criar interface `ICenarioSimulacaoRepository`
  - [ ] 20+ unit tests cobrindo criação, transições, validações
  - [ ] Property test: cenário arquivado é imutável

- [ ] **2.1b [S] (D-10, Q7)** Domain — Factory `CenarioSimulacao.DuplicarComoRascunho`
  - [ ] Método estático recebe cenário origem + `criadoPor` novo + clock
  - [ ] Cópia profunda: todas `SimulacaoContratacao` filhas duplicadas com novos Ids
  - [ ] Nome sufixado `" (cópia)"`; status `Rascunho`; `CreatedAt = now`
  - [ ] 6+ unit tests (cópia simples, cópia com N filhas, cenário arquivado pode ser duplicado, audit fields novos)

- [ ] **2.2 [M]** Migration + EF Configurations + Repository
  - [ ] Migration `S8_SimulacaoContratacao` com 2 tabelas
  - [ ] `CenarioSimulacaoConfiguration` + `SimulacaoContratacaoConfiguration`
  - [ ] Índices `(status, criado_por)`, `(cenario_id)`, `(banco_id)`
  - [ ] Soft delete query filter
  - [ ] Implementar `CenarioSimulacaoRepository`
  - [ ] DbSets no `SgcfDbContext`
  - [ ] Verify: `dotnet ef migrations add` aplica limpo; integration test CRUD passa

- [ ] **2.3 [L → fragmentar]** Application — CRUD do Cenário
  - [ ] `CriarCenarioSimulacaoCommand` + handler + validator
  - [ ] `AtualizarCenarioCommand` + handler + validator
  - [ ] `AtivarCenarioCommand` + handler
  - [ ] `ArquivarCenarioCommand` + handler
  - [ ] `DuplicarCenarioCommand` + handler + validator **(D-10, Q7)**
  - [ ] `DeletarCenarioCommand` + handler
  - [ ] `AdicionarSimulacaoCommand` + handler + validator (inclui `garantiaExigidaPrevista` opcional — D-9)
  - [ ] `RemoverSimulacaoCommand` + handler
  - [ ] `GetCenarioSimulacaoByIdQuery` + handler
  - [ ] `ListCenariosSimulacaoQuery` + handler
  - [ ] DTOs: `CenarioSimulacaoDto`, `SimulacaoContratacaoDto` (com `garantiaExigidaPrevista`)
  - [ ] Cobertura ≥ 85% (handlers + validators)

- [ ] **2.4 [S]** `SimulacaoCronogramaCalculator`
  - [ ] Helper que constrói `GerarCronogramaInput` a partir de `SimulacaoContratacao`
  - [ ] Reusa `CronogramaStrategyFactory.Criar(estrutura).Gerar(...)`
  - [ ] 4 golden tests (Bullet, BulletComJuros, Price, SAC)
  - [ ] Acceptance: cronograma idêntico ao de um contrato real equivalente

- [ ] **2.5 [M]** API REST CRUD — `SimulacoesController`
  - [ ] 10 endpoints (POST/GET/PATCH/DELETE de cenário + simulação + ativar/arquivar/**duplicar**) **(D-10, Q7)**
  - [ ] OpenAPI/Swagger com exemplos
  - [ ] Idempotência via `Idempotency-Key`
  - [ ] RBAC: policies `Tesouraria`, `Gerente`, `Leitura`
  - [ ] Smoke E2E: criar → adicionar 3 sims → ativar → **duplicar** → consultar → arquivar

- [ ] **2.6 [S]** Endpoint preview de cronograma hipotético
  - [ ] `POST /api/v1/simulacoes/cronograma-hipotetico` sem persistir
  - [ ] Query `SimularCronogramaHipoteticoQuery` reusa o calculator
  - [ ] Integration test compara com contrato real equivalente

- [ ] **Checkpoint Fase 2** — Frontend pode CRUD simulações
  - [ ] Suite verde
  - [ ] Migration aplicada em dev
  - [ ] Bruno collection executa fluxo completo
  - [ ] Gate humano: frontend valida shape dos DTOs

---

## Fase 3 — Integração no Quadro

- [ ] **3.1 [M]** `GetQuadroDividaQuery` aceita `cenarioId`
  - [ ] Buscar cenário + gerar cronogramas hipotéticos
  - [ ] Converter para `EventoProjecao` (Captacao no `DataContratacaoPrevista` + Amortizacao em cada evento Principal)
  - [ ] Agregar com eventos reais e passar ao `ProjetorSaldoMensal`
  - [ ] Adicionar `cenarioAplicado` no DTO
  - [ ] 404 em cenário inexistente; 422 se `anoBase != ano`
  - [ ] Property test: soma de `totalCaptacaoBrl` no ano == soma dos `valorPrincipal` das simulações

- [ ] **3.2 [S]** `ProjetorSaldoMensal` aceita `EventoProjecao.Tipo = Captacao`
  - [ ] Captação aumenta saldo no mês da `DataContratacaoPrevista`
  - [ ] 4 unit tests + 1 property test
  - [ ] Acceptance: `SaldoFinal[m] = SaldoInicial[m] - Σ Amortizações[m] + Σ Captações[m]`

- [ ] **3.3 [S]** Endpoint conveniência `/simulacoes/cenarios/{id}/quadro-divida`
  - [ ] Usa `cenario.AnoBase` por padrão
  - [ ] Aceita `?ano=...` opcional
  - [ ] Integration test: rota de atalho == chamada direta com `cenarioId`

- [ ] **3.4 [S] (D-11, Q8)** Tetão mensal configurável via `ParametroSistema`
  - [ ] Adicionar chave `TetaoMensalCapacidadeBrl` (decimal?) à entidade `ParametroSistema`
  - [ ] Migration aditiva inserindo a chave com valor null (não-bloqueante quando não configurada)
  - [ ] `ValidadorTetaoMensal` (pure function) injetado em `GetQuadroDividaQueryHandler`
  - [ ] Quando configurada e algum mês `captacoes + amortizacoes > tetao` → adicionar item em `alertas[]` do `QuadroDividaDto`
  - [ ] Endpoint admin `PATCH /parametros-sistema/tetao-mensal` (RBAC Admin)
  - [ ] 4+ unit tests (sem config, com config + dentro, com config + estourado, múltiplos meses estourados)
  - [ ] Integration test: configurar via API + consultar quadro + verificar alerta

- [ ] **Checkpoint Fase 3** — Quadro renderiza com cenário aplicado
  - [ ] Suite verde
  - [ ] Gate humano: reproduzir manualmente 1 mês da planilha original

---

## Fase 4 — Polimento

> Paralelização total: 4.1, 4.2, 4.3, 4.4 independentes.

- [ ] **4.1 [M]** Comparativo entre cenários
  - [ ] `CompararCenariosQuery` + handler
  - [ ] `POST /api/v1/simulacoes/comparar` (máx 5 cenários)
  - [ ] DTO com deltas mensais vs. baseline
  - [ ] Integration test com 3 cenários

- [ ] **4.2 [M]** Golden dataset — 1 mês da planilha
  - [ ] `tests/Sgcf.GoldenDataset/data/quadro-divida-2026/input.json`
  - [ ] `output_esperado.json` reproduzindo janeiro/2026
  - [ ] Driver test com tolerância R$ 1,00
  - [ ] README documenta premissas

- [ ] **4.3 [M]** Documentação
  - [ ] `docs/api/painel.md` — seção Quadro da Dívida
  - [ ] `docs/api/simulacoes.md` — novo documento
  - [ ] `docs/specs/simulacoes/SPEC.md` — spec consolidado
  - [ ] Bruno `13-Simulacoes/*` (10+ requests)
  - [ ] Bruno `07-Painel/6 - Quadro da Divida.bru`
  - [ ] `docs/api/schemas.md` — novos DTOs
  - [ ] `docs/changelog/CHANGELOG.md` — v0.7.0

- [ ] **4.4 [M]** Tools MCP read-only
  - [ ] `get_quadro_divida(ano, cenarioId?)`
  - [ ] `list_cenarios_simulacao(status?, anoBase?)`
  - [ ] `get_cenario_simulacao(id)`
  - [ ] JSON Schema em `docs/mcp/tools/`
  - [ ] Tests em `Sgcf.Mcp.Tests`

- [ ] **Checkpoint Final — Release v0.7.0**
  - [ ] Todas as 14 tasks mergeadas
  - [ ] Suite completa verde (sem `--filter`)
  - [ ] Cobertura: Painel ≥ 90%, Simulacao ≥ 85%
  - [ ] Migration aplicada em staging
  - [ ] Bruno collection end-to-end ok
  - [ ] CHANGELOG v0.7.0 publicado
  - [ ] Gate humano: sponsor valida que pode aposentar a aba `Quadro_da_Divida` da planilha

---

## Resumo

- **4 fases** sequenciais com checkpoints
- **14 tasks** (6 S + 10 M + 0 L) — tamanho médio adequado
- **5 oportunidades de paralelização** (1.1/1.2/1.3, 2.2/2.4, 4.1/4.2/4.3/4.4)
- **12 endpoints REST novos** + **3 tools MCP** + **1 migration**
- **Release alvo:** v0.7.0
- **Sem mudanças destrutivas** em APIs existentes (apenas campos aditivos em `VencimentoItemDto` e `QuadroDividaDto`)
