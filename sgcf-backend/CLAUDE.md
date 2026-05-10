# SGCF Backend — Claude Agent Instructions

Sistema de Gestão de Contratos de Financiamento (.NET 11)

## Project Architecture

```
sgcf-backend/
├── src/
│   ├── Sgcf.Domain          # Pure domain: entities, value objects, domain services
│   ├── Sgcf.Application     # Use cases: Commands, Queries, Handlers (MediatR)
│   ├── Sgcf.Infrastructure  # Adapters: EF Core / PostgreSQL, Redis, external APIs
│   ├── Sgcf.Api             # HTTP entry point (ASP.NET Core Web API)
│   ├── Sgcf.Mcp             # MCP protocol adapter (thin, depends on Application only)
│   ├── Sgcf.A2a             # Agent-to-Agent adapter (thin, depends on Application only)
│   └── Sgcf.Jobs            # Background jobs and scheduled tasks
└── tests/
    ├── Sgcf.Domain.Tests           # Unit tests (xUnit + FsCheck + FluentAssertions)
    ├── Sgcf.Application.Tests      # Application layer tests (Testcontainers)
    ├── Sgcf.Api.IntegrationTests   # End-to-end HTTP tests (WebApplicationFactory)
    ├── Sgcf.Mcp.Tests              # MCP adapter unit tests
    └── Sgcf.GoldenDataset          # Golden dataset regression tests (JSON-driven)
```

## Naming Conventions

- Domain concept names in **Portuguese**: `Contrato`, `Banco`, `Parcela`, `Mutuario`, `Moeda`, `TaxaJuros`
- Technical/infrastructure names in **English**: `Repository`, `Handler`, `Controller`, `Factory`, `Mapper`
- Namespace pattern: `Sgcf.Domain.Contratos`, `Sgcf.Application.Contratos.Commands`, `Sgcf.Infrastructure.Persistence`

## Non-Negotiable Rules

### 1. Money — Never Use Raw `decimal`

**WRONG:**
```csharp
public decimal ValorPrincipal { get; set; }
decimal total = parcela1 + parcela2; // compile error is intentional
```

**CORRECT:**
```csharp
public Money ValorPrincipal { get; init; }
Money total = parcela1.Valor + parcela2.Valor;
```

The `Money` value object lives in `Sgcf.Domain.Financeiro.Money`.
It encapsulates the amount (`decimal`) and the currency (`Moeda`).
It enforces rounding on every arithmetic operation.

### 2. Dates — Never Use `DateTime.Now` in Domain

**WRONG:**
```csharp
var hoje = DateTime.Now;           // forbidden
var hoje = DateTimeOffset.UtcNow;  // also forbidden in domain
```

**CORRECT:**
```csharp
// For calendar dates (no time component needed):
LocalDate hoje = clock.GetCurrentInstant().InZone(fusoHorarioBrasilia).Date;

// For point-in-time (audit, event sourcing):
Instant agora = clock.GetCurrentInstant();
```

Use NodaTime types throughout the domain:
- `LocalDate` — calendar date (vencimento, emissao, competencia)
- `Instant` — UTC point in time (created_at, updated_at)
- `Period` — date-based duration (prazo em meses)
- `Duration` — time-based duration (timeouts)
- `DateTimeZone` — always `DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]` for BR dates

Never call `DateTime.Now`, `DateTime.UtcNow`, or `DateTimeOffset.UtcNow` inside domain or application layers.
Inject `IClock` (NodaTime) to allow deterministic testing.

### 3. Financial Calculations — Pure Functions Only

All financial formula implementations must be:
- **Pure**: same inputs always produce same outputs
- **No I/O**: no database calls, no HTTP, no logging inside calculation
- **No side effects**: do not mutate state; return new value objects

**CORRECT (pure domain service):**
```csharp
public static class CalculadoraAmortizacao
{
    public static TabelaPrice CalcularPrice(
        Money valorFinanciado,
        TaxaJuros taxaMensal,
        int prazoMeses) { ... }

    public static TabelaSac CalcularSac(
        Money valorFinanciado,
        TaxaJuros taxaMensal,
        int prazoMeses) { ... }
}
```

**WRONG:**
```csharp
public async Task<Parcela[]> CalcularAsync(Guid contratoId) // NO — reads DB
{
    var contrato = await _repo.GetAsync(contratoId); // forbidden in calc
    ...
}
```

### 4. Rounding — Always `MidpointRounding.AwayFromZero` (HalfUp)

Brazilian financial regulations require "arredondamento comercial" (HalfUp).

**CORRECT:**
```csharp
decimal rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
```

The `Money` value object enforces this automatically on all arithmetic.
Never use `Math.Round(value, 2)` (which defaults to `ToEven` / banker's rounding).

### 5. Layer Dependency Rules

```
Domain       ← no dependencies
Application  ← Domain only
Infrastructure ← Domain + Application
Api          ← Application + Infrastructure
Mcp / A2a    ← Application only (NEVER Infrastructure directly)
Jobs         ← Application + Infrastructure
Tests        ← target project only (+ Infrastructure for integration tests)
```

If you find yourself importing `Sgcf.Infrastructure` from `Sgcf.Mcp` or `Sgcf.A2a`, stop — you are breaking the architecture.

### 6. Entity Framework — Infrastructure Only

EF Core `DbContext`, migrations, and entity configurations belong exclusively in `Sgcf.Infrastructure`.
The domain layer must have zero EF attributes or navigation property conventions that depend on EF.

Use **owned entities** for value objects and **private setters** for domain properties.

## Testing Workflow

```bash
# Fast feedback loop (before every commit):
dotnet test --filter "Category!=Slow"

# Full suite (CI, before merge):
dotnet test

# With coverage:
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Golden dataset only:
dotnet test tests/Sgcf.GoldenDataset/Sgcf.GoldenDataset.csproj
```

Mark tests that spin up Docker containers with `[Trait("Category", "Slow")]`.

## Golden Dataset Tests

Files in `tests/Sgcf.GoldenDataset/data/` are authoritative reference cases.
Each JSON file contains `input` and `expectedOutput` for a specific scenario
(e.g., Price amortization table for a 12-month loan at 1.5% a.m.).

**Do not change expected outputs** without business sign-off. If a calculation
changes and breaks a golden test, open a PR explaining why the formula changed.

## Local Development

```bash
# Start PostgreSQL 16 + Redis 7:
docker compose -f infra/dev/docker-compose.yml up -d

# Apply EF migrations:
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Run the API:
dotnet run --project src/Sgcf.Api

# Run all tests fast:
dotnet test --filter "Category!=Slow"
```

Connection string for local dev:
```
Host=localhost;Database=sgcf_dev;Username=sgcf;Password=sgcf_dev_password
```

## Key Domain Concepts (Glossary)

| Portuguese         | English equivalent         | Notes |
|--------------------|----------------------------|-------|
| Contrato           | FinancingContract          | Aggregate root |
| Parcela            | Installment                | Value object within Contrato |
| Banco              | Bank / Lender              | External entity |
| Mutuario           | Borrower                   | |
| TaxaJuros          | InterestRate               | Value object; stores rate + basis (a.m., a.a.) |
| Moeda              | Currency                   | BRL, USD, etc. |
| Money              | Money                      | Amount + Moeda value object |
| TabelaPrice        | Price amortization table   | Equal installments |
| TabelaSac          | SAC amortization table     | Decreasing installments |
| VencimentoParcela  | InstallmentDueDate         | |
| IOF                | IOF (tax on financial ops) | Brazilian tax |
| CET                | TotalEffectiveCost         | Custo Efetivo Total |
