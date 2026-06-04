# TODO — Regime de Limite Explícito por Banco

> Plano: `tasks/regime-limite-explicito/plan.md` · Spec: `docs/specs/limites-banco/SPEC_REGIME_LIMITE_EXPLICITO.md` (v0.2)
> Marcar `[x]` ao concluir. Não avançar de fase sem o checkpoint verde.

## Fase 0 — Fundação

- [ ] **T1** Domínio: enum `RegimeLimiteBanco` + `Banco.RegimeLimite` + `DefinirRegimeLimite`
  - [ ] `RegimeLimiteBanco.cs` (PerModalidade=0, GlobalPuro=1)
  - [ ] Propriedade `RegimeLimite` (default PerModalidade, setter privado) + método em `Banco.cs`
  - [ ] `BancoRegimeLimiteTests` (default + mutação/UpdatedAt) verdes
- [ ] **T2** Infra: `BancoConfiguration` + migration `S37_RegimeLimiteBanco` + backfill
  - [ ] Mapear `regime_limite integer NOT NULL DEFAULT 0`
  - [ ] Gerar migration; editar `Up` com UPDATE de backfill; `Down` remove coluna
  - [ ] `dotnet ef database update` aplica; coluna confirmada
  - [ ] Relatório dos bancos marcados `GlobalPuro` para conferência do PO
- [ ] **T3** Detecção de regime lê a flag (`ConsultaSaldoBancoService.BancoEmRegimePerModalityAsync`)
  - [ ] Ajustar setup dos testes que dependiam da inferência implícita
- [ ] **[CHECKPOINT A]** `dotnet build` + `dotnet test` verdes; backfill validado; sem mudança de comportamento → **revisar**

## Fase 1 — Coerência e cadastro

- [ ] **T4** REG-01: bloquear `LimiteBanco` em banco `GlobalPuro`
  - [ ] `CreateLimiteBancoCommand` + `UpdateLimiteBancoCommand` injetam `IBancoRepository` e validam regime
  - [ ] `CriarLimiteBanco_EmBancoGlobalPuro_Bloqueia` verde + regressão
- [ ] **T5** Cadastro de regime + REG-02 (Admin-only)
  - [ ] `CreateBancoCommand` aceita `RegimeLimite` opcional (default PerModalidade)
  - [ ] Novo `DefinirRegimeLimiteBancoCommand` + handler (REG-02 / REG-04)
  - [ ] Endpoint Admin em `BancosController`; DTO expõe `regimeLimite`
  - [ ] Testes REG-02 (bloqueia/permite) + autorização (Operador → 403)
- [ ] **[CHECKPOINT B]** Build + suite verdes → **revisar**

## Fase 2 — Enforcement na cotação

- [ ] **T6** `AdicionarBancoNaCotacao` ramificado por regime (§4.3)
  - [ ] Injetar `IBancoRepository`, `ILimiteGlobalBancoRepository`, `IConsultaSaldoBanco`, `ITenantContext`, `IClock`
  - [ ] Ramo `PerModalidade`: `min(disponível modalidade, disponível global)`
  - [ ] Ramo `GlobalPuro`: exige global vigente (REG-03); valida `disponível global ≥ ValorAlvo`
  - [ ] Preservar pré-preenchimento de garantia
  - [ ] `AdicionarBancoRegimeGlobalTests` (4) + regressão
- [ ] **[CHECKPOINT C]** Smoke do cenário Itaú (adição à cotação) → **revisar**

## Fase 3 — Enforcement na conversão (LG-11 + LG-12)

- [ ] **T7** `ConverterEmContrato` ramo `GlobalPuro` (LG-12)
  - [ ] Adicionar `IConsultaSaldoBanco`, `ITenantContext`
  - [ ] `saldoDevedor + principal > global` → bloqueio; sem `RegistrarUso`; mesma transação
  - [ ] `ConverterEmContratoRegimeGlobalTests` (§8.3)
- [ ] **T8** `ConverterEmContrato` ramo `PerModalidade` (LG-11)
  - [ ] Exigir `LimiteBanco` na modalidade; checar disponível modalidade; checar teto global agregado (se houver global)
  - [ ] `RegistrarUso` preservado dentro dos tetos
  - [ ] `ConverterEmContratoLG11Tests` (5, §8.3.1) + regressão (incl. SC-07)
- [ ] **[CHECKPOINT D]** Enforcement completo; suite verde → **revisar**

## Fase 4 — Documentação e fechamento

- [ ] **T9** Docs `docs/api/bancos.md`, `docs/api/limites-banco.md` + nota de emenda em `SPEC_LIMITE_GLOBAL.md §4.3` + OpenAPI
- [ ] **[CHECKPOINT E]** `dotnet test` completo verde + smoke ponta-a-ponta + `/review` → **Go/No-Go merge**

---

### Critérios de aceitação global (da spec §9)
- [ ] Migration S37 aplicada (coluna + backfill) em local e CI
- [ ] Banco `GlobalPuro` com global vigente entra em cotação em qualquer modalidade
- [ ] Conversão `GlobalPuro` respeita teto (LG-12)
- [ ] Conversão `PerModalidade` implementa LG-11 (modalidade + teto global agregado)
- [ ] `GlobalPuro` sem global vigente bloqueado (REG-03) na cotação e na conversão
- [ ] `LimiteBanco` em banco `GlobalPuro` bloqueado (REG-01)
- [ ] Troca para `GlobalPuro` com `LimiteBanco` ativo bloqueada (REG-02)
- [ ] Bancos existentes mantêm 100% do comportamento
- [ ] `regimeLimite` editável apenas por `Admin`
- [ ] docs/api + OpenAPI atualizados
