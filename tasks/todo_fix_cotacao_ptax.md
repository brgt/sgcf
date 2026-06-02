# Todo — Correção dos Bugs de Cotação / PTAX

> Plano: `tasks/plan_fix_cotacao_ptax.md` · Spec: `SPEC_FIX_COTACAO_PTAX.md`
> Marque cada item ao concluir. Critérios de aceite detalhados no plano.

## Fase 1 — Núcleo (desbloqueio)
- [x] **T1** Prove-It: off-by-one guardado no nível unitário (asserção `ResolverFxAsync(..., dataAbertura)` em CriarCotacaoCommandHandlerTests) **+** Prove-It de integração Testcontainers `CriarCotacaoPtaxD0Tests` (banco só-`PtaxD0`, demonstrado red→green) → AC-1/AC-4 ✓
- [x] **T2** Método `ResolverFxAsync` no `IResolveTipoCotacaoService` + impl (extrai tradução D1→D0(D-1)) — 11/11 unit ✓ (commit 170f071)
- [x] **T3** Perfil A: `CriarCotacao` (passa `dataAbertura`) + `RegistrarProposta`/`AtualizarProposta` (PtaxD0 exata) + fixtures (PtaxD0) → AC-1/AC-3 ✓ (commit 48fa1bc)

### ✅ Checkpoint Fase 1
- [x] `dotnet build` solução limpo + `Application.Tests` não-slow 464/464 verdes
- [x] Off-by-one coberto (unit). Prove-It de integração pendente (T1)
- [x] Branch `fix/cotacao-ptax-d1-d0`; seed dev ajustado para `PtaxD0` (5,0357)
- [ ] **Revisão humana antes da Fase 2** ← aqui

## Fase 2 — Propagação (Perfil B, mid-rate preservado)
- [x] **T4** Reroute Painel (10 handlers) → AC-2 ✓ (resolver injetado; mid-rate intacto; 464/464 não-slow + 43 Painel verdes; 6 fixtures de teste migradas p/ `IResolveTipoCotacaoService`)
- [x] **T5** Reroute Tesouraria (3) + Sensibilidade (1) + RefreshMercado (1) ✓ — RefreshMercado migrado p/ **spot-first, fallback PtaxD0 de H** (decisão humana 02/jun); 4 fixtures de teste atualizadas; 465/465 não-slow verdes
- [x] **T6** Reroute Jobs (2: RecalcularMtm, SnapshotMensal) + MCP (1: DividaTools) ✓ — `sp.GetRequiredService<IResolveTipoCotacaoService>()`; DividaTools só depende de `Application` (Infrastructure só no `Program.cs`, composition root); 2 fixtures MCP migradas

### ✅ Checkpoint Fase 2
- [x] `grep` confirma zero chamadas diretas a `GetMaisRecenteAsync(..., PtaxD1, ...)` em `src`
- [x] Painéis/Tesouraria/Jobs/MCP cambiais roteados pelo resolver (funcionam com banco só-`PtaxD0`)
- [x] `dotnet test --filter "Category!=Slow"` verde — Domain 755, Application 465, MCP 45, Jobs 4
- [ ] **Revisão humana antes da Fase 3** ← aqui

## Fase 3 — Cadastro manual, varredura e guia
- [x] **T7** Endpoint admin `POST /cotacoes-fx` + `GET` conferência + `RegistrarCotacaoFxCommand`/validator/`CotacaoFxDto`/`BuscarCotacaoFxQuery` + 6 testes de integração (AC-6 grava PtaxD0 + idempotência; AC-7 403 não-admin + 400 inválidos) ✓ — `TestAuthHandler` estendido com header `X-Test-Roles` (retrocompatível)
- [x] **T8** Varredura de fixtures: 6 seeds/objetos `CotacaoFx` migrados `PtaxD1`→`PtaxD0` (RF-10; 2 fixtures de integração load-bearing + 4 unitárias; preservado o tipo lógico `PtaxD1` em stubs/resolver-tests) + teste de paridade Perfil A×B `CotacaoResolverParidadePerfilTests` (RF-11) ✓
- [x] **T9** `docs/api/GUIA_FRONT_COTACOES.md` (RF-12) ✓ — cobre (a) tipo vs taxa, (b) enum + contrato 400, (c) `POST /cotacoes-fx`/ingestão, (d) `GET /parametros-cotacao/resolve`

### ✅ Checkpoint Completo
- [x] Cotação funciona alimentando só `PtaxD0` — provado por `CriarCotacaoPtaxD0Tests` (integração) + `CotacoesFluxoTests`/`ContratoSnapshot` verdes após migração das fixtures para `PtaxD0` (sem seed via SQL)
- [x] Guia do front publicado
- [x] Suíte não-slow verde: Domain 755 · Application 466 · MCP 45 · Jobs 4
- [x] Integração (Slow): **todos os testes de cotação/painel/contrato verdes** (CriarCotacaoPtaxD0, CotacaoFxEndpoint, CotacoesFluxo, ContratoSnapshot)
- [~] `dotnet test` Slow completo: **4 falhas pré-existentes** em `LimitesBancoGarantias`(3)+`GarantiaPreenchimento`(1) — **provado** que falham idênticas no HEAD (48fa1bc) sem minhas mudanças; pertencem ao WIP de garantias-alternativas (fora deste escopo)
- [ ] Pronto para `/review` e PR (pós-revisão humana)

## Pendências (fora do escopo deste plano)
- [x] `RefreshCotacaoMercado`: **decidido migrar para D0/spot** (spot-first, fallback PtaxD0 de H) — aplicado na T5
- [ ] Investigar os dois diretórios de migrations (`Migrations/` vs `Persistence/Migrations/`)
- [ ] **Achado**: 4 testes de `LimitesBancoGarantias`/`GarantiaPreenchimento` vermelhos no HEAD (feature garantias incompleta) — reportar ao responsável pelo stream de garantias-alternativas
