# Plano de Implementação — Snapshot Temporal de Garantias no Contrato (S34)

**Versão:** 1.0
**Data:** 2026-05-25
**SPEC âncora:** `sgcf-backend/docs/specs/limites-banco/SPEC_SNAPSHOT_LIMITE_NO_CONTRATO.md`
**Investigação de origem:** `sgcf-backend/docs/specs/limites-banco/PROPOSTA_SNAPSHOT_LIMITE_NO_CONTRATO.md`
**Esforço estimado:** 2 a 3 sprints.
**Dependências externas:** `LimiteBanco`, `LimiteGlobalBanco` (S33), `Contrato`, `ConverterEmContratoCommand` — todos já entregues.
**Sem dependentes:** o frontend Nordware consome os novos campos sob feature-flag/optional types e não bloqueia.

---

## 1. Visão Geral

Transformar a coleção mutável `LimiteBanco.GarantiasExigidas` em estrutura versionada (`GarantiaExigidaRevisao`), vincular cada `Contrato` à revisão vigente no momento da contratação por três FKs nullable (`LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId`), e bloquear a conversão cotação→contrato quando garantias obrigatórias da revisão vigente não estiverem cobertas.

Três lacunas resolvidas:
1. **Histórico de política**: cada PATCH gera nova revisão; revisões antigas ficam imutáveis (Lacuna 1).
2. **Rastreabilidade**: contrato sabe sob qual limite e qual revisão foi criado (Lacuna 2).
3. **Enforcement**: conversão bloqueia (409) quando obrigatórias ficam sem cobertura (Lacuna 3).

---

## 2. Decisões Arquiteturais

### 2.1 Versionamento via entidade dedicada — não JSONB

`GarantiaExigidaRevisao` é entidade própria com tabela própria. Itens ficam em `garantia_exigida_item` (renomeada de `garantia_exigida_limite`) com FK para revisão. Razão: queries cross-contract habilitadas, schema evoluível, índices nativos. JSONB foi avaliado e descartado pela impossibilidade de correlação entre contratos.

### 2.2 Append-only com `Instant` em vigência

Revisões nunca são atualizadas após criação; itens nunca são alterados após `VigenciaFim` da revisão pai. Precisão de `Instant` (microssegundos) evita ambiguidade quando múltiplos PATCHes ocorrem no mesmo dia.

### 2.3 Migração in-migration (sem janela)

A migration `S34_SnapshotGarantiasContrato` faz backfill em uma única transação:
1. `CREATE TABLE garantia_exigida_revisao`.
2. Adiciona `revisao_id` em `garantia_exigida_limite`.
3. Para cada limite com itens, cria uma revisão inicial e popula `revisao_id`.
4. Torna `revisao_id NOT NULL`.
5. Remove `limite_banco_id` da tabela de itens.
6. Renomeia tabela para `garantia_exigida_item`.
7. Adiciona 3 colunas NULL em `contrato`.

Tempo total esperado: < 5s em base com 100k linhas. Sem rollback automático em produção (Down destrutivo, documentado).

### 2.4 Endpoint `DELETE /garantias-exigidas/{itemId}` deprecado

Razão: um Id de item passa a pertencer a uma revisão específica que pode estar fechada — operar por Id quebra a invariante de imutabilidade. Substituído por `DELETE /garantias-exigidas?tipo=X` que abre nova revisão. Endpoint antigo retorna `410 Gone` com header `Location`. Remoção física fica para fase posterior.

### 2.5 Enforcement só na conversão, não em outras criações de contrato

`ConverterEmContratoHandler` é o único caminho com cotação aprovada → contrato. Outras criações (admin, importação histórica) ficam fora do enforcement por design — não há cotação de referência e dados podem ser legados. Marcador no `CreateContratoCommand`: campo opcional `validarGarantiasVigentes` (default `false`).

### 2.6 Imutabilidade dos 3 campos no `Contrato` enforced no domain

`VincularPoliticaBanco` aceita re-chamada idempotente com mesmos valores (defensive coding para retries de transação); rejeita valores diferentes. `Contrato.Atualizar` não toca esses campos.

### 2.7 Naming: rename de `GarantiaExigidaLimite` → `GarantiaExigidaItem`

Custo de rename amplo (~14 arquivos). Benefício: evita confusão "uma garantia por limite". O nome anterior era natural quando havia 1 nível; com revisão no meio, o item é "item da revisão", não "do limite". Trade-off aceito.

---

## 3. Grafo de Dependências

```
                    ┌─ T0.1 Rename Item+Spec
                    │      │
   Fase 0           │      ▼
   (Foundation)     ├─ T0.2 GarantiaExigidaRevisao entity
                    │      │
                    │      ▼
                    ├─ T0.3 LimiteBanco refatoração (usa Revisão)
                    │      │
                    ├─ T0.4 Contrato 3 campos + VincularPoliticaBanco
                    │      │
                    ├─ T0.5 EF Configurations
                    │      │
                    └─ T0.6 Migration S34 com backfill
                          │
        ─────────── Checkpoint Fase 0 ───────────
                          │
   Fase 1           ┌─ T1.1 Handlers existentes atualizados
   (Versionamento — │      │
    Lacuna 1)       ├─ T1.2 ListarRevisoesGarantias Query/Handler/DTO
                    │      │
                    ├─ T1.3 GET /revisoes-garantias endpoint
                    │      │
                    └─ T1.4 DELETE por itemId → 410 Gone
                          │
        ─────────── Checkpoint Fase 1 ───────────
                          │
   Fase 2           ┌─ T2.1 ConverterEmContratoHandler preenche 3 FKs
   (Rastreabilidade │      │
    — Lacuna 2)     ├─ T2.2 ContratoDto estendido
                    │      │
                    ├─ T2.3 GET /contratos integration tests
                    │      │
                    └─ T2.4 Mcp ContratoTools sincronizado
                          │
        ─────────── Checkpoint Fase 2 ───────────
                          │
   Fase 3           ┌─ T3.1 Enforcement no handler + Exception
   (Enforcement —   │      │
    Lacuna 3)       ├─ T3.2 Middleware mapeia para 409 §4.5
                    │      │
                    └─ T3.3 Tests Application + HTTP cobertura matrix
                          │
        ─────────── Checkpoint Fase 3 ───────────
                          │
   Fase 4           ┌─ T4.1 OpenAPI / Swagger verify
   (Polish)         │      │
                    ├─ T4.2 Changelog S34
                    │      │
                    └─ T4.3 Smoke test queries forenses
                          │
        ─────────── Checkpoint Final ───────────
```

Cada fase entrega uma das 3 lacunas fechada e sistema em estado operacional.

---

## 4. Tarefas Detalhadas

### Fase 0 — Foundation

#### T0.1 — Rename `GarantiaExigidaLimite` → `GarantiaExigidaItem` (+ Spec)

**Descrição:** Refator mecânico, sem mudança de comportamento. Renomeia o tipo e referências. Mantém os métodos da `LimiteBanco` apontando para o novo nome.

**Acceptance criteria:**
- [ ] `Sgcf.Domain/Cotacoes/GarantiaExigidaItem.cs` substitui `GarantiaExigidaLimite.cs`.
- [ ] `Sgcf.Domain/Cotacoes/GarantiaExigidaItemSpec.cs` substitui `GarantiaExigidaLimiteSpec.cs`.
- [ ] Todas as referências em Application, Infrastructure, Api, Tests compilam.
- [ ] `GarantiaExigidaLimiteDto`, `CriarGarantiaExigidaLimiteRequest`, `LimiteBancoConfiguration`, `GarantiaExigidaLimiteConfiguration` renomeados em conformidade.
- [ ] EF migration **NÃO** é gerada nesta task (rename de tabela vem em T0.6).

**Verification:**
- [ ] `dotnet build` sucesso.
- [ ] `dotnet test --filter "Category!=Slow"` verde (zero regressão).
- [ ] `grep -r "GarantiaExigidaLimite" src/ tests/` retorna 0 ocorrências.

**Dependencies:** Nenhuma.

**Files likely touched:** ~14 (renomeados + referências).

**Estimated scope:** M (3-5 conceitos, muitos arquivos mas mecânico).

---

#### T0.2 — Adicionar entidade `GarantiaExigidaRevisao`

**Descrição:** Criar a nova entidade com factory, `EncerrarVigencia`, coleção privada de itens. **Não** integrar ao `LimiteBanco` ainda — coexiste sem uso. Testes SR-01..SR-08 cobrem invariantes.

**Acceptance criteria:**
- [ ] `Sgcf.Domain/Cotacoes/GarantiaExigidaRevisao.cs` implementa SPEC §7.1.
- [ ] Factory `Criar(limiteBancoId, itens, clock, motivo, observacoes)` valida SR-01.
- [ ] `EncerrarVigencia(clock)` valida SR-03 e SR-04.
- [ ] `AdicionarItemInterno` valida SR-06.
- [ ] Coleção `Itens` é `IReadOnlyCollection`.
- [ ] Tests `GarantiaExigidaRevisaoTests.cs` cobrem SR-01 a SR-08 (≥ 8 tests).

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~GarantiaExigidaRevisaoTests"` verde.
- [ ] Cobertura da nova entidade ≥ 95% linhas.

**Dependencies:** T0.1 (usa o tipo renomeado `GarantiaExigidaItem`).

**Files likely touched:** 2 (entity + tests).

**Estimated scope:** M.

---

#### T0.3 — Refatorar `LimiteBanco` para operar com revisões

**Descrição:** Substituir a coleção `_garantiasExigidas` por `_revisoesGarantias`. Métodos públicos `SubstituirGarantiasExigidas`, `AdicionarGarantiaExigida`, `RemoverGarantiaExigidaPorTipo` passam por revisão (SLB-02, SLB-03, SLB-04). Propriedade `GarantiasExigidas` torna-se computed (itens da revisão vigente). Tests existentes do `LimiteBanco` adaptados (mesmas asserções de comportamento, mas agora via revisões).

**Acceptance criteria:**
- [ ] `LimiteBanco._revisoesGarantias: List<GarantiaExigidaRevisao>` substitui `_garantiasExigidas`.
- [ ] `LimiteBanco.GarantiasExigidas` retorna `RevisaoGarantiasVigente?.Itens ?? Array.Empty<...>()`.
- [ ] `LimiteBanco.RevisaoGarantiasVigente` retorna `null` ou a revisão com `VigenciaFim == null`.
- [ ] `SubstituirGarantiasExigidas` fecha vigente + abre nova; idempotente por valor (SLB-04).
- [ ] `AdicionarGarantiaExigida` e `RemoverGarantiaExigidaPorTipo` operam abrindo nova revisão com (itens da anterior ± diff).
- [ ] `RemoverGarantiaExigida(Guid itemId)` é **descontinuado** (método domain removido); tests respectivos removidos.
- [ ] `LimiteBancoRevisoesTests.cs` cobre SLB-01..SLB-05 (≥ 6 tests).
- [ ] Tests antigos de `LimiteBanco` em `GarantiasExigidas` continuam verdes (adaptados).

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~LimiteBanco"` verde.
- [ ] Cobertura ≥ 95% linhas em `LimiteBanco.cs`.

**Dependencies:** T0.2.

**Files likely touched:** `LimiteBanco.cs`, `LimiteBancoTests.cs`, novo `LimiteBancoRevisoesTests.cs` (~3 arquivos).

**Estimated scope:** L. **Atenção:** task com maior risco da Fase 0 — invariante SLB-04 (idempotência por valor) exige equality semântica entre `GarantiaExigidaItem` (com Id) e `GarantiaExigidaItemSpec` (sem Id). Sessão dedicada.

---

#### T0.4 — `Contrato`: 3 campos + `VincularPoliticaBanco`

**Descrição:** Adicionar `LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId` como `Guid?` privados-set. Adicionar método `internal VincularPoliticaBanco(...)` idempotente. `Contrato.Atualizar` permanece inalterado (não toca esses campos).

**Acceptance criteria:**
- [ ] 3 propriedades `Guid?` com getters públicos e setters privados em `Contrato.cs`.
- [ ] Método `internal void VincularPoliticaBanco(Guid?, Guid?, Guid?)` aceita idempotência (mesmos valores) e rejeita valores diferentes com `InvalidOperationException`.
- [ ] `Contrato.Atualizar` não modifica os 3 campos (verificado por test).
- [ ] `ContratoVincularPoliticaBancoTests.cs` cobre SC-05 (≥ 4 tests: primeira vez, idempotência, valores diferentes, parcial).

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~ContratoVincularPoliticaBancoTests"` verde.
- [ ] Zero regressão em tests existentes de `Contrato`.

**Dependencies:** Nenhuma (independente de T0.1..T0.3).

**Files likely touched:** `Contrato.cs`, `ContratoVincularPoliticaBancoTests.cs`.

**Estimated scope:** S.

---

#### T0.5 — EF Configurations atualizadas

**Descrição:** Criar `GarantiaExigidaRevisaoConfiguration.cs`. Renomear `GarantiaExigidaLimiteConfiguration` para `GarantiaExigidaItemConfiguration` e ajustar FK para `RevisaoId`. Atualizar `LimiteBancoConfiguration` para navegação `RevisoesGarantiasExigidas`. Atualizar `ContratoConfiguration` para 3 FKs com `ON DELETE SET NULL`.

**Acceptance criteria:**
- [ ] `GarantiaExigidaRevisaoConfiguration` mapeia tabela `garantia_exigida_revisao` (PK, tenant_id, FK limite_banco_id, vigencias, motivo, observacoes).
- [ ] Índice único parcial `(tenant_id, limite_banco_id) WHERE vigencia_fim IS NULL` declarado via `HasFilter`.
- [ ] `GarantiaExigidaItemConfiguration` aponta `RevisaoId` como FK (Cascade), remove `LimiteBancoId`.
- [ ] `LimiteBancoConfiguration` mapeia `_revisoesGarantias` como navegação privada.
- [ ] `ContratoConfiguration` adiciona 3 FKs nullable com `OnDelete(DeleteBehavior.SetNull)`.
- [ ] RLS habilitada para `garantia_exigida_revisao` no padrão existente (`tenant_id = current_setting('app.tenant_id')::uuid`).

**Verification:**
- [ ] `dotnet build` sucesso.
- [ ] Inspeção visual das configurations vs SPEC §6.2.

**Dependencies:** T0.3, T0.4.

**Files likely touched:** 4 arquivos de configuration.

**Estimated scope:** S.

---

#### T0.6 — Migration `S34_SnapshotGarantiasContrato` com backfill

**Descrição:** Gerar migration EF Core e ajustar manualmente o `Up()` para incluir backfill in-migration conforme SPEC §6.3. Validar contra base com dados representativos via Testcontainers.

**Acceptance criteria:**
- [ ] Migration gerada via `dotnet ef migrations add S34_SnapshotGarantiasContrato`.
- [ ] `Up()` executa na ordem: CREATE TABLE revisao → ADD COLUMN revisao_id (nullable) → INSERT revisões iniciais → UPDATE itens.revisao_id → ALTER COLUMN NOT NULL → DROP limite_banco_id → RENAME tabela → ADD 3 colunas em contrato → criar índices → RLS policy.
- [ ] `Down()` reverte (documentado como destrutivo no header da migration).
- [ ] Teste de integração `MigrationS34BackfillTests` verifica que, partindo de base com 3 limites × 4 itens, todas as 12 linhas têm `revisao_id` populado, sem nulos, com `vigencia_fim IS NULL`.
- [ ] Query `SELECT COUNT(*) FROM garantia_exigida_item WHERE revisao_id IS NULL` retorna 0 pós-migration.

**Verification:**
- [ ] `dotnet ef migrations script PreviousMigration S34_SnapshotGarantiasContrato` produz SQL válido.
- [ ] `dotnet test --filter "FullyQualifiedName~MigrationS34"` verde.
- [ ] Aplicar localmente: `dotnet ef database update`. Inspecionar tabelas via `psql`.

**Dependencies:** T0.5.

**Files likely touched:** 2 (migration + designer + opcionalmente test).

**Estimated scope:** M. **Atenção:** task de maior risco operacional da Fase 0. Validar com dump de produção (anonimizado) em ambiente local antes de propor merge.

---

### Checkpoint Fase 0 — Foundation Pronta

- [ ] `dotnet build` sucesso em todos os projetos.
- [ ] `dotnet test` (suite completo, incluindo Slow) verde.
- [ ] `dotnet ef database update` aplicado em local sem erro.
- [ ] Cobertura: Domain ≥ 95% nas entidades novas/refatoradas.
- [ ] `grep -r "GarantiaExigidaLimite" src/ tests/` = 0 ocorrências.
- [ ] Backfill validation query OK.
- [ ] Revisar com humano antes de prosseguir para Fase 1.

---

### Fase 1 — Versionamento Operacional (Lacuna 1)

#### T1.1 — Ajustar handlers existentes para nova semântica

**Descrição:** `CreateLimiteBancoHandler`, `UpdateLimiteBancoHandler` e handlers de adicionar/remover/substituir garantia. Assinatura externa do command permanece; internamente os handlers passam pela nova API do agregado (que abre revisão). Tests existentes adaptados.

**Acceptance criteria:**
- [ ] `CreateLimiteBancoHandler` cria limite com revisão inicial (se houver itens no command).
- [ ] `UpdateLimiteBancoHandler` PATCH com `garantiasExigidas` chama `SubstituirGarantiasExigidas` (que internamente abre nova revisão).
- [ ] `UpdateLimiteBancoHandler` PATCH sem `garantiasExigidas` (null) preserva revisão vigente.
- [ ] Handler de adicionar garantia individual: chama `AdicionarGarantiaExigida` (abre nova revisão com itens da anterior + novo).
- [ ] Handler de remover garantia: aceita `tipo` (não Id); chama `RemoverGarantiaExigidaPorTipo`.
- [ ] Tests existentes desses handlers verdes após adaptação.
- [ ] Novo test `LimiteBancoPatchAbreRevisaoTests` cobre fluxo end-to-end (PATCH → revisão fechada + nova vigente).

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~LimiteBanco"` verde.
- [ ] `dotnet test --filter "FullyQualifiedName~LimiteBancoPatchAbreRevisao"` verde.

**Dependencies:** Fase 0 completa.

**Files likely touched:** 4-5 handlers + adaptação de tests.

**Estimated scope:** M.

---

#### T1.2 — `ListarRevisoesGarantiasQuery` + Handler + DTO

**Descrição:** Query simples que retorna `IReadOnlyList<GarantiaExigidaRevisaoDto>` ordenado por `VigenciaInicio` ascendente. Inclui itens da revisão (eager load).

**Acceptance criteria:**
- [ ] `ListarRevisoesGarantiasQuery(Guid LimiteBancoId)` define a query.
- [ ] `GarantiaExigidaRevisaoDto.From(entity)` produz DTO completo com `itens[]`.
- [ ] Handler usa `ILimiteBancoRepository` (novo método `GetRevisoesGarantiasAsync(limiteBancoId, ct)`).
- [ ] Ordenação SLB-05 garantida no SQL (`ORDER BY vigencia_inicio ASC`).
- [ ] `ListarRevisoesGarantiasHandlerTests` cobre: limite vazio, limite com 1 revisão, limite com 3 revisões em ordem.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~ListarRevisoesGarantias"` verde.

**Dependencies:** T1.1.

**Files likely touched:** 4-5 (query + handler + DTO + tests + repo).

**Estimated scope:** M.

---

#### T1.3 — Endpoint `GET /limites-banco/{id}/revisoes-garantias`

**Descrição:** Adicionar rota ao `LimitesBancoController`. Role: `Operador`. Retorna JSON conforme SPEC §5.2.

**Acceptance criteria:**
- [ ] Rota `GET /api/v1/limites-banco/{id}/revisoes-garantias` implementada.
- [ ] Retorna `200 OK` com body `{ limiteBancoId, revisoes: [ ... ] }`.
- [ ] `404 Not Found` se `limiteBancoId` não existe.
- [ ] Autorização `Operador` aplicada (testar `401` sem token, `403` com role errado).
- [ ] Multi-tenant: outro tenant retorna `404` (RLS filtra).
- [ ] Integration test `SnapshotGarantiasEndpointsTests.GetRevisoesGarantias_*`.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~SnapshotGarantiasEndpoints"` verde.
- [ ] Swagger expõe o endpoint.

**Dependencies:** T1.2.

**Files likely touched:** `LimitesBancoController.cs`, `SnapshotGarantiasEndpointsTests.cs`.

**Estimated scope:** S.

---

#### T1.4 — Deprecar `DELETE /limites-banco/{id}/garantias-exigidas/{itemId}` → 410 Gone

**Descrição:** Endpoint antigo passa a retornar `410 Gone` com header `Location: /api/v1/limites-banco/{id}/garantias-exigidas?tipo=X`. Novo endpoint `DELETE ?tipo=X` é implementado.

**Acceptance criteria:**
- [ ] `DELETE /limites-banco/{id}/garantias-exigidas/{itemId}` retorna `410 Gone`.
- [ ] Header `Location` aponta para o endpoint novo (incluindo o `tipo` derivado do itemId).
- [ ] Body do 410: problem-details com `type=https://sgcf.io/errors/endpoint-descontinuado`.
- [ ] Novo endpoint `DELETE /limites-banco/{id}/garantias-exigidas?tipo=X` chama `RemoverGarantiaExigidaPorTipo`.
- [ ] `404 Not Found` se tipo não existe na revisão vigente.
- [ ] Integration tests para ambos os endpoints.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~DeleteGarantia"` verde.
- [ ] Manual: hit no endpoint antigo via `curl` retorna 410.

**Dependencies:** T1.1.

**Files likely touched:** `LimitesBancoController.cs`, test.

**Estimated scope:** S.

---

### Checkpoint Fase 1 — Versionamento (Lacuna 1 fechada)

- [ ] PATCH em garantias gera nova revisão (verificado por integration test).
- [ ] `GET /revisoes-garantias` retorna histórico ordenado.
- [ ] Endpoint deprecado responde `410 Gone`.
- [ ] `dotnet test` verde.
- [ ] Smoke test query forense: `SELECT * FROM garantia_exigida_revisao WHERE limite_banco_id = X AND vigencia_inicio <= 'YYYY-MM-DD' AND (vigencia_fim IS NULL OR vigencia_fim > 'YYYY-MM-DD')` retorna a revisão correta.
- [ ] Revisar com humano antes de Fase 2.

---

### Fase 2 — Rastreabilidade (Lacuna 2)

#### T2.1 — `ConverterEmContratoHandler` preenche 3 FKs

**Descrição:** Antes de `Contrato.Criar`, o handler busca o `LimiteBanco` ativo para `(bancoId, modalidade)` na `DataContratacao` e o `LimiteGlobalBanco` vigente. Após criar o contrato, chama `VincularPoliticaBanco(...)` com os 3 ids (qualquer um pode ser null se ausente).

**Acceptance criteria:**
- [ ] Lookup do `LimiteBanco` usa `DataContratacao` como referência temporal (SPEC §11 ponto aberto resolvido como SPEC §11 confirmed default).
- [ ] Lookup do `LimiteGlobalBanco` usa `GetVigenteByBancoAsync`.
- [ ] `VincularPoliticaBanco` chamado uma única vez após `Criar`.
- [ ] Quando não há `LimiteBanco`: contrato criado com `LimiteBancoId = null` e `GarantiasExigidasRevisaoId = null` (SC-07).
- [ ] Quando há `LimiteBanco` sem revisão vigente: `LimiteBancoId` preenchido, `GarantiasExigidasRevisaoId = null` (edge case explícito).
- [ ] Tests `ConverterEmContratoFKsTests` cobrem 4 cenários: full, sem global, sem limite, sem revisão.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~ConverterEmContratoFKsTests"` verde.

**Dependencies:** Fase 1 completa.

**Files likely touched:** `ConverterEmContratoHandler.cs`, test.

**Estimated scope:** M.

---

#### T2.2 — `ContratoDto` estendido

**Descrição:** Adicionar `LimiteBancoId`, `LimiteGlobalBancoId`, `GarantiasExigidasRevisaoId` ao `ContratoDto`. Adicionar `GarantiasExigidasSnapshot: GarantiaExigidaSnapshotItemDto[]?` apenas no detalhe (não na listagem). Criar `GarantiaExigidaSnapshotItemDto` sem `id`/`createdAt`/`updatedAt`.

**Acceptance criteria:**
- [ ] `ContratoDto.From(entity)` aceita um parâmetro opcional `IReadOnlyCollection<GarantiaExigidaItem>? snapshot` para o detalhe.
- [ ] `GetContratoQuery` (detalhe) faz eager-load da revisão vinculada e popula `GarantiasExigidasSnapshot`.
- [ ] `ListContratosQuery` (listagem) **não** faz eager-load — `GarantiasExigidasSnapshot` permanece `null`.
- [ ] Tests para ambos os caminhos.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~GetContrato|FullyQualifiedName~ListContratos"` verde.

**Dependencies:** T2.1.

**Files likely touched:** `ContratoDto.cs`, `GarantiaExigidaSnapshotItemDto.cs`, queries, tests.

**Estimated scope:** M.

---

#### T2.3 — Integration tests `GET /contratos`

**Descrição:** Validar via HTTP que detalhe inclui snapshot e listagem não inclui.

**Acceptance criteria:**
- [ ] `GET /contratos/{id}` de contrato com FKs retorna `garantiasExigidasSnapshot[]` populado.
- [ ] `GET /contratos/{id}` de contrato legado retorna 3 FKs como `null` e `garantiasExigidasSnapshot: null`.
- [ ] `GET /contratos` (listagem) retorna 3 FKs mas `garantiasExigidasSnapshot` ausente (ou `null`).
- [ ] Multi-tenant: snapshot só aparece se o tenant tem acesso.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~GetContrato.*Snapshot|FullyQualifiedName~ListContratos.*Snapshot"` verde.

**Dependencies:** T2.2.

**Files likely touched:** Integration tests.

**Estimated scope:** S.

---

#### T2.4 — Atualizar `Sgcf.Mcp/Tools/ContratoTools`

**Descrição:** O adapter MCP expõe `ContratoDto` para agentes. Garantir que os campos novos apareçam coerentemente no schema exportado.

**Acceptance criteria:**
- [ ] `ContratoTools` reflete novos campos do `ContratoDto`.
- [ ] Schema exportado via MCP inclui `limiteBancoId`, `limiteGlobalBancoId`, `garantiasExigidasRevisaoId`, `garantiasExigidasSnapshot`.
- [ ] Tests `Sgcf.Mcp.Tests` verde.

**Verification:**
- [ ] `dotnet test tests/Sgcf.Mcp.Tests` verde.

**Dependencies:** T2.2.

**Files likely touched:** `ContratoTools.cs`, test.

**Estimated scope:** S.

---

### Checkpoint Fase 2 — Rastreabilidade (Lacuna 2 fechada)

- [ ] Contrato novo carrega 3 FKs e snapshot.
- [ ] Contrato legado retorna 3 FKs como null sem erro.
- [ ] Listagem não carrega payload pesado.
- [ ] MCP exposed schema atualizado.
- [ ] `dotnet test` verde.
- [ ] Revisar com humano antes de Fase 3.

---

### Fase 3 — Enforcement (Lacuna 3)

#### T3.1 — Enforcement no `ConverterEmContratoHandler`

**Descrição:** Após validar inputs e antes de chamar `Contrato.Criar`, comparar `revisao.Itens.Where(Obrigatoria)` contra `garantiasInformadasNoCommand`. Usar `CalculadorValorGarantiaExigida` para o valor esperado. Acumular lacunas (item por item) e, se houver, lançar `GarantiaExigidaNaoCobertaException` com a lista.

**Acceptance criteria:**
- [ ] `GarantiaExigidaNaoCobertaException` definida em `Sgcf.Application/Cotacoes/Exceptions/`.
- [ ] Função privada `AvaliarCobertura(itens, garantias, valorPrincipalBrl)` retorna `List<LacunaGarantia>`.
- [ ] Cada `LacunaGarantia` traz `Tipo`, `ValorEsperadoBrl`, `ValorCobertoBrl`.
- [ ] Tipo `Aval` é avaliado por presença (ignora valor); demais tipos avaliam valor.
- [ ] Cobertura excedente (Σ Garantia.Valor > esperado) **não** bloqueia.
- [ ] Cobertura parcial (Σ Garantia.Valor < esperado) bloqueia.
- [ ] Item `Obrigatoria = false` é ignorado.
- [ ] Sem `LimiteBanco`: enforcement desligado (SC-07).
- [ ] Sem revisão vigente: enforcement desligado.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~ConverterEmContratoEnforcement"` verde.

**Dependencies:** Fase 2 completa.

**Files likely touched:** `ConverterEmContratoHandler.cs`, `GarantiaExigidaNaoCobertaException.cs`, tests.

**Estimated scope:** M.

---

#### T3.2 — Mapear exceção para `409 Conflict` no middleware

**Descrição:** Adicionar entrada no `ExceptionHandlingMiddleware` para `GarantiaExigidaNaoCobertaException` retornar `409` com body conforme SPEC §4.5.

**Acceptance criteria:**
- [ ] Middleware mapeia para `409`.
- [ ] Body inclui `type`, `title`, `status`, `detail`, `limiteBancoId`, `garantiasExigidasRevisaoId`, `lacunas[]`.
- [ ] Cada lacuna inclui `tipo`, `obrigatoria`, `valorEsperadoBrl`, `valorCobertoBrl`.
- [ ] `Aval` com ambos valores nulos é representado como `valorEsperadoBrl: null, valorCobertoBrl: null`.
- [ ] Schema validado por `JsonSchema` em test.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~ExceptionHandlingMiddleware.*GarantiaExigidaNaoCoberta"` verde.

**Dependencies:** T3.1.

**Files likely touched:** `ExceptionHandlingMiddleware.cs`, test.

**Estimated scope:** S.

---

#### T3.3 — Matrix de tests Application + HTTP

**Descrição:** Cobertura matriz para enforcement: tipos × cenários. Para cada tipo (`PercentualSobreLimite`, `ValorFixoBrl`, `Aval`), testar cenários: cobertura completa, cobertura parcial, zero cobertura, cobertura excedente, item não-obrigatório.

**Acceptance criteria:**
- [ ] Tests Application em `ConverterEmContratoEnforcementTests` cobrem ≥ 12 cenários da matriz.
- [ ] Tests HTTP em `ConverterEmContratoEnforcementHttpTests` cobrem ≥ 3 fluxos: bloqueio percentual, bloqueio aval, sucesso com excedente.
- [ ] Cobertura de `AvaliarCobertura` ≥ 95% linhas.
- [ ] Cenário "sem cotação" / "cotação sem garantias informadas" tratado.

**Verification:**
- [ ] `dotnet test --filter "FullyQualifiedName~ConverterEmContratoEnforcement"` verde.

**Dependencies:** T3.1, T3.2.

**Files likely touched:** 2 arquivos de test.

**Estimated scope:** M.

---

### Checkpoint Fase 3 — Enforcement (Lacuna 3 fechada)

- [ ] Conversão bloqueia quando obrigatórias sem cobertura (409 com body §4.5).
- [ ] Conversão sucede quando cobertura completa ou excedente.
- [ ] Bancos sem `LimiteBanco` ou sem revisão convertem normalmente.
- [ ] `dotnet test` verde com matriz cobertura completa.
- [ ] Revisar com humano antes de Fase 4.

---

### Fase 4 — Polish

#### T4.1 — OpenAPI / Swagger verify

**Descrição:** Verificar que documentação OpenAPI exposta inclui:
- `GET /limites-banco/{id}/revisoes-garantias`
- `DELETE /limites-banco/{id}/garantias-exigidas` (com query `tipo`)
- `GET /contratos/{id}` com novos campos
- Campo `garantiasExigidasSnapshot` documentado
- Schema de erro `409` para `garantia-exigida-nao-coberta`

**Acceptance criteria:**
- [ ] `swagger.json` gerado em build inclui todos os itens acima.
- [ ] Verificação manual via Swagger UI local.

**Verification:**
- [ ] `dotnet run --project src/Sgcf.Api` + `curl http://localhost:5000/swagger/v1/swagger.json | jq '.paths | keys'` mostra rotas novas.

**Dependencies:** Fase 3 completa.

**Files likely touched:** Provavelmente nenhum (gerado automaticamente). Eventualmente XML doc nos controllers.

**Estimated scope:** S.

---

#### T4.2 — Changelog `S34`

**Descrição:** Adicionar entrada em `sgcf-backend/docs/changelog/` cobrindo:
- Mudanças quebrantes (`DELETE /garantias-exigidas/{itemId}` → 410).
- Mudanças aditivas (3 FKs no contrato, GET revisoes).
- Migração in-place (sem janela).
- Como auditoria deve consumir.

**Acceptance criteria:**
- [ ] Arquivo `changelog/S34_SnapshotGarantiasContrato.md` criado.
- [ ] Seções: "Breaking", "Aditivo", "Migração", "Como usar para forense".
- [ ] Referência cruzada para SPEC.

**Verification:**
- [ ] Inspeção visual.

**Dependencies:** Fase 3 completa.

**Files likely touched:** 1.

**Estimated scope:** S.

---

#### T4.3 — Smoke tests pós-deploy

**Descrição:** Roteiro de validação manual pós-deploy em produção (ou staging com dados reais):
1. Listar revisões de um `LimiteBanco` que sofreu PATCH recentemente.
2. Criar contrato via cotação faltando garantia obrigatória → confirmar 409 com body correto.
3. Confirmar que contrato legado retorna `LimiteBancoId = null` em `GET /contratos/{id}`.
4. Query forense: `SELECT * FROM garantia_exigida_revisao WHERE limite_banco_id = X AND vigencia_inicio <= 'data' AND (vigencia_fim IS NULL OR vigencia_fim > 'data')`.

**Acceptance criteria:**
- [ ] Roteiro documentado em `tasks/smoke_snapshot_limite_contrato.md`.
- [ ] Cada passo tem expected output.

**Verification:**
- [ ] Executar contra staging após deploy.

**Dependencies:** Fase 3 completa.

**Files likely touched:** 1.

**Estimated scope:** S.

---

### Checkpoint Final — §9.4 da SPEC

- [ ] Migration `S34_SnapshotGarantiasContrato` aplicada em prod (staging primeiro).
- [ ] Backfill validation: `SELECT COUNT(*) FROM garantia_exigida_item WHERE revisao_id IS NULL = 0`.
- [ ] RLS ativa em `garantia_exigida_revisao`.
- [ ] Cobertura nos limites mínimos (Domain ≥95%, Application ≥90%, Infrastructure ≥75%).
- [ ] OpenAPI inclui rotas novas e campos novos.
- [ ] `DELETE /garantias-exigidas/{itemId}` responde 410.
- [ ] Suite completo verde.
- [ ] Invariantes SR-01..SR-08, SLB-01..SLB-05, SC-01..SC-07 cobertas por ≥ 1 test cada.
- [ ] Changelog publicado.
- [ ] Smoke test executado em staging.
- [ ] Frontend Nordware sinalizado para iniciar consumo dos campos novos.

---

## 5. Paralelização

### 5.1 Tarefas que podem rodar em paralelo

- **T0.1** (rename) e **T0.4** (Contrato fields): independentes; podem ir em paralelo em sessões distintas.
- **T1.3** (endpoint GET) e **T1.4** (endpoint DELETE 410): independentes após T1.1; podem paralelizar.
- **T2.3** (HTTP tests) e **T2.4** (Mcp): independentes após T2.2.
- **T4.1**, **T4.2**, **T4.3**: todos independentes.

### 5.2 Tarefas que devem ser sequenciais

- T0.2 → T0.3 → T0.5 → T0.6 (cadeia obrigatória: entidade → uso no agregado → mapping EF → migration).
- T1.1 → T1.2 (handler atualizado antes da query consumir).
- T2.1 → T2.2 → T2.3 (handler preenche FKs → DTO expõe → HTTP testa).
- T3.1 → T3.2 → T3.3.

### 5.3 Coordenação necessária

- Coordenar deploy: migration S34 deve preceder o release do binário novo (mesmo deploy idealmente). Plano de rollback: reverter binário; **não** reverter migration (destrutivo). Comunicar com Ops antes de Phase 4.

---

## 6. Riscos e Mitigações

| Risco | Impacto | Probabilidade | Mitigação |
| --- | --- | --- | --- |
| Backfill da migration falha em produção (dataset maior que o testado) | Alto | Baixa | Testar migration contra dump de produção (anonimizado) em staging antes do merge. Estimativa atual: <5s para 100k linhas. |
| Equality de `GarantiaExigidaItemSpec` vs `GarantiaExigidaItem` quebra SLB-04 | Médio | Média | T0.3: invariante coberta por ≥ 3 tests de idempotência por valor (ordem irrelevante, casas decimais, observações). |
| `CalculadorValorGarantiaExigida` retorna valor diferente da regra atual de cotação | Alto | Baixa | T3.1 reutiliza o mesmo calculador — qualquer divergência é regressão na suite existente (já testada). |
| Frontend quebra com FKs nulas em contratos legados | Médio | Média | Sinalizar para frontend ANTES da migração; revisão obrigatória do tipo `ContratoDto` no Nordware para tornar campos `?: string \| null`. |
| Endpoint deprecated (`DELETE /{itemId}`) ainda usado por scripts internos | Médio | Baixa | T1.4 retorna 410 com `Location` apontando para o substituto. Buscar referências internas (grep) antes do merge. |
| MCP/A2A schemas desatualizados pós-deploy | Baixo | Média | T2.4 atualiza adapter; smoke test pós-deploy verifica schema MCP. |
| Vigência de revisão e vigência do limite divergem em interpretação | Médio | Baixa | SPEC §3.6 explicita: independente. Tests T0.2 e T0.3 cobrem cenários. |

---

## 7. Pontos Abertos (Confirmar Durante o Build)

Identificados na SPEC §11 e mantidos aqui para visibilidade no build:

| #  | Pergunta                                                                              | Default desta SPEC                                                | Quando confirmar |
| -- | ------------------------------------------------------------------------------------- | ----------------------------------------------------------------- | ---------------- |
| 1  | Critério temporal de vigência do `LimiteBanco` na conversão                            | `cmd.DataContratacao` (data negocial)                              | T2.1             |
| 2  | `PATCH` com `garantiasExigidas: null` deve preservar revisão?                          | Sim, preserve (não cria nova revisão)                              | T1.1             |
| 3  | Texto padrão do `Motivo` no backfill da migration                                      | `"Revisão inicial gerada pela migration S34"`                       | T0.6             |
| 4  | `Down()` da migration: implementar reverte parcial ou bloquear?                        | Implementar reverte destrutivo + header documenta perda             | T0.6             |
| 5  | Frontend já pode consumir? Há feature-flag?                                            | Sim, sem flag; campos chegam optional/nullable                      | Pós-Fase 2       |

---

## 8. Refinamentos Remanescentes

Nenhum bloqueante. Decisões fechadas pelo PO em 2026-05-25 (registradas na SPEC §11). Confirmações pontuais marcadas em §7 acima são detalhes de execução, não decisões de produto.
