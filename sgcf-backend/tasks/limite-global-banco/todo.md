# TODO — LimiteGlobalBanco Implementation

> Companion to `tasks/limite-global-banco/plan.md`.
> Spec: `docs/specs/limites-banco/SPEC_LIMITE_GLOBAL.md`
> Marque `[~]` em andamento · `[x]` concluído.
> Cada item inclui o comando de verificação — o task é done somente quando o comando passa.

---

## Phase 1 — Domain Foundation

- [x] **T01** — Criar `src/Sgcf.Domain/Cotacoes/LimiteGlobalBancoHistorico.cs`
  (entidade filha append-only; espelha `LimiteBancoHistorico`)
  `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj`

- [x] **T02** — Criar `src/Sgcf.Domain/Cotacoes/LimiteGlobalBanco.cs`
  (agregado raiz com `Criar`, `Atualizar`, `EncerrarVigencia`; invariantes LG-01..LG-08)
  `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj`

- [x] **T03** — Criar `src/Sgcf.Domain/Cotacoes/IConsultaSaldoBanco.cs`
  (interface de domain service, 4 métodos async conforme SPEC §3.4)
  `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj`

- [x] **T04** — Criar testes unitários de domínio
  - `tests/Sgcf.Domain.Tests/Cotacoes/LimiteGlobalBancoTests.cs`
  - `tests/Sgcf.Domain.Tests/Cotacoes/LimiteGlobalBancoHistoricoTests.cs`
  `dotnet test tests/Sgcf.Domain.Tests/ --filter "FullyQualifiedName~LimiteGlobalBanco"`

### ✅ Checkpoint 1
```
dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj
dotnet test tests/Sgcf.Domain.Tests/ --filter "FullyQualifiedName~LimiteGlobalBanco"
dotnet test tests/Sgcf.Domain.Tests/
```

---

## Phase 2 — Infrastructure

- [x] **T05** — Criar configurações EF Core
  - `src/Sgcf.Infrastructure/Persistence/Configurations/LimiteGlobalBancoHistoricoConfiguration.cs`
  - `src/Sgcf.Infrastructure/Persistence/Configurations/LimiteGlobalBancoConfiguration.cs`
  `dotnet build src/Sgcf.Infrastructure/Sgcf.Infrastructure.csproj`

- [x] **T06** — Adicionar DbSets + criar interface `ILimiteGlobalBancoRepository`
  - Modificar `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` (2 linhas)
  - Criar `src/Sgcf.Application/Cotacoes/ILimiteGlobalBancoRepository.cs`
  `dotnet build src/Sgcf.Infrastructure/Sgcf.Infrastructure.csproj && dotnet build src/Sgcf.Application/Sgcf.Application.csproj`

- [x] **T07** — Criar implementações de repositório e serviço
  - `src/Sgcf.Infrastructure/Persistence/Repositories/LimiteGlobalBancoRepository.cs`
  - `src/Sgcf.Infrastructure/Persistence/Repositories/ConsultaSaldoBancoService.cs`
  `dotnet build src/Sgcf.Infrastructure/Sgcf.Infrastructure.csproj`

- [x] **T08** — Registrar no DI
  - Modificar `src/Sgcf.Infrastructure/DependencyInjection.cs` (2 linhas `AddScoped`)
  `dotnet build`

- [x] **T09** — Gerar e aplicar migration `S33_LimiteGlobalBanco`
  ```
  dotnet ef migrations add S33_LimiteGlobalBanco \
    --project src/Sgcf.Infrastructure \
    --startup-project src/Sgcf.Api \
    --output-dir Persistence/Migrations
  ```
  Adicionar manualmente os blocos RLS (ver plan T09).
  ```
  dotnet ef database update \
    --project src/Sgcf.Infrastructure \
    --startup-project src/Sgcf.Api
  ```

### ✅ Checkpoint 2
```
dotnet build
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
dotnet test --filter "Category!=Slow"
```

---

## Phase 3 — Application Layer (CQRS)

- [x] **T10** — Criar DTOs
  - `src/Sgcf.Application/Cotacoes/LimiteGlobalBancoDto.cs`
  - `src/Sgcf.Application/Cotacoes/LimiteGlobalBancoVigenteDto.cs`
  `dotnet build src/Sgcf.Application/Sgcf.Application.csproj`

- [x] **T11** — Criar `CriarLimiteGlobalBancoCommand` + handler (LG-05, LG-13)
  - `src/Sgcf.Application/Cotacoes/Commands/CriarLimiteGlobalBancoCommand.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~CriarLimiteGlobalBancoHandler"`

- [x] **T12** — Criar `AtualizarLimiteGlobalBancoCommand` + handler (LG-10)
  - `src/Sgcf.Application/Cotacoes/Commands/AtualizarLimiteGlobalBancoCommand.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~AtualizarLimiteGlobalBancoHandler"`

- [x] **T13** — Criar `EncerrarVigenciaLimiteGlobalBancoCommand` + handler (LG-08)
  - `src/Sgcf.Application/Cotacoes/Commands/EncerrarVigenciaLimiteGlobalBancoCommand.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~EncerrarVigenciaLimiteGlobalBancoHandler"`

- [x] **T14** — Criar query handlers
  - `src/Sgcf.Application/Cotacoes/Queries/ListarLimitesGlobaisBancoQuery.cs`
  - `src/Sgcf.Application/Cotacoes/Queries/GetLimiteGlobalBancoQuery.cs`
  - `src/Sgcf.Application/Cotacoes/Queries/GetLimiteGlobalVigenteBancoQuery.cs`
  `dotnet build src/Sgcf.Application/Sgcf.Application.csproj`

- [x] **T15** — Criar testes unitários dos handlers
  - `tests/Sgcf.Application.Tests/Cotacoes/CriarLimiteGlobalBancoHandlerTests.cs`
  - `tests/Sgcf.Application.Tests/Cotacoes/AtualizarLimiteGlobalBancoHandlerTests.cs`
  - `tests/Sgcf.Application.Tests/Cotacoes/EncerrarVigenciaLimiteGlobalBancoHandlerTests.cs`
  - `tests/Sgcf.Application.Tests/Cotacoes/LimiteGlobalBancoQueryHandlerTests.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~LimiteGlobalBanco"`

### ✅ Checkpoint 3
```
dotnet test --filter "Category=Unit"
dotnet test --filter "Category!=Slow"
```

---

## Phase 4 — API Layer

- [x] **T16** — Criar `LimitesGlobaisBancoController` + modificar `BancosController`
  - Criar `src/Sgcf.Api/Controllers/LimitesGlobaisBancoController.cs` (5 endpoints)
  - Modificar `src/Sgcf.Api/Controllers/BancosController.cs` (1 endpoint `GET /{bancoId}/limite-global-vigente`)
  `dotnet build src/Sgcf.Api/Sgcf.Api.csproj`

- [x] **T17** — Criar testes de integração HTTP
  - `tests/Sgcf.Api.IntegrationTests/LimitesGlobaisBanco/LimitesGlobaisBancoApiFixture.cs`
  - `tests/Sgcf.Api.IntegrationTests/LimitesGlobaisBanco/LimitesGlobaisBancoEndpointsTests.cs`
  `dotnet test tests/Sgcf.Api.IntegrationTests/ --filter "FullyQualifiedName~LimitesGlobaisBanco"`

### ✅ Checkpoint 4
```
dotnet test --filter "FullyQualifiedName~LimiteGlobalBanco"
dotnet test
```
> Swagger deve mostrar 6 novos endpoints.

---

## Phase 5 — Cross-Cutting Validation

- [x] **T18** — Adicionar verificação LG-09 nos handlers existentes
  - Modificar `src/Sgcf.Application/Cotacoes/Commands/CreateLimiteBancoCommand.cs`
  - Modificar `src/Sgcf.Application/Cotacoes/Commands/UpdateLimiteBancoCommand.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~LimiteBanco"`

- [x] **T19** — Criar testes de invariantes cruzadas
  - `tests/Sgcf.Application.Tests/Cotacoes/LimiteGlobalBancoInvariantesCruzadasTests.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~LimiteGlobalBancoInvariantes"`

- [x] **T20** — Criar testes de integração `ConsultaSaldoBancoService` (Testcontainers)
  - `tests/Sgcf.Application.Tests/Cotacoes/ConsultaSaldoBancoServiceTests.cs`
  `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~ConsultaSaldoBancoService"`

### ✅ Checkpoint 5 — Final
```
dotnet test --filter "Category!=Slow"
dotnet test
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```
> Cobertura: Domain ≥ 95% · Application ≥ 85% · Infrastructure ≥ 70%
> Todas as 13 invariantes (LG-01..LG-13) exercidas por ao menos um teste

---

## Resumo

| Fase | Tasks | Arquivos novos | Arquivos modificados |
|---|---|---|---|
| 1 — Domain | T01–T04 | 4 | 0 |
| 2 — Infrastructure | T05–T09 | 4 | 2 (DbContext, DI) + 1 migration gerada |
| 3 — Application | T10–T15 | 9 | 0 |
| 4 — API | T16–T17 | 3 | 1 (BancosController) |
| 5 — Cross-Cutting | T18–T20 | 2 | 2 (handlers existentes) |
| **Total** | **20** | **22** | **5** |
