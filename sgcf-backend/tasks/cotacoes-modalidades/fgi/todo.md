# TODO — Cotações de FGI

Espelho operacional de `tasks/cotacoes-modalidades/fgi/plan.md`.

**Status geral: ENTREGUE — v0.9.0 (2026-05-18)**

O design final diferiu do plano original: `Proposta` não ganhou campos FGI inline.
`FgiInputs` são passados apenas na conversão para contrato (`ConverterEmContratoCommand`),
o que simplifica a modelagem e elimina as fases de persistência intermediária.

---

## Fase 1 — Domínio [ENTREGUE]

- [x] **Task 1.1** Guards de modalidade em `RegistrarPropostaCommand`
    - [x] BRL obrigatório para FGI (EC-10)
    - [x] NDF proibido para FGI (EC-11)
    - [x] Bullet obrigatório no MVP (EC-1, SPEC §1.1)
    - [x] Testes RED → GREEN em `CalculadoraCetFgiTests.cs`

- [x] **Task 1.2** `CalculadoraCet.CalcularCetFgi` implementado
    - [x] Fluxo manual: IOF em t=0, principal+juros+tarifaFgi em t=vencimento
    - [x] TIR Newton-Raphson, base 360 dias
    - [x] Fórmula TarifaFgi idêntica a `GerarCronogramaCommand.AdicionarTarifaFgiAsync`
    - [x] Guards: BRL, sem NDF, Bullet, TaxaFgi > 0
    - [x] Testes verdes (9 casos)

## Fase 2 — Persistência [SEM MUDANÇAS]

- [x] `FgiDetail` e `fgi_detail` já existem desde Onda 0 (`S6_FgiDetail`)
- [x] Nenhuma migration necessária

## Fase 3 — Application + API [ENTREGUE]

- [x] **Task 3.1** `CriarCotacaoCommand` — FGI dispensa PTAX (já suportado via `Modalidade == Fgi` no ramo BRL)
- [x] **Task 3.2** `ConverterEmContratoCommand` ganha `NumeroOperacaoFgi` e `FgiInputs? Fgi`
    - [x] Usa `FgiInputs` do domínio diretamente (sem duplicação)
    - [x] `ConversorFgi` real implementado (extrato de stub)
- [x] **Task 3.3** `ConversorFgi` cria `FgiDetail` via `FgiDetail.Criar`
    - [x] Validação: TaxaFgi > 0, PercentualCoberto em (0,100]
    - [x] Testes `ConversorFgiTests.cs` verdes (12 casos)

## Fase 4 — Golden Dataset [ENTREGUE]

- [x] **Task 4.1** Cenário `fgi-brl-bullet-12m`
    - [x] Principal R$ 500k, 365d, taxa 12%, IOF 0.38%, TaxaFgi 0.5%, PercentualCoberto 80%
    - [x] CET esperado: 12.912788% a.a. (±0.01 p.p.)
    - [x] Invariante: PercentualCoberto não altera CET (SPEC §7.2)
    - [x] Sign-off equipe financeira 2026-05-18
    - [x] `dotnet test tests/Sgcf.GoldenDataset` verde (Cenário 8)

## Fase 5 — Documentação [ENTREGUE]

- [x] **Task 5.1** `docs/specs/cotacoes/modalidades/fgi.md` → Status: Entregue v0.9.0
- [x] **Task 5.2** `docs/api/cotacoes.md` atualizado
    - [x] Modalidades entregues atualizadas no banner
    - [x] Tabela de guards por modalidade em POST /propostas
    - [x] Campos fgi/numeroOperacaoFgi documentados em converter-em-contrato
- [x] **Task 5.3** Bruno collection
    - [x] Request 22: Criar Cotacao FGI
    - [x] Request 23: Converter em Contrato FGI
- [x] **Task 5.4** CHANGELOG v0.9.0 — bloco completo com fórmulas, golden case, cobertura

## Checkpoint Final [VERDE]

- [x] `dotnet test --filter "Category!=Slow"` — 693 testes, 0 falhas
- [x] Build limpo, 0 warnings, 0 erros
- [x] Golden Dataset assinado pelo time financeiro (2026-05-18)
- [x] E2E tests: `FgiFluxoTests.cs` (4 cenários Slow)
- [x] Bruno collection validada com exemplo golden case

## Perguntas em Aberto — Respostas Fixadas

- [x] Q1 — FGI Price/SAC: **restrito a Bullet no MVP** — Price/SAC exigem cronograma (SPEC §1.1)
- [x] Q2 — PercentualCoberto máximo: **100% (invariante de domínio)**, 0% rejeitado
- [ ] Q3 — Capturar `TipoFgi` (subprograma): **backlog pós-MVP**
- [x] Q4 — TaxaFgiAa após aceitação: **imutável** — FgiDetail é criado uma única vez na conversão
- [ ] Q5 — Capturar `BancoIntermediario`: **backlog pós-MVP**
- [x] Q6 — Helper "BRL dispensa PTAX": **reutilizado — mesmo ramo do NCE já existente**
- [x] Q7 — Versão: **v0.9.0** (paralelo com Capital de Giro v0.9.0)
