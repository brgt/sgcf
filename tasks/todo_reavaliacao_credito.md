# TODO — Reavaliação de Crédito (Ciclo de Vida de Limites)

> Spec: `docs/specs/limites-banco/SPEC_reavaliacao_credito.md` v1.1
> Plano: `tasks/plan_reavaliacao_credito.md`

---

## Fase 1 — Fundação

- [ ] **T1** — Domain + Migration: campo `MotivoEncerramento` no `LimiteBanco`
  - [ ] `LimiteBanco.cs` — add `MotivoEncerramento` property + param no `Atualizar()`
  - [ ] `LimiteBancoConfiguration.cs` — mapear coluna `motivo_encerramento TEXT NULL`
  - [ ] `LimiteBancoDto.cs` — expor `MotivoEncerramento`
  - [ ] `dotnet ef migrations add AddMotivoEncerramentoLimiteBanco`
  - [ ] Verificar: `dotnet build` + `dotnet test --filter "Category!=Slow"` verde

---

## Fase 2 — RV-01: PATCH com vigência

- [ ] **T2** — Application: `UpdateLimiteBancoCommand` estendido + `AtualizarLimiteBancoResponse`
  - [ ] Adicionar `NovaDataVigenciaFim`, `NovaDataVigenciaInicio`, `MotivoEncerramento` ao command
  - [ ] Regras de validação (datas, sobreposição, ajuste de início sem utilização)
  - [ ] Handler: overlap check, `limite.Atualizar()` com novos campos, aviso quando `ValorUtilizado > 0`
  - [ ] Criar `AtualizarLimiteBancoResponse.cs`
  - [ ] Verificar: `dotnet build` verde

- [ ] **T3** — API: controller PATCH + docs
  - [ ] `LimitesBancoController.cs` — aceitar novos campos, retornar tipo correto
  - [ ] `docs/api/limites-banco.md` — atualizar seção PATCH
  - [ ] Verificar: `dotnet build` verde

- [ ] **Checkpoint 1** — `dotnet test --filter "Category!=Slow"` verde; smoke manual do PATCH

- [ ] **T4** — Testes RV-01
  - [ ] `AtualizarLimiteVigenciaTests.cs` (Application.Tests — unitários)
  - [ ] `LimitesBancoVigenciaTests.cs` (Api.IntegrationTests — Slow)
  - [ ] Verificar: `dotnet test` verde (todos)

---

## Fase 3 — RV-02: substituição atômica

- [ ] **T5** — Application: `SubstituirLimiteBancoCommand`
  - [ ] Criar `SubstituirLimiteBancoCommand.cs` (command + validator + handler)
  - [ ] Handler: atualizar anterior, verificar sobreposição, criar sucessor sem antecipação, uma transação
  - [ ] Verificar: `dotnet build` verde

- [ ] **T6** — API: controller `POST /{id}/substituir` + docs
  - [ ] `LimitesBancoController.cs` — nova action
  - [ ] `docs/api/limites-banco.md` — adicionar seção `/substituir`
  - [ ] Verificar: `dotnet build` verde

- [ ] **Checkpoint 2** — `dotnet test --filter "Category!=Slow"` verde; smoke manual do fluxo de substituição

- [ ] **T7** — Testes RV-02
  - [ ] `SubstituirLimiteBancoTests.cs` (Application.Tests — unitários)
  - [ ] `SubstituirLimiteBancoApiTests.cs` (Api.IntegrationTests — Slow)
  - [ ] Verificar: `dotnet test` verde (todos)

---

## Checkpoint Final

- [ ] `dotnet test` — suite completa verde
- [ ] `dotnet build` — zero erros/warnings
- [ ] Todos os critérios de aceitação do spec v1.1 verificados
- [ ] Nenhum teste existente regrediu
