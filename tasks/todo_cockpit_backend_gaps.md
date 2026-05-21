# Todo — Backend para o Cockpit Multi-Persona

> Plano detalhado: `tasks/plan_cockpit_backend_gaps.md` v1.1
> Marque cada item ao concluir.

## Fase 0 — Transversais (Sprint 1)

- [x] **0.1** ADR-019 + `EnvelopeResponse<T>` + filtro `EnvelopeResultFilter`.
- [x] **0.2** Agregado `Alerta` + enums + repositório + migration EF Core.
- [x] **0.3** `AlertasController` (`GET /alertas`, `/contadores`, `POST dispensar`, `POST marcar-como-lido`).
- [x] **0.4** Rules engine inicial (Vencimento iminente, ContratoSemHedge, LimiteBancoUtilizacao) + migração de alertas legados.
- [x] **0.5** Expandir `StatusCotacao` com `EmAnaliseBanco` e `PropostaRecebida` + máquina de estados + migration + spec.

### Checkpoint A
- [x] CI verde.
- [ ] Endpoints `/alertas/*` validados pelo FE.
- [ ] Envelope `{data, meta}` aprovado por FE em sessão de 30 min.
- [ ] Novos estágios de cotação validados com Gerente Financeiro.

---

## Fase 1 — Cockpit CFO (Sprints 2 a 3)

- [x] **1.1** GAP-CKP-01 — `GET /painel/divida/breakdown-modalidade`.
- [x] **1.2** GAP-CKP-03 — `GET /painel/vencimentos/horizonte?meses=&granularidade=`.
- [x] **1.3** GAP-CKP-04 — `DadosContabeisMensal` + `POST /painel/dados-contabeis` + `GET /painel/estrutura-capital`.

### Checkpoint B
- [ ] Demo CFO + PO com cockpit FE consumindo os três endpoints.
- [ ] Go/No-Go para Fase 2.

---

## Fase 2 — Cockpit Gerente Financeiro (Sprint 4)

- [x] **2.1** GAP-CKP-07 — `GET /painel/inadimplencia`.
- [x] **2.2** Guia FE para Não-Gaps (`12_BACKEND_API_COCKPIT_FE_GUIDE.md`) cobrindo GAP-05, GAP-06, GAP-19, GAP-20, GAP-14-CDI.

---

## Fase 3 — Cockpit Tesouraria intraday (Sprints 5 a 7)

- [x] **3.1** Domínio `ContaBancaria` + CRUD `GET/POST/PUT/DELETE /contas-bancarias`.
- [x] **3.2** GAP-CKP-08 — `SaldoCaixa` com edição por data + `POST /tesouraria/saldos` (upsert idempotente) + `GET /tesouraria/saldos` (série histórica) + `GET /tesouraria/posicao-caixa?dataReferencia=`.
- [x] **3.3** GAP-CKP-09 — `GET /tesouraria/fluxo-caixa?granularidade=dia` + `POST /tesouraria/eventos-fluxo`.
- [x] **3.4** GAP-CKP-10 — `GET /tesouraria/hedge-efetividade`.

### Checkpoint C
- [ ] Demo com gerente de tesouraria (15 min) operando o cockpit.
- [ ] Go/No-Go para Fase 4.

---

## Fase 4 — P1 progressivo (Sprints 8+)

- [ ] **4.1** GAP-CKP-11 — Resultado histórico de hedge.
- [ ] **4.2** GAP-CKP-12 — Sensibilidade a indexadores (consolidação server-side).
- [ ] **4.3** GAP-CKP-13 — Covenants (domínio + endpoints + monitoramento).
- [ ] **4.4** GAP-CKP-14 SOFR/Selic — saving vs benchmark além de CDI.
- [ ] **4.5** GAP-CKP-15 — Orçamento de encargos financeiros.
- [ ] **4.6** GAP-CKP-16 — Workflow de documentação.
- [ ] **4.7** GAP-CKP-17 — Conformidade regulatória (DEF/RDE-ROF, SISCOSERV).
- [x] **4.8** GAP-CKP-18 — Tarifas e IOF agregados.
- [ ] **4.9** GAP-CKP-24 — Preferências de usuário backend.
- [ ] **4.10** GAP-CKP-19 — Adicionar `TaxaIndicativaAa` a `Cotacao`/`Proposta` + spread agregado.

---

## Fase 5 — Backlog (P2)

- [ ] **5.1** GAP-CKP-21 — Economia tributária acumulada.
- [ ] **5.2** GAP-CKP-22 — Produtividade da equipe.
- [ ] **5.3** GAP-CKP-23 — WebSocket/SSE de eventos em tempo real.
- [ ] **5.4** GAP-CKP-25 — Exportação consolidada assíncrona.

---

## Decisões do sponsor — 2026-05-20

- [x] Expandir enum `StatusCotacao` com `EmAnaliseBanco` e `PropostaRecebida` (Task 0.5).
- [x] `ContaBancaria`: input manual com edição por data (Task 3.2).
- [x] Janela de deprecação do `alertas: string[]`: 2 sprints.
- [x] `TaxaIndicativaAa` em `Proposta`: confirmado ausente; vira Task 4.10.
- [x] Preferências de usuário: `localStorage` no MVP; sync server-side em Task 4.9.

## Refinamentos antes de cada fase

- [ ] Granularidade da edição retroativa de `SaldoCaixa` (D-30/D-90/sem limite) — confirmar antes da Task 3.2.
- [ ] Critério para `EmCaptacao → EmAnaliseBanco` (manual ou timeout automático) — confirmar antes da Task 0.5.
