# Todo — Snapshot Temporal de Garantias no Contrato (S34)

> Plano detalhado: `tasks/plan_snapshot_limite_contrato.md` v1.0
> SPEC: `sgcf-backend/docs/specs/limites-banco/SPEC_SNAPSHOT_LIMITE_NO_CONTRATO.md`
> Marque cada item ao concluir.

## Fase 0 — Foundation

- [ ] **T0.1** Renomear `GarantiaExigidaLimite` → `GarantiaExigidaItem` e Spec correspondente. Refator mecânico, sem mudança de comportamento.
- [ ] **T0.2** Adicionar entidade `GarantiaExigidaRevisao` + tests SR-01..SR-08. Coexiste sem uso.
- [ ] **T0.3** Refatorar `LimiteBanco` para operar com revisões. Métodos públicos com mesma assinatura passam a abrir nova revisão. Tests SLB-01..SLB-05.
- [ ] **T0.4** `Contrato`: 3 campos (`LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId`) + `VincularPoliticaBanco` + tests SC-05.
- [ ] **T0.5** EF Configurations: nova `GarantiaExigidaRevisaoConfiguration`, rename `GarantiaExigidaItemConfiguration`, ajustar `LimiteBancoConfiguration` e `ContratoConfiguration` (3 FKs, ON DELETE SET NULL).
- [ ] **T0.6** Migration `S34_SnapshotGarantiasContrato` com backfill in-migration. Tests Testcontainers validam backfill.

### Checkpoint Fase 0 — Foundation Pronta
- [ ] `dotnet build` sucesso.
- [ ] `dotnet test` (incluindo Slow) verde.
- [ ] `dotnet ef database update` aplicado local sem erro.
- [ ] `grep -r "GarantiaExigidaLimite" src/ tests/` = 0 ocorrências.
- [ ] Backfill validation: `SELECT COUNT(*) FROM garantia_exigida_item WHERE revisao_id IS NULL` = 0.
- [ ] Revisão humana antes de Fase 1.

---

## Fase 1 — Versionamento Operacional (Lacuna 1)

- [ ] **T1.1** Atualizar handlers existentes (`CreateLimiteBancoHandler`, `UpdateLimiteBancoHandler`, adicionar/remover garantia individual) para nova semântica via revisão.
- [ ] **T1.2** `ListarRevisoesGarantiasQuery` + handler + `GarantiaExigidaRevisaoDto`.
- [ ] **T1.3** Endpoint `GET /api/v1/limites-banco/{id}/revisoes-garantias` + integration tests.
- [ ] **T1.4** Deprecar `DELETE /api/v1/limites-banco/{id}/garantias-exigidas/{itemId}` → `410 Gone`. Implementar `DELETE /...?tipo=X`.

### Checkpoint Fase 1 — Versionamento (Lacuna 1 fechada)
- [ ] PATCH gera nova revisão (integration test verde).
- [ ] `GET /revisoes-garantias` retorna histórico ordenado.
- [ ] Endpoint deprecated responde `410`.
- [ ] Smoke query forense OK.
- [ ] Revisão humana antes de Fase 2.

---

## Fase 2 — Rastreabilidade (Lacuna 2)

- [ ] **T2.1** `ConverterEmContratoHandler` preenche `LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId`. SC-01..SC-03.
- [ ] **T2.2** `ContratoDto` estendido + `GarantiaExigidaSnapshotItemDto` novo. Detalhe inclui snapshot, listagem não.
- [ ] **T2.3** Integration tests para `GET /contratos/{id}` e `GET /contratos`.
- [ ] **T2.4** Atualizar `Sgcf.Mcp/Tools/ContratoTools` para refletir novos campos.

### Checkpoint Fase 2 — Rastreabilidade (Lacuna 2 fechada)
- [ ] Contrato novo carrega 3 FKs e snapshot.
- [ ] Contrato legado retorna 3 FKs como `null` sem erro.
- [ ] Listagem não inclui payload pesado.
- [ ] MCP schema atualizado.
- [ ] Revisão humana antes de Fase 3.

---

## Fase 3 — Enforcement (Lacuna 3)

- [ ] **T3.1** Enforcement SC-04 no `ConverterEmContratoHandler` + `GarantiaExigidaNaoCobertaException`. Reutiliza `CalculadorValorGarantiaExigida`.
- [ ] **T3.2** `ExceptionHandlingMiddleware` mapeia exceção para `409 Conflict` com body §4.5.
- [ ] **T3.3** Matriz de tests (Application + HTTP) cobrindo tipos × cenários (≥ 12 tests Application + ≥ 3 HTTP).

### Checkpoint Fase 3 — Enforcement (Lacuna 3 fechada)
- [ ] Conversão bloqueia obrigatórias sem cobertura com `409` + body completo.
- [ ] Conversão sucede com cobertura completa/excedente.
- [ ] Bancos sem `LimiteBanco`/revisão convertem sem enforcement.
- [ ] Matriz de testes verde.
- [ ] Revisão humana antes de Fase 4.

---

## Fase 4 — Polish

- [ ] **T4.1** Verificar OpenAPI/Swagger inclui rotas novas e campos novos.
- [ ] **T4.2** Changelog `S34_SnapshotGarantiasContrato.md`.
- [ ] **T4.3** Roteiro de smoke tests pós-deploy documentado.

### Checkpoint Final — §9.4 da SPEC
- [ ] Migration aplicada em staging.
- [ ] Backfill validation OK.
- [ ] RLS ativa em `garantia_exigida_revisao`.
- [ ] Cobertura: Domain ≥95%, Application ≥90%, Infrastructure ≥75%.
- [ ] OpenAPI completo.
- [ ] Suite completo verde.
- [ ] Todas as invariantes SR-01..SR-08, SLB-01..SLB-05, SC-01..SC-07 cobertas por ≥ 1 test.
- [ ] Changelog publicado.
- [ ] Smoke test em staging executado.
- [ ] Frontend Nordware sinalizado.

---

## Decisões do PO — 2026-05-25

- [x] Modelo: versionar `GarantiaExigidaLimite` em vez de JSONB no Contrato.
- [x] Enforcement na conversão entra nesta SPEC (Lacuna 3 dentro do escopo).
- [x] Endpoint genérico "política em data X": fora do escopo desta fase.
- [x] `LimiteGlobalBanco` permanece sem `garantiasExigidas` próprio; FK no contrato é só rastreabilidade.

## Pontos abertos para confirmar durante o build

- [ ] Critério temporal de vigência do `LimiteBanco` na conversão (default: `cmd.DataContratacao`). Confirmar em T2.1.
- [ ] PATCH com `garantiasExigidas: null` preserva revisão (default: sim). Confirmar em T1.1.
- [ ] Texto padrão do `Motivo` no backfill (default: "Revisão inicial gerada pela migration S34"). Confirmar em T0.6.
- [ ] Implementar `Down()` destrutivo ou bloqueá-lo (default: destrutivo + documentado). Confirmar em T0.6.
- [ ] Frontend pode consumir sem feature-flag (default: sim, campos chegam optional). Confirmar pós-Fase 2.
