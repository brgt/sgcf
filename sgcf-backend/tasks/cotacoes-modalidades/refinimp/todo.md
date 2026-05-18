# Todo — Cotações de REFINIMP

Lista de tarefas em ordem de execução. Marque cada item conforme concluir.
Plano completo em `plan.md` (mesmo diretório).

---

## Pré-requisitos

- [ ] Módulo Cotações FINIMP estável (v0.5.0) — confirmado
- [ ] Módulo Contratos REFINIMP estável (`ProcessarRefinimpAsync`, regra 70% BB, `MarcarRefinanciado*`) — confirmado
- [ ] Resposta às 7 perguntas em aberto do `plan.md` §6

---

## Fase 1 — Domínio

- [ ] **Task 1.1** Adicionar `ContratoMaeId` ao agregado `Cotacao`
  - [ ] Propriedade `Guid? ContratoMaeId { get; private set; }`
  - [ ] `Cotacao.Criar` recebe parâmetro opcional `contratoMaeId`
  - [ ] Invariante: Refinimp obriga mãe; não-Refinimp proíbe mãe
  - [ ] Testes em `CotacaoRefinimpTests.cs` (ok, falhas, regressão FINIMP)

- [ ] **Checkpoint A** — Build verde + testes domínio passam + revisão humana

---

## Fase 2 — Persistência

- [ ] **Task 2.1** Migration `S6_CotacaoContratoMae`
  - [ ] Coluna `contrato_mae_id uuid NULL` aditiva
  - [ ] FK `contrato(id) ON DELETE RESTRICT`
  - [ ] Index não-único em `contrato_mae_id`
  - [ ] `dotnet ef migrations add` limpo
  - [ ] `dotnet ef database update` aplica sem afetar dados FINIMP
  - [ ] `dotnet ef migrations remove` reverte

- [ ] **Task 2.2** EF Configuration de `Cotacao.ContratoMaeId`
  - [ ] `Property(c => c.ContratoMaeId)...IsRequired(false)`
  - [ ] FK `HasOne<Contrato>().WithMany().OnDelete(Restrict)`
  - [ ] Teste de integração `CotacaoRepositoryRefinimpTests` (round-trip + DELETE proibido)

- [ ] **Checkpoint B** — Migration aplica/reverte, round-trip ok, FK restritiva validada

---

## Fase 3 — Application: criação e captação

- [ ] **Task 3.1** `CriarCotacaoCommand` aceita `contratoMaeId`
  - [ ] Record + validator (When Refinimp → NotNull; When não-Refinimp → Null)
  - [ ] Handler valida existência e status do mãe (rejeita Cancelado/Quitado)
  - [ ] Testes unitários e E2E

- [ ] **Task 3.2** `AdicionarBancoNaCotacaoCommand` valida `AceitaRefinimp`
  - [ ] Validação fail-fast quando cotação é Refinimp e Banco.AceitaRefinimp=false
  - [ ] Reusa `GetByBancoModalidadeAsync(Refinimp)` para limite (linha própria — AD-3)
  - [ ] Teste E2E happy + erro 409

- [ ] **Task 3.3** `RegistrarPropostaCommand` valida moeda vs contrato mãe
  - [ ] Quando Refinimp, busca mãe e exige `proposta.MoedaOriginal == mae.Moeda`
  - [ ] Rejeita com 409 e mensagem específica
  - [ ] Regressão FINIMP

- [ ] **Checkpoint C** — Fluxo de captação Refinimp funcional + zero regressão FINIMP

---

## Fase 4 — Application: conversão em contrato

- [ ] **Task 4.1** Estender `ConverterEmContratoCommand` para REFINIMP
  - [ ] Branch `if (cotacao.Modalidade == Refinimp)` após criar `Contrato`
  - [ ] Carrega `contratoPai` e `ancestral` via `IContratoRepository`
  - [ ] Aplica regra 70% BB (idêntica a `ProcessarRefinimpAsync`)
  - [ ] Calcula `percentualFracao` e cria `RefinimpDetail`
  - [ ] `AddRefinimpDetail`
  - [ ] Marca mãe `RefinanciadoTotal` (>=1.0) ou `RefinanciadoParcial`
  - [ ] `ContratoDto` retorna `RefinimpDetail` populado
  - [ ] Teste: refi 100% → mãe Total; refi 50% → mãe Parcial
  - [ ] Teste: BB + valor > 70% ancestral → 409
  - [ ] Regressão: cotação FINIMP continua convertendo

- [ ] **Task 4.2** Auditoria + snapshot incluem vínculo REFINIMP
  - [ ] `snapshotContratoJson` ganha 3 campos refi
  - [ ] `audit_log` registra `contrato_mae_id` no payload
  - [ ] Teste E2E lê `audit_log`

- [ ] **Checkpoint D** — Conversão REFINIMP completa + atomicidade + auditoria

---

## Fase 5 — API + Bruno + Golden

- [ ] **Task 5.1** Atualizar request DTO de `POST /api/v1/cotacoes`
  - [ ] Campo `contratoMaeId: Guid?` no controller
  - [ ] OpenAPI mostra como opcional com descrição

- [ ] **Task 5.2** Bruno collection: novo cenário REFINIMP
  - [ ] Pasta `Cotacoes-Refinimp/` com setup + fluxo completo
  - [ ] Variável `contratoMaeId` reaproveitada via env
  - [ ] Inclui caso 409 (banco sem AceitaRefinimp)

- [ ] **Task 5.3** Golden Dataset cenário REFINIMP
  - [ ] `cotacoes-refinimp-001.json` (mãe FINIMP USD 1M, refi 50%, BB)
  - [ ] Inclui CET, snapshot, economia
  - [ ] Aprovação humana do CET esperado

- [ ] **Checkpoint E** — Verificação fim-a-fim manual + automática

---

## Fase 6 — Documentação

- [ ] **Task 6.1** Atualizar `docs/specs/cotacoes/SPEC.md`
  - [ ] Remover REFINIMP de §11 (boundaries)
  - [ ] Nova seção "Modalidade REFINIMP" (§14 ou §15)
  - [ ] Atualizar §1 e §3 invariantes

- [ ] **Task 6.2** Atualizar `docs/api/cotacoes.md`
  - [ ] Payload com `contratoMaeId`
  - [ ] Exemplo de proposta Refinimp
  - [ ] Tabela de erros 409 nova

- [ ] **Task 6.3** CHANGELOG v0.7.0
  - [ ] `ADDITIVE — Cotações — Modalidade REFINIMP`
  - [ ] `INTERNAL — Migration S6`
  - [ ] Nota de compatibilidade FINIMP

- [ ] **Checkpoint Final** — Build + testes verde, docs revisadas, Bruno ok, PR pronto

---

## Perguntas pendentes (responder antes de iniciar)

- [ ] Q1: Prazo da proposta Refinimp deve respeitar prazo restante do mãe?
- [ ] Q2: Sublimite Refinimp do BB deve restringir LimiteBanco.Refinimp ao % do Finimp?
- [ ] Q3: Reconciliação automática do `LimiteBanco.Finimp` quando mãe vira RefinanciadoTotal?
- [ ] Q4: Cross-currency (refi BRL de mãe USD) será suportado?
- [ ] Q5: Profundidade máxima de cadeias REFINIMP-de-REFINIMP?
- [ ] Q6: `RefinanciadoTotal` dispara `Quitado` no mãe?
- [ ] Q7: `ListCotacoesQuery` ganha filtro por `contratoMaeId`?
