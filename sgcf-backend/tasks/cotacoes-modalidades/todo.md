# Todo — Cotações além de FINIMP (consolidado)

Lista de tarefas em ordem de execução recomendada. Cada item linka ao plano detalhado.
Plano completo em `plan.md` (mesmo diretório).

---

## Bloqueio externo

- [ ] **Aprovação humana** das decisões cross-modalidade MD-1..MD-10 (plan.md §2)
- [ ] **Decisão** das 6 perguntas em aberto (plan.md §6)
- [ ] **Escolha** da estratégia de versionamento da SPEC (plan.md §6 Q6)

---

## Onda 0 — Foundation (pré-requisito de Ondas 2–4)

- [ ] **F0.1** Tornar `Cotacao.PtaxUsadaUsdBrl` opcional (`decimal?`)
  - [ ] Migration S6_PtaxNullable
  - [ ] `CriarCotacaoCommand`: PTAX obrigatório só para FINIMP/REFINIMP/Lei4131
  - [ ] `ConverterEmContratoCommand`: tratar null em `valorPrincipalBrl`
  - [ ] `EconomiaNegociacao` snapshot: PTAX null → JSON null
  - [ ] Suite FINIMP regression verde

- [ ] **F0.2** `CalculadoraCet` ganha método dedicado por modalidade
  - [ ] Método `CalcularCetFinimp` (extrair lógica atual)
  - [ ] Stubs para Refinimp/Lei4131/Nce/CapitalDeGiro/Fgi (`NotImplementedException`)
  - [ ] Fachada `CalcularCet(proposta, ...)` dispatcheia por modalidade
  - [ ] Golden dataset FINIMP existente sem ajuste

- [ ] **F0.3** `ConverterEmContratoCommand` como dispatcher
  - [ ] Interface `IConversorModalidade` (ou strategy registrado em DI)
  - [ ] `ConversorFinimp` extrai a lógica existente
  - [ ] Conversores Refinimp/Lei4131/Nce/CapitalDeGiro/Fgi como `NotImplementedException`
  - [ ] Reduzir `ConverterEmContratoCommand` em ~30 linhas

- [ ] **Checkpoint F0** — Suite completa verde + migration aplicada + revisão humana

---

## Onda 1 — REFINIMP (paralelo com Onda 0)

Plano detalhado: [`refinimp/plan.md`](./refinimp/plan.md) · Todo: [`refinimp/todo.md`](./refinimp/todo.md)

- [ ] Fase 1 (Domínio) — `Cotacao.ContratoMaeId` opcional, validações
- [ ] Fase 2 (Persistência) — Migration S7_RefinimpContratoMae
- [ ] Fase 3 (Application + API) — `CriarCotacaoCommand` aceita ContratoMaeId, `RegistrarPropostaCommand` valida moeda do mãe, `ConverterEmContratoCommand` cria `RefinimpDetail`
- [ ] Fase 4 (Regra 70% BB) — Pré-cálculo informativo no comparativo + validação na conversão
- [ ] Fase 5 (Integração) — `LimiteBanco.Refinimp`, Golden case REFINIMP, Bruno requests
- [ ] Fase 6 (Doc) — SPEC, `docs/api/cotacoes.md`, CHANGELOG v0.7.0

**Release alvo:** v0.7.0

---

## Onda 2 — NCE (depois de Checkpoint F0)

Plano detalhado: [`nce/plan.md`](./nce/plan.md) · Todo: [`nce/todo.md`](./nce/todo.md)

- [ ] Fase 1 (Domínio) — Validações NCE (moeda=BRL, NDF=false)
- [ ] Fase 2 (Persistência) — Sem migration nova (entidade NceDetail já existe)
- [ ] Fase 3 (Application + API) — `CriarCotacao` aceita NCE sem PTAX, `RegistrarProposta` valida BRL, `ConverterEmContrato` cria `NceDetail`
- [ ] Fase 4 (CalculadoraCet branch NCE) — Sem IRRF, sem IOF câmbio, com IOF crédito, periodicidade configurável
- [ ] Fase 5 (Doc) — SPEC, `docs/api/cotacoes.md`, Bruno, Golden case NCE BRL, CHANGELOG v0.8.0

**Release alvo:** v0.8.0

---

## Onda 3a — FGI (paralelo com Onda 3b)

Plano detalhado: [`fgi/plan.md`](./fgi/plan.md) · Todo: [`fgi/todo.md`](./fgi/todo.md)

- [ ] Fase 1 (Domínio) — Disambiguação FGI-modalidade vs FGI-garantia
- [ ] Fase 2 (Persistência) — Sem migration (FgiDetail existe)
- [ ] Fase 3 (Application + API) — `CriarCotacao` aceita FGI sem PTAX, `RegistrarProposta` valida BRL, `ConverterEmContrato` cria `FgiDetail`, `CalculadoraCet` branch FGI com tarifa anual
- [ ] Fase 4 (Integração) — Eventos `TarifaFgi` no cronograma alinhados com `GerarCronograma`
- [ ] Fase 5 (Doc) — SPEC glossário, `docs/api/cotacoes.md`, Bruno, Golden case FGI, CHANGELOG v0.9.0

**Release alvo:** v0.9.0

---

## Onda 3b — Capital de Giro (paralelo com Onda 3a)

Plano detalhado: [`capital-de-giro/plan.md`](./capital-de-giro/plan.md) · Todo: [`capital-de-giro/todo.md`](./capital-de-giro/todo.md)

- [ ] Fase 1 (Domínio) — `CapitalDeGiroDetail.TemFgi` validation, tipo de produto
- [ ] Fase 2 (Persistência) — Migration S6b_PropostaCamposCapitalDeGiro
- [ ] Fase 3 (Application + API) — `CriarCotacao` aceita CapitalDeGiro sem PTAX, `RegistrarProposta` captura premissas, `ConverterEmContrato` cria `CapitalDeGiroDetail` + opcional `FgiDetail` quando `TemFgi=true`
- [ ] Fase 4 (Cronograma externo) — Hand-off para `ImportarCronogramaCommand` existente (sem mudança)
- [ ] Fase 5 (Doc) — SPEC, `docs/api/cotacoes.md`, Bruno, Golden case PROGER, CHANGELOG v0.9.0

**Release alvo:** v0.9.0 (junto com FGI)

**Atenção:** Bloqueio externo (Q1 do plano capital-de-giro/) aguarda alinhamento com Onda 3a para evitar duplicação na criação de `FgiDetail`.

---

## Onda 4 — Lei 4131 (depois de Onda 3 ou paralelo se equipe permite)

Plano detalhado: [`lei4131/plan.md`](./lei4131/plan.md) · Todo: [`lei4131/todo.md`](./lei4131/todo.md)

- [ ] Fase 1 (Domínio) — Validações Lei 4131 (USD, sem regra 70% BB)
- [ ] Fase 2 (Persistência) — Sem migration (Lei4131Detail existe)
- [ ] Fase 3 (Application + API) — `CriarCotacao` aceita Lei4131, `RegistrarProposta` captura SBLC/MarketFlex, `ConverterEmContrato` cria `Lei4131Detail`
- [ ] Fase 4 (CET + IRRF) — Decisão Q1 (IRRF entra ou não no CET); fórmula `CalcularCetLei4131`
- [ ] Fase 5 (Tributação + SBLC) — Custo SBLC informativo no comparativo
- [ ] Fase 6 (Doc) — SPEC, `docs/api/cotacoes.md`, Bruno, Golden case Lei4131 com SBLC, CHANGELOG v1.0.0

**Release alvo:** v1.0.0

---

## Checkpoints Globais

- [ ] **Checkpoint G1 — Pós-Onda 0** — Foundation entregue, FINIMP regression verde, ADs revisadas
- [ ] **Checkpoint G2 — Pós-Onda 2 (NCE)** — Primeira modalidade BRL completa, branch BRL validado
- [ ] **Checkpoint G3 — Final** — Todas modalidades verdes, 5 golden cases novos, docs completas, release v1.0.0

---

## Resumo

- **6 ondas** (Onda 0 + Ondas 1–4 + Final)
- **~70 tasks** distribuídas
- **26 checkpoints internos** + **3 globais**
- **4 releases sugeridos:** v0.7.0 → v0.8.0 → v0.9.0 → v1.0.0
- **Pré-requisito crítico:** Onda 0 antes das Ondas 2, 3 e 4
