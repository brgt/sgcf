# TODO — Reavaliação de Crédito (Ciclo de Vida de Limites)

> Spec: `docs/specs/limites-banco/SPEC_reavaliacao_credito.md` v1.1
> Plano: `tasks/plan_reavaliacao_credito.md`

---

## Fase 1 — Fundação

- [x] **T1** — Domain + Migration: campo `MotivoEncerramento` no `LimiteBanco`
  - [x] `LimiteBanco.cs` — add `MotivoEncerramento` property + param no `Atualizar()`
  - [x] `LimiteBancoConfiguration.cs` — mapear coluna `motivo_encerramento TEXT NULL`
  - [x] `LimiteBancoDto.cs` — expor `MotivoEncerramento`
  - [x] `dotnet ef migrations add AddMotivoEncerramentoLimiteBanco`
  - [x] Verificar: `dotnet build` + `dotnet test --filter "Category!=Slow"` verde

---

## Fase 2 — RV-01: PATCH com vigência

- [x] **T2** — Application: `UpdateLimiteBancoCommand` estendido + `AtualizarLimiteBancoResponse`
  - [x] Adicionar `NovaDataVigenciaFim`, `NovaDataVigenciaInicio`, `MotivoEncerramento` ao command
  - [x] Regras de validação (datas, sobreposição, ajuste de início sem utilização)
  - [x] Handler: overlap check, `limite.Atualizar()` com novos campos, aviso quando `ValorUtilizado > 0`
  - [x] Criar `AtualizarLimiteBancoResponse.cs`
  - [x] Verificar: `dotnet build` verde

- [x] **T3** — API: controller PATCH + docs
  - [x] `LimitesBancoController.cs` — aceitar novos campos, retornar tipo correto
  - [x] `docs/api/limites-banco.md` — atualizar seção PATCH
  - [x] Verificar: `dotnet build` verde

- [x] **Checkpoint 1** — `dotnet test --filter "Category!=Slow"` verde; smoke manual do PATCH

- [x] **T4** — Testes RV-01
  - [x] `AtualizarLimiteVigenciaTests.cs` (Application.Tests — unitários)
  - [x] `LimitesBancoVigenciaTests.cs` (Api.IntegrationTests — Slow)
  - [x] Verificar: `dotnet test` verde (todos)

---

## Fase 3 — RV-02: substituição atômica

- [x] **T5** — Application: `SubstituirLimiteBancoCommand`
  - [x] Criar `SubstituirLimiteBancoCommand.cs` (command + validator + handler)
  - [x] Handler: atualizar anterior, verificar sobreposição, criar sucessor sem antecipação, uma transação
  - [x] Verificar: `dotnet build` verde

- [x] **T6** — API: controller `POST /{id}/substituir` + docs
  - [x] `LimitesBancoController.cs` — nova action
  - [x] `docs/api/limites-banco.md` — adicionar seção `/substituir`
  - [x] Verificar: `dotnet build` verde

- [x] **Checkpoint 2** — `dotnet test --filter "Category!=Slow"` verde; smoke manual do fluxo de substituição

- [x] **T7** — Testes RV-02
  - [x] `SubstituirLimiteBancoTests.cs` (Application.Tests — unitários)
  - [x] `SubstituirLimiteBancoApiTests.cs` (Api.IntegrationTests — Slow)
  - [x] Verificar: `dotnet test` verde (todos)

---

## Checkpoint Final

- [x] `dotnet test` — suite completa verde
- [x] `dotnet build` — zero erros/warnings
- [x] Todos os critérios de aceitação do spec v1.1 verificados
- [x] Nenhum teste existente regrediu
