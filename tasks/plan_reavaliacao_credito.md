# Plano de Implementação: Reavaliação de Crédito — Ciclo de Vida de Limites

> **Spec:** `sgcf-backend/docs/specs/limites-banco/SPEC_reavaliacao_credito.md` (v1.1, aprovada)
> **Data:** 2026-05-28

---

## Visão geral

Duas capacidades novas:
- **RV-01** — Expor `novaDataVigenciaFim`, `novaDataVigenciaInicio` e `motivoEncerramento` no `PATCH /limites-banco/{id}` existente.
- **RV-02** — Novo endpoint atômico `POST /limites-banco/{id}/substituir` que encerra o limite atual e cria o sucessor em uma transação.

O domínio (`LimiteBanco.Atualizar`) já suporta os parâmetros de data. O único delta no domínio é o campo `MotivoEncerramento`, que exige nova coluna e migration.

---

## Decisões de arquitetura

- `MotivoEncerramento` é um campo de auditoria no próprio `LimiteBanco` (coluna `motivo_encerramento TEXT NULL`). Usar `Observacoes` seria ambíguo — o campo já existe para notas gerais sem semântica de encerramento.
- O PATCH retorna `AtualizarLimiteBancoResponse { Limite, Avisos }` **apenas quando `NovaDataVigenciaFim` é informado** no request. Quando não é informado, continua retornando `LimiteBancoDto` diretamente (compatibilidade com clientes atuais).
- `SubstituirLimiteBancoCommand` usa `GetByIdTrackingAsync` + `LimiteBanco.Atualizar()` + `LimiteBanco.Criar()` + `repo.Add()` em uma única transação (um `SaveChangesAsync`). Não cria um método de domínio novo — o domínio já tem os primitivos necessários.
- O sucessor **não herda** configurações de antecipação do limite encerrado.
- A verificação de sobreposição ao fechar via PATCH usa `FindOverlappingAsync(excludeId: limite.Id)` — o parâmetro já existe na interface.

---

## Grafo de dependências

```
T1 — Domain + Migration
 ├── T2 — Application: UpdateLimiteBancoCommand estendido
 │     └── T3 — API: PATCH controller + docs PATCH
 │           └── T4 — Testes RV-01
 │
 └── T5 — Application: SubstituirLimiteBancoCommand
       └── T6 — API: POST /substituir controller + docs substituir
             └── T7 — Testes RV-02
```

T2 e T5 são independentes entre si (paralelizáveis após T1 estar concluído).

---

## Fase 1 — Fundação de domínio

### T1 — Domain + Infrastructure: campo `MotivoEncerramento` e migration

**O que faz:** Adiciona `MotivoEncerramento` ao agregado `LimiteBanco`, atualiza o método `Atualizar()`, configura o mapeamento EF Core e gera a migration.

**Acceptance criteria:**
- `LimiteBanco` possui propriedade `public string? MotivoEncerramento { get; private set; }`.
- `Atualizar()` aceita parâmetro `string? motivoEncerramento = null`; quando não-null, atualiza a propriedade.
- `LimiteBancoConfiguration` mapeia `motivo_encerramento TEXT NULL`.
- Migration criada com `ALTER TABLE sgcf.limite_banco ADD COLUMN motivo_encerramento text NULL`.
- `LimiteBancoDto` expõe `MotivoEncerramento: string?`.

**Verificação:**
- `dotnet build` sem erros.
- `dotnet ef migrations list` mostra a nova migration.
- `dotnet test --filter "Category!=Slow"` verde (nenhum teste quebra).

**Dependências:** Nenhuma.

**Arquivos:**
- `src/Sgcf.Domain/Cotacoes/LimiteBanco.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/LimiteBancoConfiguration.cs`
- `src/Sgcf.Application/Cotacoes/LimiteBancoDto.cs`
- `src/Sgcf.Infrastructure/Migrations/<timestamp>_AddMotivoEncerramentoLimiteBanco.cs` (gerado via `dotnet ef`)

**Tamanho:** M (4 arquivos)

---

## Fase 2 — RV-01: PATCH com vigência

### T2 — Application: estender `UpdateLimiteBancoCommand`

**O que faz:** Adiciona os três novos campos ao command, as regras de validação correspondentes, e atualiza o handler para (a) verificar sobreposição quando `NovaDataVigenciaFim` é informado, (b) passar os campos ao domínio, e (c) retornar `AtualizarLimiteBancoResponse` com aviso quando o limite encerrado tiver `ValorUtilizadoBrl > 0`. Cria o DTO `AtualizarLimiteBancoResponse`.

**Acceptance criteria:**
- `UpdateLimiteBancoCommand` possui: `NovaDataVigenciaFim: DateOnly?`, `NovaDataVigenciaInicio: DateOnly?`, `MotivoEncerramento: string?`.
- Validator rejeita `NovaDataVigenciaFim <= DataVigenciaInicio` com mensagem clara.
- Validator rejeita `NovaDataVigenciaInicio` quando `ValorUtilizadoBrl > 0`.
- Handler chama `FindOverlappingAsync(excludeId: limite.Id)` quando `NovaDataVigenciaFim` informado; lança `InvalidOperationException` em caso de sobreposição.
- Handler passa `NovaDataVigenciaFim`, `NovaDataVigenciaInicio` e `MotivoEncerramento` ao `limite.Atualizar()`.
- `AtualizarLimiteBancoResponse` contém `LimiteBancoDto Limite` e `IReadOnlyList<string> Avisos`.
- Quando `NovaDataVigenciaFim` informado e `ValorUtilizadoBrl > 0`, handler retorna `AtualizarLimiteBancoResponse` com aviso descrevendo o valor em uso (ex.: "Este limite possui BRL X em utilização ativa...").
- Quando `NovaDataVigenciaFim` **não** informado, handler retorna `LimiteBancoDto` diretamente (sem wrapper).

> **Atenção ao tipo de retorno:** o command deve retornar `object` (ou usar `IRequest<LimiteBancoDto>` com um segundo command) para suportar os dois tipos de retorno. Alternativa mais limpa: `IRequest<AtualizarLimiteBancoResponse>` sempre, e o controller decide o que expor. Avaliar na implementação qual é mais coerente com os padrões existentes antes de decidir.

**Verificação:**
- `dotnet build` sem erros.
- `dotnet test --filter "Category=Unit"` verde.

**Dependências:** T1.

**Arquivos:**
- `src/Sgcf.Application/Cotacoes/Commands/UpdateLimiteBancoCommand.cs`
- `src/Sgcf.Application/Cotacoes/AtualizarLimiteBancoResponse.cs` (novo)

**Tamanho:** M (2 arquivos, handler complexo)

---

### T3 — API: controller PATCH + documentação

**O que faz:** Atualiza a action `Atualizar` no controller para aceitar os novos campos e retornar o tipo correto (`AtualizarLimiteBancoResponse` ou `LimiteBancoDto`). Atualiza o contrato documentado.

**Acceptance criteria:**
- `Atualizar()` no controller deserializa `NovaDataVigenciaFim`, `NovaDataVigenciaInicio`, `MotivoEncerramento` do body.
- Quando o handler retorna `AtualizarLimiteBancoResponse`, o controller retorna `200 OK` com esse body.
- Quando o handler retorna `LimiteBancoDto` diretamente, o comportamento é idêntico ao atual.
- `[ProducesResponseType]` atualizado para refletir ambos os casos.
- `docs/api/limites-banco.md` — seção PATCH atualizada com os novos campos e a descrição do response com `avisos`.

**Verificação:**
- `dotnet build` sem erros.
- Teste manual: `PATCH` sem `novaDataVigenciaFim` → body é `LimiteBancoDto` simples.
- Teste manual: `PATCH` com `novaDataVigenciaFim` e limite sem uso → body sem `avisos`.

**Dependências:** T2.

**Arquivos:**
- `src/Sgcf.Api/Controllers/LimitesBancoController.cs`
- `sgcf-backend/docs/api/limites-banco.md`

**Tamanho:** S (2 arquivos)

---

### — Checkpoint 1: RV-01 completo —

```
dotnet test --filter "Category!=Slow"   # verde
dotnet build                            # zero erros/warnings
```

Validar manualmente:
- `PATCH /limites-banco/{id}` com `novaDataVigenciaFim` → 200 OK, `dataVigenciaFim` persistido.
- `PATCH` com vigência sobreposta → 409 Conflict.
- `PATCH` sem novos campos → 200 OK, comportamento idêntico ao atual.

---

### T4 — Testes RV-01

**O que faz:** Escreve cobertura de testes unitários (Application.Tests com NSubstitute) e de integração (Api.IntegrationTests com WebApplicationFactory + Testcontainers) para todos os cenários RV-01 especificados.

**Cenários unitários (Application.Tests — `[Trait("Category","Unit")]`):**
- PATCH com `novaDataVigenciaFim` válida e sem utilização ativa → retorna DTO sem `avisos`.
- PATCH com `novaDataVigenciaFim` válida e `valorUtilizadoBrl > 0` → retorna `AtualizarLimiteBancoResponse` com aviso contendo o valor.
- PATCH com `novaDataVigenciaFim <= dataVigenciaInicio` → `ArgumentException`.
- PATCH com `novaDataVigenciaFim` sobrepondo outro limite → `InvalidOperationException`.
- PATCH sem `novaDataVigenciaFim` → retorna `LimiteBancoDto` simples (compatibilidade).
- `MotivoEncerramento` informado → persiste no limite.

**Cenários de integração (Api.IntegrationTests — `[Trait("Category","Slow")]`):**
- `PATCH /limites-banco/{id}` com `novaDataVigenciaFim` → `200 OK`; `GET /{id}` confirma campo persistido.
- PATCH com vigência sobreposta → `409 Conflict`.
- PATCH sem novos campos (cliente antigo) → `200 OK` com `LimiteBancoDto` simples.

**Verificação:**
- `dotnet test --filter "Category!=Slow"` verde (unitários).
- `dotnet test` verde (todos, incluindo Slow).

**Dependências:** T3.

**Arquivos:**
- `tests/Sgcf.Application.Tests/Cotacoes/AtualizarLimiteVigenciaTests.cs` (novo)
- `tests/Sgcf.Api.IntegrationTests/LimitesBanco/LimitesBancoVigenciaTests.cs` (novo)

**Tamanho:** M (2 novos arquivos)

---

## Fase 3 — RV-02: substituição atômica

### T5 — Application: `SubstituirLimiteBancoCommand`

**O que faz:** Cria o command, validator e handler para a operação atômica de substituição.

**Acceptance criteria:**
- `SubstituirLimiteBancoCommand` contém: `LimiteId`, `NovoInicio: DateOnly`, `NovoValorLimiteBrl: decimal`, `NovaDataVigenciaFim: DateOnly?`, `Observacoes: string?`, `MotivoEncerramento: string?`, `GarantiasExigidas: IReadOnlyList<CriarGarantiaExigidaItemRequest>?`.
- Validator rejeita `NovoValorLimiteBrl <= 0`.
- Validator rejeita `NovoInicio <= limite.DataVigenciaInicio` (verificado no handler após carregar o limite).
- Handler executa em uma transação: (1) `limite.Atualizar(novaDataVigenciaFim: novoInicio.MinusDays(1), motivoEncerramento: cmd.MotivoEncerramento)`; (2) verificar sobreposição do sucessor via `FindOverlappingAsync`; (3) verificar LG-09; (4) `LimiteBanco.Criar(...)` **sem** herdar antecipação; (5) `repo.Add(sucessor)`; (6) `repo.SaveChangesAsync()`.
- Retorna `LimiteBancoDto` do **sucessor** criado.
- Se `SaveChangesAsync` falhar, nenhuma das alterações persiste.

**Verificação:**
- `dotnet build` sem erros.
- `dotnet test --filter "Category=Unit"` verde.

**Dependências:** T1.

**Arquivos:**
- `src/Sgcf.Application/Cotacoes/Commands/SubstituirLimiteBancoCommand.cs` (novo)

**Tamanho:** M (1 arquivo, handler com lógica de transação)

---

### T6 — API: controller `POST /{id}/substituir` + documentação

**O que faz:** Adiciona a nova action ao controller e documenta o endpoint.

**Acceptance criteria:**
- `[HttpPost("{id:guid}/substituir")]` com `[Authorize(Policy = Policies.Admin)]`.
- Retorna `201 Created` com `Location` apontando para `GET /limites-banco/{id-do-sucessor}`.
- Trata `KeyNotFoundException` → 404, `InvalidOperationException` → 409, `ArgumentException` → 400.
- `docs/api/limites-banco.md` inclui a seção completa do endpoint `/substituir`.

**Verificação:**
- `dotnet build` sem erros.
- Teste manual: POST → 201 + `Location` correto.

**Dependências:** T5.

**Arquivos:**
- `src/Sgcf.Api/Controllers/LimitesBancoController.cs`
- `sgcf-backend/docs/api/limites-banco.md`

**Tamanho:** S (2 arquivos)

---

### — Checkpoint 2: RV-02 completo —

```
dotnet test --filter "Category!=Slow"   # verde
dotnet build                            # zero erros/warnings
```

Validar manualmente o fluxo completo de reavaliação:
- `POST /limites-banco/{id-antigo}/substituir` → 201 + Location.
- `GET /limites-banco/{id-antigo}` → `dataVigenciaFim = novoInicio - 1 dia`, `motivoEncerramento` preenchido.
- `GET /limites-banco/{id-novo}` → sucessor sem antecipação, `dataVigenciaInicio = novoInicio`.
- Tentar `POST /limites-banco` com mesmo banco/modalidade sobreposto → 409.

---

### T7 — Testes RV-02

**O que faz:** Cobertura de testes para todos os cenários da substituição atômica.

**Cenários unitários (Application.Tests):**
- Substituição bem-sucedida → sucessor com `dataVigenciaInicio = novoInicio`; anterior com `dataVigenciaFim = novoInicio - 1 dia`.
- Successor não herda `PadraoAntecipacao` do anterior.
- `novoInicio <= limite.DataVigenciaInicio` → `ArgumentException`.
- Valor do sucessor acima do limite global (LG-09) → `InvalidOperationException`.
- `MotivoEncerramento` informado → `limite.MotivoEncerramento` atualizado no anterior.

**Cenários de integração (Api.IntegrationTests):**
- `POST /{id}/substituir` → `201 Created` + `Location` correto.
- `GET /{id-antigo}` pós-substituição → `dataVigenciaFim` e `motivoEncerramento` preenchidos.
- `GET /{id-novo}` pós-substituição → `valorUtilizadoBrl = 0`, antecipação nula.
- `POST /limites-banco` com sobreposição ao sucessor → `409 Conflict`.

**Verificação:**
- `dotnet test` verde (todos os testes).

**Dependências:** T6.

**Arquivos:**
- `tests/Sgcf.Application.Tests/Cotacoes/SubstituirLimiteBancoTests.cs` (novo)
- `tests/Sgcf.Api.IntegrationTests/LimitesBanco/SubstituirLimiteBancoApiTests.cs` (novo)

**Tamanho:** M (2 novos arquivos)

---

### — Checkpoint Final —

```
dotnet test   # suite completa verde
dotnet build  # zero erros
```

Verificar:
- Todos os critérios de aceitação do spec v1.1 marcados.
- Documentação `limites-banco.md` cobre PATCH estendido e `/substituir`.
- Nenhum teste existente regrediu.

---

## Riscos e mitigações

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| O controller PATCH tem tipo de retorno ambíguo (`LimiteBancoDto` ou `AtualizarLimiteBancoResponse`) — pode exigir refactor de como o MediatR é tipado | Médio | Avaliar em T2: se o MediatR não suportar `IRequest<object>` limpo, usar sempre `AtualizarLimiteBancoResponse` e o controller omite `avisos` quando vazio |
| `dotnet ef migrations add` pode gerar conflito se outra migration pendente existir | Baixo | Verificar `dotnet ef migrations list` antes de T1 |
| Testes de integração existentes em `LimitesBancoSobreposicaoTests` podem interagir com o novo comportamento de vigência | Baixo | Rodar suite completa após T3 antes de prosseguir |

---

## Paralelização

Após T1 estar concluído e mergeado:
- Um agente/sessão executa T2 → T3 → T4 (trilha RV-01).
- Outro agente/sessão executa T5 → T6 → T7 (trilha RV-02).

As trilhas não compartilham arquivos até T3 e T6 (controller), onde ambas modificam `LimitesBancoController.cs` — coordenar esse merge.
