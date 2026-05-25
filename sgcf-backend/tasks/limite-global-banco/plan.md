# Implementation Plan — LimiteGlobalBanco (Limite Guarda-Chuva por Banco)

**Spec:** `docs/specs/limites-banco/SPEC_LIMITE_GLOBAL.md` v1.0
**Data:** 2026-05-23
**Modelo:** Clean Architecture — Domain / Application / Infrastructure / Api

---

## Overview

`LimiteGlobalBanco` introduz uma linha de crédito guarda-chuva por banco que coexiste com o
`LimiteBanco` (por modalidade) já existente. Dois regimes operacionais surgem: **Cenário A**
(apenas global — qualquer modalidade drena de um pool único) e **Cenário B** (linhas por
modalidade cuja soma não pode exceder o guarda-chuva). A feature exige novas entidades de
domínio, uma interface de domain service, tabelas PostgreSQL com RLS, handlers CQRS, validação
cruzada em dois handlers existentes (`CreateLimiteBancoCommandHandler`,
`UpdateLimiteBancoCommandHandler`) e seis novos endpoints de API.

A implementação segue cadeia de dependência bottom-up: entidades de domínio devem compilar
antes das configurações de infraestrutura; configurações antes da migration EF; migration
validada antes dos handlers; handlers antes do controller.

---

## Architecture Decisions

| # | Decisão | Justificativa |
|---|---|---|
| AD-01 | `LimiteGlobalBanco` vive em `Sgcf.Domain/Cotacoes/` | Mesmo bounded context de `LimiteBanco`; infraestrutura `Money`, `ITenantScoped`, `IAuditable` já importada. |
| AD-02 | `IConsultaSaldoBanco` vive em `Sgcf.Domain.Cotacoes` | Domínio precisa do contrato para expressar a invariante (saldo ≥ novo limite) sem depender de repositório. |
| AD-03 | `ILimiteGlobalBancoRepository` vive em `Sgcf.Application.Cotacoes` | Espelha `ILimiteBancoRepository`. Application detém as portas; Infrastructure detém os adaptadores. |
| AD-04 | `ValorUtilizado` nunca persiste | Calculado via `IConsultaSaldoBanco`. Persistência criaria risco de dado obsoleto. |
| AD-05 | Índice único parcial `(tenant_id, banco_id) WHERE data_vigencia_fim IS NULL` | Guarda de banco-de-dados para LG-04; mais confiável que guarda de aplicação. |
| AD-06 | Verificação de sobreposição em `CriarLimiteGlobalBancoHandler` via `FindOverlappingAsync` | Mesmo padrão de `CreateLimiteBancoCommandHandler`. Consistente e testável. |
| AD-07 | `LimiteGlobalBancoVigenteDto` separado de `LimiteGlobalBancoDto` | Campos computados (`valorUtilizado`, `valorDisponivel`, `regime`) só fazem sentido no endpoint "vigente". |
| AD-08 | Invariantes cruzadas LG-09 e LG-13 ficam nos handlers da Application | Agregados separados não se alcançam; o handler é o lugar correto para coordenar. |
| AD-09 | `ConsultaSaldoBancoService` registrado como `IConsultaSaldoBanco` em `DependencyInjection.cs` | Padrão existente para todos os outros serviços de infraestrutura. |
| AD-10 | RLS via `migrationBuilder.Sql(...)` | EF não tem suporte nativo a `ENABLE ROW LEVEL SECURITY` / `CREATE POLICY`. Espelha migration S32. |

---

## Phase 1 — Domain Foundation

### T01 — Entidade `LimiteGlobalBancoHistorico`

**Descrição:** Criar a entidade filha append-only que registra cada alteração do valor do
guarda-chuva. Espelha `LimiteBancoHistorico` exatamente.

**Arquivos (1):**
- `src/Sgcf.Domain/Cotacoes/LimiteGlobalBancoHistorico.cs` — NOVO

**Critérios de aceite:**
- `sealed`, herda `Entity`, implementa `IAuditable`.
- Propriedades: `LimiteGlobalBancoId`, `ValorAnteriorBrlDecimal` (nullable), `ValorNovoBrlDecimal`, `RegistradoEm` (Instant), `Observacoes` (string?).
- Computed read-only: `ValorAnteriorBrl` (Money?) e `ValorNovoBrl` (Money).
- Factory `internal static Criar(...)` valida `limiteGlobalBancoId != Guid.Empty` e `valorNovoBrl.Moeda == Moeda.Brl`.
- Construtor privado `private LimiteGlobalBancoHistorico() {}` para EF.
- Zero atributos EF no arquivo.

**Verificação:** `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj`

**Dependências:** Nenhuma.

**Tamanho:** S (~65 linhas).

---

### T02 — Agregado `LimiteGlobalBanco`

**Descrição:** Criar o agregado raiz com factory `Criar`, métodos de mutação `Atualizar` e
`EncerrarVigencia`, lista backing `_historico`. Implementa invariantes LG-01 a LG-08.

**Arquivos (1):**
- `src/Sgcf.Domain/Cotacoes/LimiteGlobalBanco.cs` — NOVO

**Critérios de aceite:**
- LG-01: `Criar` com `valorLimiteBrl.Moeda != Brl` lança `ArgumentException`.
- LG-02: `Criar` com `valorLimiteBrl.Valor <= 0` lança `ArgumentOutOfRangeException`.
- LG-03: `Criar` com `dataVigenciaFim <= dataVigenciaInicio` lança `ArgumentException`.
- LG-07: `Criar` appenda uma entrada `LimiteGlobalBancoHistorico` com `ValorAnterior = null`.
- LG-06: `Atualizar` com `novoLimiteBrl < saldoDevedorAtual` lança `InvalidOperationException`.
- LG-07: `Atualizar` com valor diferente appenda nova entrada; valor igual não appenda.
- LG-08: `EncerrarVigencia` com `DataVigenciaFim` já definida lança `InvalidOperationException`.
- LG-08: `EncerrarVigencia` com `dataFim < DataVigenciaInicio` lança `ArgumentException`.
- Construtor privado para EF presente.

**Verificação:** `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj`

**Dependências:** T01.

**Tamanho:** M (~110 linhas).

---

### T03 — Interface `IConsultaSaldoBanco`

**Descrição:** Definir o contrato de domain service que a camada Application implementará
(implementação fica na Infrastructure). Quatro métodos async usados pelos handlers.

**Arquivos (1):**
- `src/Sgcf.Domain/Cotacoes/IConsultaSaldoBanco.cs` — NOVO

**Critérios de aceite:**
- Interface `public`, namespace `Sgcf.Domain.Cotacoes`.
- Quatro métodos conforme SPEC §3.4: `CalcularSaldoDevedorBancoAsync`, `CalcularUtilizadoAgregadoModalidadesAsync`, `CalcularSomaLimitesModalidadesAsync`, `BancoEmRegimePerModalityAsync`.
- Zero referências a EF, MediatR ou Application.

**Verificação:** `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj`

**Dependências:** T01, T02.

**Tamanho:** XS (~35 linhas).

---

### T04 — Testes unitários de domínio (LG-01..LG-08)

**Descrição:** Escrever os 9 testes obrigatórios do SPEC §8.2 mais testes de borda e happy path.

**Arquivos (2):**
- `tests/Sgcf.Domain.Tests/Cotacoes/LimiteGlobalBancoTests.cs` — NOVO
- `tests/Sgcf.Domain.Tests/Cotacoes/LimiteGlobalBancoHistoricoTests.cs` — NOVO

**Critérios de aceite:**
- Todos os 9 testes do SPEC §8.2 com `[Trait("Category", "Domain")]`.
- `Criar_ComDadosValidos_RetornaAgregadoComHistoricoInicial` (happy path).
- `Atualizar_MesmoValor_NaoGravaEntradaDuplicadaNoHistorico`.
- `LimiteGlobalBancoHistorico_Criar_ComLimiteIdVazio_LancaArgumentException`.
- Zero chamadas a `DateTime.Now` / `DateTimeOffset.UtcNow`.

**Verificação:** `dotnet test tests/Sgcf.Domain.Tests/ --filter "FullyQualifiedName~LimiteGlobalBanco"`

**Dependências:** T01, T02, T03.

**Tamanho:** M (~180 linhas).

---

### Checkpoint 1 — Domínio compila, todos os testes de domínio verdes

> `dotnet build src/Sgcf.Domain/Sgcf.Domain.csproj` → 0
> `dotnet test tests/Sgcf.Domain.Tests/ --filter "FullyQualifiedName~LimiteGlobalBanco"` → 0
> `dotnet test tests/Sgcf.Domain.Tests/` → 0 (sem regressões)

---

## Phase 2 — Infrastructure

### T05 — Configurações EF Core

**Descrição:** Mapear as duas novas entidades para tabelas PostgreSQL seguindo o padrão existente.

**Arquivos (2):**
- `src/Sgcf.Infrastructure/Persistence/Configurations/LimiteGlobalBancoHistoricoConfiguration.cs` — NOVO
- `src/Sgcf.Infrastructure/Persistence/Configurations/LimiteGlobalBancoConfiguration.cs` — NOVO

**Critérios de aceite:**
- Tabelas: `limite_global_banco` e `limite_global_banco_historico`.
- Todos os campos em snake_case, tipos conforme SPEC §6.1.
- Índice único parcial `(tenant_id, banco_id) WHERE data_vigencia_fim IS NULL`.
- FK `banco_id → banco_config.id` com `Restrict`.
- Cascade de `limite_global_banco` → `limite_global_banco_historico`.
- Propriedades computed (`ValorLimiteBrl`, `ValorAnteriorBrl`, `ValorNovoBrl`) com `Ignore()`.

**Verificação:** `dotnet build src/Sgcf.Infrastructure/Sgcf.Infrastructure.csproj`

**Dependências:** T01, T02.

**Tamanho:** M (~120 linhas).

---

### T06 — DbSets + interface `ILimiteGlobalBancoRepository`

**Descrição:** Adicionar dois DbSet ao `SgcfDbContext` e definir a interface de repositório na
camada Application.

**Arquivos (2):**
- `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` — MODIFICAR (2 linhas)
- `src/Sgcf.Application/Cotacoes/ILimiteGlobalBancoRepository.cs` — NOVO

**DbSets a adicionar (após `LimitesBanco`):**
```csharp
public DbSet<LimiteGlobalBanco> LimitesGlobaisBanco => Set<LimiteGlobalBanco>();
public DbSet<LimiteGlobalBancoHistorico> LimitesGlobaisBancoHistorico => Set<LimiteGlobalBancoHistorico>();
```

**Métodos da interface:**
- `void Add(LimiteGlobalBanco limite)`
- `Task<LimiteGlobalBanco?> GetByIdAsync(Guid id, CancellationToken ct = default)`
- `Task<LimiteGlobalBanco?> GetByIdTrackingAsync(Guid id, CancellationToken ct = default)`
- `Task<LimiteGlobalBanco?> GetVigenteByBancoAsync(Guid bancoId, CancellationToken ct = default)`
- `Task<LimiteGlobalBanco?> FindOverlappingAsync(Guid bancoId, LocalDate inicio, LocalDate? fim, Guid? excluirId = null, CancellationToken ct = default)`
- `Task<IReadOnlyList<LimiteGlobalBanco>> ListAsync(Guid? bancoId, LocalDate? vigentesEm, CancellationToken ct = default)`
- `Task<int> SaveChangesAsync(CancellationToken ct = default)`

**Verificação:** `dotnet build src/Sgcf.Infrastructure/Sgcf.Infrastructure.csproj && dotnet build src/Sgcf.Application/Sgcf.Application.csproj`

**Dependências:** T05.

**Tamanho:** S (~70 linhas).

---

### T07 — `LimiteGlobalBancoRepository` e `ConsultaSaldoBancoService`

**Descrição:** Implementar repositório e o serviço `IConsultaSaldoBanco` na camada Infrastructure.

**Arquivos (2):**
- `src/Sgcf.Infrastructure/Persistence/Repositories/LimiteGlobalBancoRepository.cs` — NOVO
- `src/Sgcf.Infrastructure/Persistence/Repositories/ConsultaSaldoBancoService.cs` — NOVO

**`ConsultaSaldoBancoService`:**
- `CalcularSaldoDevedorBancoAsync`: soma de `Contrato.SaldoDevedorBrl` ativos por banco.
- `CalcularUtilizadoAgregadoModalidadesAsync`: `SUM(limite_banco.valor_utilizado_brl)` por banco, vigentes.
- `CalcularSomaLimitesModalidadesAsync`: `SUM(limite_banco.valor_limite_brl)` por banco, vigentes, excluindo `excluirLimiteBancoId`.
- `BancoEmRegimePerModalityAsync`: `EXISTS(limite_banco WHERE banco_id = bancoId AND data_vigencia_fim IS NULL)`.

**Verificação:** `dotnet build src/Sgcf.Infrastructure/Sgcf.Infrastructure.csproj`

**Dependências:** T06.

**Tamanho:** M (~130 linhas).

---

### T08 — Registro no DI

**Arquivos (1):**
- `src/Sgcf.Infrastructure/DependencyInjection.cs` — MODIFICAR (adicionar após `ILimiteBancoRepository`):

```csharp
services.AddScoped<ILimiteGlobalBancoRepository, LimiteGlobalBancoRepository>();
services.AddScoped<IConsultaSaldoBanco, ConsultaSaldoBancoService>();
```

**Verificação:** `dotnet build`

**Dependências:** T07.

**Tamanho:** XS (2 linhas).

---

### T09 — Migration EF Core `S33_LimiteGlobalBanco`

**Descrição:** Gerar e verificar a migration. Adicionar manualmente os blocos RLS.

**Passos:**
1. Gerar:
   ```
   dotnet ef migrations add S33_LimiteGlobalBanco \
     --project src/Sgcf.Infrastructure \
     --startup-project src/Sgcf.Api \
     --output-dir Persistence/Migrations
   ```
2. Verificar que o `Up` contém: `CreateTable` para ambas as tabelas + índice único parcial com `filter: "data_vigencia_fim IS NULL"`.
3. Adicionar manualmente os blocos RLS (ver padrão em migration S32):
   ```csharp
   migrationBuilder.Sql("ALTER TABLE sgcf.limite_global_banco ENABLE ROW LEVEL SECURITY;");
   migrationBuilder.Sql("CREATE POLICY tenant_isolation ON sgcf.limite_global_banco USING (tenant_id = current_setting('app.tenant_id', true)::uuid);");
   migrationBuilder.Sql("ALTER TABLE sgcf.limite_global_banco_historico ENABLE ROW LEVEL SECURITY;");
   migrationBuilder.Sql("CREATE POLICY tenant_isolation ON sgcf.limite_global_banco_historico USING (tenant_id = current_setting('app.tenant_id', true)::uuid);");
   ```
   Adicionar blocos `DROP POLICY` e `DISABLE ROW LEVEL SECURITY` no `Down`.
4. Aplicar: `dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api`

**Critérios de aceite:**
- Migration aplica sem erros.
- `SELECT tablename, rowsecurity FROM pg_tables WHERE schemaname = 'sgcf' AND tablename LIKE 'limite_global%'` retorna `t` para ambas.
- Índice único parcial visível em `\d sgcf.limite_global_banco`.

**Dependências:** T05, T06, T08.

**Tamanho:** S (1 arquivo gerado + ~20 linhas manuais de RLS).

---

### Checkpoint 2 — Infrastructure completa, schema DB válido

> `dotnet build` → 0 para todos os projetos
> Migration aplicada sem erros; RLS ativo confirmado
> `dotnet test --filter "Category!=Slow"` → 0

---

## Phase 3 — Application Layer (CQRS)

### T10 — DTOs

**Arquivos (2):**
- `src/Sgcf.Application/Cotacoes/LimiteGlobalBancoDto.cs` — NOVO (`LimiteGlobalBancoDto` + `LimiteGlobalBancoHistoricoDto`)
- `src/Sgcf.Application/Cotacoes/LimiteGlobalBancoVigenteDto.cs` — NOVO

**`LimiteGlobalBancoDto`:** `Id`, `BancoId`, `ValorLimiteBrl` (decimal), `DataVigenciaInicio` (DateOnly), `DataVigenciaFim` (DateOnly?), `Observacoes`, `CreatedAt`, `UpdatedAt`, `Historico`.

**`LimiteGlobalBancoVigenteDto`:** Herda campos acima + `ValorUtilizadoBrl`, `ValorDisponivelBrl`, `Regime` (string: `"GlobalPuro"` ou `"PerModalidade"`).

**Verificação:** `dotnet build src/Sgcf.Application/Sgcf.Application.csproj`

**Dependências:** T01, T02, T06.

**Tamanho:** S (~80 linhas).

---

### T11 — `CriarLimiteGlobalBancoCommand` + handler

**Arquivos (1):**
- `src/Sgcf.Application/Cotacoes/Commands/CriarLimiteGlobalBancoCommand.cs` — NOVO

**Campos do command:** `Guid BancoId`, `decimal ValorLimiteBrl`, `DateOnly DataVigenciaInicio`, `DateOnly? DataVigenciaFim`, `string? Observacoes`.

**Lógica do handler:**
1. Verificar banco existe via `IBancoRepository` → 404 se não encontrado.
2. Verificar sobreposição via `FindOverlappingAsync` → 409 se sobrepõe (LG-05).
3. LG-13: se banco tem `LimiteBanco` vigentes, `CalcularSomaLimitesModalidadesAsync` > `ValorLimiteBrl` → 409.
4. `LimiteGlobalBanco.Criar(...)`.
5. `repo.Add(limite); await repo.SaveChangesAsync(ct)`.
6. Retornar `LimiteGlobalBancoDto.From(limite)`.

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~CriarLimiteGlobalBancoHandler"`

**Dependências:** T06, T07, T10.

**Tamanho:** M (~110 linhas).

---

### T12 — `AtualizarLimiteGlobalBancoCommand` + handler

**Arquivos (1):**
- `src/Sgcf.Application/Cotacoes/Commands/AtualizarLimiteGlobalBancoCommand.cs` — NOVO

**Campos do command:** `Guid Id`, `decimal? ValorLimiteBrl`, `DateOnly? DataVigenciaInicio`, `DateOnly? DataVigenciaFim`, `string? Observacoes` (semântica PATCH — todos opcionais).

**Lógica do handler:** Conforme SPEC §7.2 — busca saldo devedor via `IConsultaSaldoBanco` antes de chamar `Atualizar`; verifica regime A ou B para a validação de redução.

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~AtualizarLimiteGlobalBancoHandler"`

**Dependências:** T11.

**Tamanho:** M (~95 linhas).

---

### T13 — `EncerrarVigenciaLimiteGlobalBancoCommand` + handler

**Arquivos (1):**
- `src/Sgcf.Application/Cotacoes/Commands/EncerrarVigenciaLimiteGlobalBancoCommand.cs` — NOVO

**Campos do command:** `Guid Id`, `DateOnly DataFim`.

**Lógica:** Carrega via `GetByIdTrackingAsync` → `InvalidOperationException` se null. Chama `limite.EncerrarVigencia(dataFim, _clock)`. Salva. Retorna DTO.

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~EncerrarVigenciaLimiteGlobalBancoHandler"`

**Dependências:** T12.

**Tamanho:** S (~55 linhas).

---

### T14 — Query handlers

**Arquivos (3):**
- `src/Sgcf.Application/Cotacoes/Queries/ListarLimitesGlobaisBancoQuery.cs` — NOVO
- `src/Sgcf.Application/Cotacoes/Queries/GetLimiteGlobalBancoQuery.cs` — NOVO
- `src/Sgcf.Application/Cotacoes/Queries/GetLimiteGlobalVigenteBancoQuery.cs` — NOVO

**`GetLimiteGlobalVigenteBancoQuery`:** Determina regime via `BancoEmRegimePerModalityAsync`. Computa `valorUtilizado` baseado no regime (A ou B). Retorna `LimiteGlobalBancoVigenteDto`.

**Verificação:** `dotnet build src/Sgcf.Application/Sgcf.Application.csproj`

**Dependências:** T10, T06.

**Tamanho:** M (~130 linhas).

---

### T15 — Testes unitários dos handlers

**Arquivos (4):**
- `tests/Sgcf.Application.Tests/Cotacoes/CriarLimiteGlobalBancoHandlerTests.cs` — NOVO
- `tests/Sgcf.Application.Tests/Cotacoes/AtualizarLimiteGlobalBancoHandlerTests.cs` — NOVO
- `tests/Sgcf.Application.Tests/Cotacoes/EncerrarVigenciaLimiteGlobalBancoHandlerTests.cs` — NOVO
- `tests/Sgcf.Application.Tests/Cotacoes/LimiteGlobalBancoQueryHandlerTests.cs` — NOVO

**Testes obrigatórios (SPEC §8.3):**
- `CriarLimiteGlobal_ComSomaLimitesBancoMaior_Retorna409` (LG-13)
- `AtualizarLimiteGlobal_ReduzindoAbaixoSaldoDevedor_RegimeA_Bloqueia` (LG-10/A)
- `AtualizarLimiteGlobal_ReduzindoAbaixoSomaLimitesModalidade_RegimeB_Bloqueia` (LG-10/B)
- `CriarLimiteGlobal_ComSobreposicaoDeVigencia_Retorna409` (LG-04/LG-05)
- `GetLimiteGlobalVigente_RegimeA_ComputaValorUtilizadoCorreto`
- `GetLimiteGlobalVigente_RegimeB_ComputaValorUtilizadoCorreto`

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~LimiteGlobalBanco"`

**Dependências:** T11, T12, T13, T14.

**Tamanho:** L (~300 linhas).

---

### Checkpoint 3 — Application layer completa, todos os unit tests verdes

> `dotnet test --filter "Category=Unit" --filter "FullyQualifiedName~LimiteGlobalBanco"` → 0
> `dotnet test --filter "Category!=Slow"` → 0

---

## Phase 4 — API Layer

### T16 — `LimitesGlobaisBancoController` + modificação em `BancosController`

**Arquivos (2):**
- `src/Sgcf.Api/Controllers/LimitesGlobaisBancoController.cs` — NOVO (5 endpoints)
- `src/Sgcf.Api/Controllers/BancosController.cs` — MODIFICAR (1 endpoint)

| Rota | Método | Command/Query | Sucesso | Erros |
|---|---|---|---|---|
| `GET /api/v1/limites-globais-banco` | `ListarLimitesGlobaisBancoQuery` | 200 | — |
| `GET /api/v1/limites-globais-banco/{id}` | `GetLimiteGlobalBancoQuery` | 200 | 404 |
| `POST /api/v1/limites-globais-banco` | `CriarLimiteGlobalBancoCommand` | 201 | 400, 404, 409 |
| `PATCH /api/v1/limites-globais-banco/{id}` | `AtualizarLimiteGlobalBancoCommand` | 200 | 400, 404, 409 |
| `POST /api/v1/limites-globais-banco/{id}/encerrar-vigencia` | `EncerrarVigenciaLimiteGlobalBancoCommand` | 200 | 400, 404, 409 |
| `GET /api/v1/bancos/{bancoId}/limite-global-vigente` | `GetLimiteGlobalVigenteBancoQuery` | 200 | 404 |

**Mapeamento de exceções:**
- `KeyNotFoundException` → 404
- `InvalidOperationException` → 409 `{ "error": "..." }`
- `ArgumentException` → 400 `{ "error": "..." }`

**Verificação:** `dotnet build src/Sgcf.Api/Sgcf.Api.csproj`; Swagger mostra 6 novos endpoints.

**Dependências:** T11, T12, T13, T14.

**Tamanho:** M (~140 linhas).

---

### T17 — Testes de integração HTTP

**Arquivos (2):**
- `tests/Sgcf.Api.IntegrationTests/LimitesGlobaisBanco/LimitesGlobaisBancoApiFixture.cs` — NOVO
- `tests/Sgcf.Api.IntegrationTests/LimitesGlobaisBanco/LimitesGlobaisBancoEndpointsTests.cs` — NOVO

**Fluxos obrigatórios (SPEC §8.4):**
1. `POST` → 201; `historico` tem exatamente 1 entrada.
2. GET vigente Cenário A: `regime = "GlobalPuro"`, `valorUtilizado = Σ contratos`.
3. GET vigente Cenário B: `regime = "PerModalidade"`, `valorUtilizado = Σ LimiteBanco.ValorUtilizadoBrl`.
4. `PATCH` reduzindo abaixo do saldo → 409.
5. `POST .../encerrar-vigencia` → 200, `dataVigenciaFim` definida.
6. Criar dois registros vigentes para o mesmo banco → segundo POST → 409.

`[Trait("Category", "Slow")]`.

**Verificação:** `dotnet test tests/Sgcf.Api.IntegrationTests/ --filter "FullyQualifiedName~LimitesGlobaisBanco"`

**Dependências:** T16.

**Tamanho:** M (~220 linhas).

---

### Checkpoint 4 — Stack completo, todos os testes verdes

> `dotnet test --filter "FullyQualifiedName~LimiteGlobalBanco"` → 0
> `dotnet test` → 0 (suite completa)
> OpenAPI mostra 6 novos endpoints

---

## Phase 5 — Cross-Cutting Validation

### T18 — LG-09 em handlers existentes

**Arquivos (2):**
- `src/Sgcf.Application/Cotacoes/Commands/CreateLimiteBancoCommand.cs` — MODIFICAR
- `src/Sgcf.Application/Cotacoes/Commands/UpdateLimiteBancoCommand.cs` — MODIFICAR

**Lógica a adicionar (create):** Após `LimiteBanco.Criar`, antes de `repo.Add`: busca limite global vigente; se existir, verifica `somaLimitesModalidades + novoLimiteBrl <= limiteGlobal`. Caso contrário lança `InvalidOperationException`.

**Lógica a adicionar (update):** Mesma verificação usando `CalcularSomaLimitesModalidadesAsync(bancoId, excluirLimiteBancoId: cmd.LimiteId)` para excluir o registro atual da soma.

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~LimiteBanco"`

**Dependências:** T07, T06.

**Tamanho:** S (~30 linhas de adição em cada handler).

---

### T19 — Testes de invariantes cruzadas

**Arquivos (1):**
- `tests/Sgcf.Application.Tests/Cotacoes/LimiteGlobalBancoInvariantesCruzadasTests.cs` — NOVO

**Testes obrigatórios:**
- `CriarLimiteBanco_QuandoSomaExcedeLimiteGlobal_Bloqueia` (LG-09 create)
- `AtualizarLimiteBanco_QuandoNovaSomaExcedeLimiteGlobal_Bloqueia` (LG-09 update)
- `CriarLimiteBanco_QuandoNaoExisteLimiteGlobal_Permite` (no-op sem global)
- `CriarLimiteBanco_QuandoSomaNaoExcedeLimiteGlobal_Permite` (happy path)

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~LimiteGlobalBancoInvariantes"`

**Dependências:** T18.

**Tamanho:** S (~100 linhas).

---

### T20 — `ConsultaSaldoBancoServiceTests` (Testcontainers)

**Arquivos (1):**
- `tests/Sgcf.Application.Tests/Cotacoes/ConsultaSaldoBancoServiceTests.cs` — NOVO

**Testes obrigatórios:**
- `ConsultaSaldoBanco_RegimeA_CalculaSomaContratosAtivos`
- `ConsultaSaldoBanco_RegimeB_DetectaPerModalityComUmLimiteBanco`

`[Trait("Category", "Slow")]`.

**Verificação:** `dotnet test tests/Sgcf.Application.Tests/ --filter "FullyQualifiedName~ConsultaSaldoBancoService"`

**Dependências:** T07, T09.

**Tamanho:** M (~120 linhas).

---

### Checkpoint 5 — Final: todas as invariantes cobertas, suite completa verde

> `dotnet test --filter "Category!=Slow"` → 0
> `dotnet test` → 0
> Cobertura: Domain ≥ 95%, Application ≥ 85%, Infrastructure ≥ 70%
> Todas as 13 invariantes (LG-01..LG-13) exercidas por ao menos um teste automatizado

---

## Risks and Mitigations

| Risco | Prob | Impacto | Mitigação |
|---|---|---|---|
| `ConsultaSaldoBancoService.CalcularSaldoDevedorBancoAsync` usa predicado errado para contratos "ativos" | Média | Alto | Antes de implementar, ler o serviço de alertas de exposição existente para confirmar o predicado canônico de contrato ativo. |
| Migration S33 gera sintaxe errada para índice parcial no PostgreSQL | Baixa | Médio | Revisar SQL gerado antes de aplicar. Npgsql suporta `HasFilter(...)` → `CREATE UNIQUE INDEX ... WHERE ...` corretamente. Confirmar via `\d sgcf.limite_global_banco`. |
| `LimiteGlobalBancoHistorico` sem `TenantId` quando RLS é exigido na tabela | Baixa | Alto | Verificar se `LimiteBancoHistorico` implementa `ITenantScoped` — se não implementar, confirmar se o acesso à tabela historico é sempre via JOIN da tabela pai (herdando RLS indiretamente). Ajustar conforme o padrão confirmado. |
| Race condition em LG-04 (dois requests criam registros vigentes simultâneos) | Baixa | Médio | O índice único parcial no DB é a guarda definitiva; a verificação no handler é apenas UX. O DB bloqueará a segunda transação. |
| Adicionar `ILimiteGlobalBancoRepository` ao `CreateLimiteBancoCommandHandler` quebra testes existentes | Baixa | Médio | Testes existentes usam NSubstitute; basta adicionar dois `Substitute.For<>` no setup. Atualizar em T18/T19. |

---

## Parallelization Opportunities

**Após Checkpoint 1**, podem ser paralelizados:
- T05 + T10 (Configurations e DTOs não se dependem)
- T11, T12, T13, T14 (commands/queries diferentes; após T10 e T06 prontos)
- T18 (cross-cutting) pode ser desenvolvido em paralelo com T16 (API controller)

**Cadeias sequenciais obrigatórias:**
- T01 → T02 → T03 → T04
- T05 → T06 → T07 → T08 → T09
- T10 + T06 → T11 → T12 → T13
- T16 → T17
- T07 + T06 → T18 → T19
