# Todo — Snapshot Temporal de Garantias no Contrato (S34)

> Plano detalhado: `tasks/plan_snapshot_limite_contrato.md` v1.0
> SPEC: `sgcf-backend/docs/specs/limites-banco/SPEC_SNAPSHOT_LIMITE_NO_CONTRATO.md`
> Marque cada item ao concluir.

## Fase 0 — Foundation

- [x] **T0.1** Renomear `GarantiaExigidaLimite` → `GarantiaExigidaItem` e Spec correspondente. Refator mecânico, sem mudança de comportamento.
- [x] **T0.2** Adicionar entidade `GarantiaExigidaRevisao` + tests SR-01..SR-08. Coexiste sem uso.
- [x] **T0.3** Refatorar `LimiteBanco` para operar com revisões. Métodos públicos com mesma assinatura passam a abrir nova revisão. Tests SLB-01..SLB-05.
- [x] **T0.4** `Contrato`: 3 campos (`LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId`) + `VincularPoliticaBanco` + tests SC-05.
- [x] **T0.5** EF Configurations: nova `GarantiaExigidaRevisaoConfiguration`, rename `GarantiaExigidaItemConfiguration`, ajustar `LimiteBancoConfiguration` e `ContratoConfiguration` (3 FKs, ON DELETE SET NULL).
- [x] **T0.6** Migration `S34_SnapshotGarantiasContrato` com backfill in-migration. Tests Testcontainers validam backfill (TC-01..TC-08 verdes).

### Checkpoint Fase 0 — Foundation Pronta
- [x] `dotnet build` sucesso.
- [x] `dotnet test` (incluindo Slow) verde — 1.247 fast + 8 slow = 0 falhas.
- [ ] `dotnet ef database update` aplicado local sem erro.
- [ ] `grep -r "GarantiaExigidaLimite" src/ tests/` = 0 ocorrências.
- [ ] Backfill validation: `SELECT COUNT(*) FROM garantia_exigida_item WHERE revisao_id IS NULL` = 0.
- [ ] Revisão humana antes de Fase 1.

---

## Fase 1 — Versionamento Operacional (Lacuna 1)

- [x] **T1.1** Integration test `LimiteBancoPatchAbreRevisaoTests` valida PATCH → revisão fechada + nova vigente. Handlers existentes já eram compatíveis (mesma assinatura externa).
- [x] **T1.2** `ListarRevisoesGarantiasQuery` + handler + `GarantiaExigidaRevisaoDto` + 4 tests.
- [x] **T1.3** Endpoint `GET /api/v1/limites-banco/{id}/revisoes-garantias` + 5 integration tests HTTP.
- [x] **T1.4** Endpoint `DELETE /api/v1/limites-banco/{id}/garantias-exigidas?tipo=X` + 5 integration tests. **Sem 410 Gone**: endpoint antigo (`/{itemId}`) não existia no codebase; nada a deprecar.

### Checkpoint Fase 1 — Versionamento (Lacuna 1 fechada)
- [x] PATCH gera nova revisão (integration test verde).
- [x] `GET /revisoes-garantias` retorna histórico ordenado.
- [x] `DELETE ?tipo=X` implementado (sem deprecação 410).
- [x] Smoke query forense documentada em smoke_snapshot_limite_contrato.md §3.8.

---

## Fase 2 — Rastreabilidade (Lacuna 2)

- [x] **T2.1** `ConverterEmContratoHandler` preenche `LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId`. SC-01..SC-03. 4 tests novos.
- [x] **T2.2** `ContratoDto` estendido + `GarantiaExigidaSnapshotItemDto` novo. Detalhe inclui snapshot, listagem omite.
- [x] **T2.3** Integration tests para `GET /contratos/{id}` e `GET /contratos` — 5 cases HTTP (snapshot, legado, listagem leve, congelamento pós-PATCH, multi-tenant).
- [x] **T2.4** `Sgcf.Mcp/Tools/ContratoTools` reflete novos campos automaticamente; 2 smoke tests MCP.

### Checkpoint Fase 2 — Rastreabilidade (Lacuna 2 fechada)
- [x] Contrato novo carrega 3 FKs e snapshot.
- [x] Contrato legado retorna 3 FKs como `null` sem erro.
- [x] Listagem não inclui payload pesado.
- [x] MCP schema atualizado.

---

## Fase 3 — Enforcement (Lacuna 3)

- [x] **T3.1** Enforcement SC-04 no `ConverterEmContratoHandler` + `GarantiaExigidaNaoCobertaException`. Reutiliza `CalculadorValorGarantiaExigida`.
- [x] **T3.2** `GlobalExceptionHandler` mapeia exceção para `409 Conflict` com ProblemDetails (body conforme SPEC §4.5).
- [x] **T3.3** Matriz de tests: 14 Application unit + 3 HTTP integration cobrindo tipos × cenários (cobertura completa/parcial/zero/excedente + Aval puro + item não-obrigatório + SC-07).

### Checkpoint Fase 3 — Enforcement (Lacuna 3 fechada)
- [x] Conversão bloqueia obrigatórias sem cobertura com `409` + body completo.
- [x] Conversão sucede com cobertura completa/excedente.
- [x] Bancos sem `LimiteBanco`/revisão convertem sem enforcement.
- [x] Matriz de testes verde.

---

## Fase 4 — Polish

- [x] **T4.1** OpenAPI/Swagger: endpoints novos têm `ProducesResponseType` + `Authorize` completos; Swashbuckle gera automaticamente.
- [x] **T4.2** Changelog `[0.11.0] — 2026-05-25` em `sgcf-backend/docs/changelog/CHANGELOG.md` (BREAKING + ADDITIVE + INTERNAL sections).
- [x] **T4.3** Roteiro de smoke tests em `tasks/smoke_snapshot_limite_contrato.md` (sanity check + backfill + RLS + 8 cenários HTTP + monitoramento + rollback).

### Checkpoint Final — §9.4 da SPEC
- [ ] Migration aplicada em staging. **(operacional — pendente deploy)**
- [ ] Backfill validation OK em staging. **(operacional — pendente deploy)**
- [x] RLS ativa em `garantia_exigida_revisao` (verificado em test S34_BackfillTests TC-04/05).
- [ ] Cobertura: Domain ≥95%, Application ≥90%, Infrastructure ≥75%. **(verificar com `dotnet test --collect:"XPlat Code Coverage"`)**
- [x] OpenAPI completo (ProducesResponseType + Authorize em todos os endpoints novos).
- [x] Suite completo verde: 1271/1271 fast tests + 8/8 slow tests (TC-01..TC-08).
- [x] Todas as invariantes SR-01..SR-08, SLB-01..SLB-05, SC-01..SC-07 cobertas por ≥ 1 test.
- [x] Changelog publicado (`CHANGELOG.md` [0.11.0]).
- [ ] Smoke test em staging executado (runbook pronto em `tasks/smoke_snapshot_limite_contrato.md`). **(operacional — pendente deploy)**
- [ ] Frontend Nordware sinalizado. **(coordenação externa pendente)**

---

## Decisões do PO — 2026-05-25

- [x] Modelo: versionar `GarantiaExigidaLimite` em vez de JSONB no Contrato.
- [x] Enforcement na conversão entra nesta SPEC (Lacuna 3 dentro do escopo).
- [x] Endpoint genérico "política em data X": fora do escopo desta fase.
- [x] `LimiteGlobalBanco` permanece sem `garantiasExigidas` próprio; FK no contrato é só rastreabilidade.

## Pontos abertos para confirmar durante o build

- [ ] Critério temporal de vigência do `LimiteBanco` na conversão (default: `cmd.DataContratacao`). Confirmar em T2.1.
- [ ] PATCH com `garantiasExigidas: null` preserva revisão (default: sim). Confirmar em T1.1.
- [x] Texto padrão do `Motivo` no backfill: "Revisão inicial gerada pela migration S34" — confirmado em T0.6.
- [x] `Down()` destrutivo + documentado — confirmado em T0.6.
- [ ] Frontend pode consumir sem feature-flag (default: sim, campos chegam optional). Confirmar pós-Fase 2.
