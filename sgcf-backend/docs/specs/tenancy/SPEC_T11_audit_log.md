# SPEC — Task −1.11 — `AuditLog` Per-Tenant + Endpoint Admin Cross-Tenant

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.11
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Dependências:** Task −1.5

---

## 1. Objetivo

`AuditLog` recebe `TenantId NOT NULL` para isolar trilha de auditoria por cliente. Adicionar endpoint admin `GET /admin/auditoria` que aceita `tenantId` no query string para super-admin consultar logs de qualquer tenant. Endpoint normal `GET /auditoria` continua filtrado.

Registrar flag de impersonação quando super-admin atua em nome de outro tenant.

---

## 2. Mudanças no domínio

### 2.1 `AuditLog`

```csharp
namespace Sgcf.Domain.Auditoria;

public sealed class AuditLog : ITenantScoped
{
    public long Id { get; private set; }
    public Guid TenantId { get; private set; }       // NOVO
    public Instant OccurredAt { get; private set; }
    public string ActorSub { get; private set; } = string.Empty;
    public string ActorRole { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Entity { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string? DiffJson { get; private set; }
    public Guid RequestId { get; private set; }
    public byte[]? IpHash { get; private set; }
    public bool Impersonating { get; private set; } // NOVO
    public string? ImpersonatedBy { get; private set; } // NOVO — sub do super-admin

    public static AuditLog Create(
        Instant occurredAt,
        string actorSub,
        string actorRole,
        string source,
        string entity,
        Guid? entityId,
        string operation,
        string? diffJson,
        Guid requestId,
        byte[]? ipHash,
        bool impersonating,
        string? impersonatedBy)
    {
        return new AuditLog
        {
            OccurredAt = occurredAt,
            ActorSub = actorSub,
            ActorRole = actorRole,
            Source = source,
            Entity = entity,
            EntityId = entityId,
            Operation = operation,
            DiffJson = diffJson,
            RequestId = requestId,
            IpHash = ipHash,
            Impersonating = impersonating,
            ImpersonatedBy = impersonatedBy,
            // TenantId populado pelo TenantSaveInterceptor
        };
    }
}
```

### 2.2 Quem popula `TenantId` e `Impersonating`

`AuditLogWriter` (serviço de aplicação que registra eventos) lê `ITenantContext`:

```csharp
namespace Sgcf.Application.Auditoria;

public interface IAuditLogWriter
{
    Task WriteAsync(
        string entity, Guid? entityId, string operation,
        object? diff, CancellationToken ct);
}

internal sealed class AuditLogWriter(
    SgcfDbContext db,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContext,
    IClock clock) : IAuditLogWriter
{
    public async Task WriteAsync(
        string entity, Guid? entityId, string operation,
        object? diff, CancellationToken ct)
    {
        HttpContext? ctx = httpContext.HttpContext;
        string actorSub = ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        string actorRole = string.Join(",", ctx?.User.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? []);
        Guid requestId = ctx?.TraceIdentifier is { } trace && Guid.TryParse(trace, out var g) ? g : Guid.NewGuid();
        string? impersonatedBy = tenantContext.IsImpersonating ? actorSub : null;

        AuditLog log = AuditLog.Create(
            occurredAt: clock.GetCurrentInstant(),
            actorSub: actorSub,
            actorRole: actorRole,
            source: ctx?.Request.Path ?? "system",
            entity: entity,
            entityId: entityId,
            operation: operation,
            diffJson: diff is null ? null : JsonSerializer.Serialize(diff),
            requestId: requestId,
            ipHash: HashIp(ctx),
            impersonating: tenantContext.IsImpersonating,
            impersonatedBy: impersonatedBy);

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}
```

---

## 3. Schema

`tenant_id` já adicionado na Task −1.5. Esta task adiciona apenas duas colunas novas:

```sql
ALTER TABLE sgcf.audit_log
    ADD COLUMN impersonating BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN impersonated_by TEXT NULL;
```

Migration:

```csharp
public partial class AuditLogImpersonation : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.AddColumn<bool>(
            name: "impersonating",
            schema: "sgcf",
            table: "audit_log",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        mb.AddColumn<string>(
            name: "impersonated_by",
            schema: "sgcf",
            table: "audit_log",
            type: "text",
            nullable: true);

        // Backfill explícito para clareza
        mb.Sql("UPDATE sgcf.audit_log SET impersonating = FALSE WHERE impersonating IS NULL;");
    }
}
```

---

## 4. Endpoints

### 4.1 `AuditoriaController` (per-tenant — existente)

```
GET /api/v1/auditoria?entity=&entityId=&from=&to=&page=&pageSize=
```

Sem mudança de contrato. EF filter aplica `tenant_id` automaticamente.

### 4.2 `AuditoriaAdminController` (novo)

```
GET /api/v1/admin/auditoria
```

**Auth:** `Policies.SuperAdmin`.

**Query params:**

| Param | Tipo | Obrigatório |
|-------|------|-------------|
| `tenantId` | UUID | sim |
| `entity` | string | não |
| `entityId` | UUID | não |
| `from`, `to` | date | não |
| `impersonating` | bool | não — filtra apenas operações de impersonação |
| `page`, `pageSize` | int | não |

**Response 200:** `PagedResult<AuditLogDto>` filtrado pelo `tenantId` informado.

Implementação usa `WithTenantBypass`:

```csharp
[HttpGet]
[Authorize(Policy = Policies.SuperAdmin)]
public async Task<IActionResult> List(
    [FromQuery] Guid tenantId,
    [FromQuery] string? entity,
    /* ... */,
    CancellationToken ct)
{
    var query = _db.AuditLogs
        .WithTenantBypass()
        .Where(a => a.TenantId == tenantId);

    if (!string.IsNullOrWhiteSpace(entity))
        query = query.Where(a => a.Entity == entity);

    // ... outros filtros

    var page = await query.OrderByDescending(a => a.OccurredAt).PaginateAsync(/* ... */);
    return Ok(page);
}
```

### 4.3 Visibilidade de impersonação ao admin do tenant

**Decisão sponsor 2026-05-20:** transparência total — impersonação é **sempre** visível ao admin do tenant. Não há flag, configuração ou caminho para ocultar.

Justificativa:

- **LGPD:** princípio da transparência ao titular dos dados.
- **Trust:** cliente sabe exatamente o que o suporte Nordware fez em sua conta.
- **Anti-abuso interno:** super-admin sabe que toda ação é rastreável publicamente, reduzindo risco moral.
- **Auditoria externa:** logs ficam disponíveis para investigação de qualquer parte.

Implementação:

- `GET /auditoria?impersonating=true` retorna eventos sem qualquer filtro de privacidade adicional.
- `AuditLog.ImpersonatedBy` (sub do super-admin) é exposto no DTO sem mascaramento.
- Endpoint normal `GET /auditoria` sem filtro também inclui eventos de impersonação (são apenas eventos comuns com flag).

```csharp
[HttpGet]
[Authorize(Policy = Policies.Leitura)]
public async Task<IActionResult> List(
    [FromQuery] bool? impersonating,
    /* ... */)
{
    var query = _db.AuditLogs.AsQueryable(); // filter aplica tenant_id

    if (impersonating == true)
        query = query.Where(a => a.Impersonating);

    /* ... */
}
```

---

## 5. DTOs

```csharp
public sealed record AuditLogDto(
    long Id,
    Instant OccurredAt,
    string ActorSub,
    string ActorRole,
    string Source,
    string Entity,
    Guid? EntityId,
    string Operation,
    string? DiffJson,
    Guid RequestId,
    bool Impersonating,
    string? ImpersonatedBy);
```

`IpHash` **não exposta** no DTO (LGPD).

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| `GET /admin/auditoria` sem `tenantId` | 400 (`tenantId` obrigatório) |
| `GET /admin/auditoria?tenantId=<nao-existe>` | 200 com lista vazia |
| Operação super-admin em modo normal (sem `X-Tenant-Id`) | `Impersonating = false` |
| Operação super-admin com `X-Tenant-Id` | `Impersonating = true`, `ImpersonatedBy = sub-do-super-admin` |
| Background job (sem HTTP) | `Impersonating = false`; `ActorSub = "system"` |
| `GET /auditoria?impersonating=true` no tenant que nunca foi impersonado | Lista vazia |
| Cleanup de logs antigos | Tabela cresce muito; processo de archive separado (não escopo desta task) |

---

## 7. Critérios de Aceite

- [ ] `AuditLog` implementa `ITenantScoped` + propriedades `Impersonating`, `ImpersonatedBy`.
- [ ] `IAuditLogWriter` lê `ITenantContext` e popula campos.
- [ ] Migration adiciona `impersonating` e `impersonated_by`.
- [ ] `AuditoriaController` continua isolado por tenant.
- [ ] `AuditoriaAdminController` em `/admin/auditoria` (super-admin).
- [ ] `?impersonating=true` filtra eventos de impersonação.
- [ ] `IpHash` não exposta no DTO.
- [ ] Teste cross-tenant cobre isolação.

---

## 8. Verificação

```bash
dotnet test --filter "FullyQualifiedName~AuditLog"
```

**Teste-chave:**

```csharp
[Fact]
public async Task SuperAdmin_impersonando_registra_audit_com_flag()
{
    Guid tenantB = await CriarTenant("beta");

    using var client = _fx.SuperAdminClient(impersonateTenant: tenantB);
    await client.PatchAsJsonAsync("/api/v1/contratos/{id}", new { observacoes = "fix" });

    var audits = await _fx.GetAuditLogs(tenantB);
    audits.Should().ContainSingle(a => a.Entity == "Contrato"
        && a.Impersonating
        && a.ImpersonatedBy == _superAdminSub);
}

[Fact]
public async Task Admin_do_tenant_ve_impersonacao_que_aconteceu_no_seu_tenant()
{
    // Setup: super-admin já fez ação no tenant A
    using var client = _fx.ClientFor(_tenantA);
    var response = await client.GetAsync("/api/v1/auditoria?impersonating=true");

    var page = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
    page!.Items.Should().NotBeEmpty();
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Setar `Impersonating = true` automaticamente via `ITenantContext.IsImpersonating`.
- Expor visibilidade de impersonação ao admin do tenant (LGPD).
- Mascarar `IpHash` no DTO.

### 9.2 Ask first
- Permitir delete de `AuditLog` (não permitido por design — apenas archive externo).
- Adicionar índice em campo nuevo de filtro.

### 9.3 Never do
- Aceitar `tenantId` no body de endpoints normais (apenas no admin).
- Permitir admin do cliente desabilitar visibilidade de impersonação (transparência obrigatória).
- Persistir `AuditLog` sem `ITenantContext` (lança via `TenantSaveInterceptor`).

---

## 10. Arquivos esperados

- `src/Sgcf.Domain/Auditoria/AuditLog.cs` (refatorar)
- `src/Sgcf.Application/Auditoria/IAuditLogWriter.cs`
- `src/Sgcf.Application/Auditoria/AuditLogWriter.cs`
- `src/Sgcf.Application/Auditoria/AuditLogDto.cs`
- `src/Sgcf.Api/Controllers/AuditoriaAdminController.cs` (novo)
- `src/Sgcf.Api/Controllers/AuditoriaController.cs` (atualizar — filtro `impersonating`)
- `src/Sgcf.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs` (atualizar)
- `src/Sgcf.Infrastructure/Migrations/<ts>_AuditLogImpersonation.cs`
- `tests/Sgcf.Application.Tests/Auditoria/AuditLogWriterTests.cs`
- `tests/Sgcf.Api.IntegrationTests/Tenancy/AuditoriaAdminControllerTests.cs`
