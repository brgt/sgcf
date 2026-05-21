# SPEC — Task −1.2 — Agregado `Tenant` + Endpoints Admin

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.2
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Task −1.1

---

## 1. Objetivo

Modelar o agregado `Tenant`, repositório, migration e os endpoints administrativos em `/api/v1/admin/tenants` (acessíveis apenas a `super-admin`). Adota padrão de mercado: `Guid` PK + `Slug` kebab-case humano.

---

## 2. Modelo de Domínio

### 2.1 Enums

```csharp
namespace Sgcf.Domain.Tenancy;

public enum StatusTenant : byte
{
    Ativo      = 1,
    Suspenso   = 2,
    Arquivado  = 3,
}

public enum PlanoAssinatura : byte
{
    Trial      = 1,
    Padrao     = 2,
    Premium    = 3,
    Enterprise = 4,
}
```

### 2.2 Agregado `Tenant`

```csharp
public sealed class Tenant : Entity, IAuditable
{
    // Id herdado de Entity (Guid)
    public string Slug { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public string CnpjMascarado { get; private set; } = default!;
    public StatusTenant Status { get; private set; }
    public PlanoAssinatura Plano { get; private set; }
    public Instant CriadoEm { get; private set; }
    public Instant? SuspensoEm { get; private set; }
    public Instant? ArquivadoEm { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private static readonly Regex SlugRegex =
        new("^[a-z][a-z0-9-]{2,30}$", RegexOptions.Compiled);

    private Tenant() { }

    public static Tenant Criar(
        Guid id, string slug, string nome, string cnpj,
        PlanoAssinatura plano, IClock clock)
    {
        if (id == Guid.Empty) throw new ArgumentException(nameof(id));
        if (!SlugRegex.IsMatch(slug ?? ""))
            throw new ArgumentException(
                "Slug deve ser kebab-case [a-z][a-z0-9-]{2,30}.", nameof(slug));
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException(nameof(nome));
        if (string.IsNullOrWhiteSpace(cnpj) || cnpj.Length < 14)
            throw new ArgumentException("CNPJ inválido.", nameof(cnpj));

        Instant agora = clock.GetCurrentInstant();
        return new Tenant
        {
            Id = id,
            Slug = slug.Trim().ToLowerInvariant(),
            Nome = nome.Trim(),
            CnpjMascarado = MascararCnpj(cnpj),
            Plano = plano,
            Status = StatusTenant.Ativo,
            CriadoEm = agora,
            UpdatedAt = agora,
        };
    }

    public void Suspender(string motivo, IClock clock)
    {
        if (Status == StatusTenant.Arquivado)
            throw new InvalidOperationException("Tenant arquivado não pode ser suspenso.");
        if (Status == StatusTenant.Suspenso) return;

        Status = StatusTenant.Suspenso;
        SuspensoEm = clock.GetCurrentInstant();
        UpdatedAt = SuspensoEm.Value;
    }

    public void Reativar(IClock clock)
    {
        if (Status == StatusTenant.Arquivado)
            throw new InvalidOperationException("Tenant arquivado não pode ser reativado.");
        if (Status == StatusTenant.Ativo) return;

        Status = StatusTenant.Ativo;
        SuspensoEm = null;
        UpdatedAt = clock.GetCurrentInstant();
    }

    public void Arquivar(IClock clock)
    {
        if (Status == StatusTenant.Arquivado) return;
        Status = StatusTenant.Arquivado;
        ArquivadoEm = clock.GetCurrentInstant();
        UpdatedAt = ArquivadoEm.Value;
    }

    public void AtualizarPlano(PlanoAssinatura novoPlano, IClock clock)
    {
        Plano = novoPlano;
        UpdatedAt = clock.GetCurrentInstant();
    }

    private static string MascararCnpj(string cnpj)
    {
        string digitos = new(cnpj.Where(char.IsDigit).ToArray());
        return digitos.Length >= 14
            ? $"{digitos[..2]}.***.***/****-{digitos[^2..]}"
            : "**.***.***/****-**";
    }
}
```

### 2.3 Geração de Id (UUID v7 preferencial)

```csharp
namespace Sgcf.Application.Tenancy;

public interface ITenantIdGenerator
{
    Guid NewId();
}

internal sealed class UuidV7TenantIdGenerator : ITenantIdGenerator
{
    public Guid NewId() => UuidNext.Uuid.NewSequential();
}
```

UUID v7 carrega timestamp embutido — útil para ordenação cronológica em listagens.

### 2.4 Repositório

```csharp
public interface ITenantRepository
{
    Task<Tenant?> GetAsync(Guid id, CancellationToken ct);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<Tenant?> GetByIdOrSlugAsync(string idOrSlug, CancellationToken ct);
    Task<PagedResult<Tenant>> ListAsync(StatusTenant? status, int page, int pageSize, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task AddAsync(Tenant tenant, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

---

## 3. Schema PostgreSQL

```sql
CREATE TABLE tenant (
    id              UUID PRIMARY KEY,
    slug            TEXT NOT NULL,
    nome            TEXT NOT NULL,
    cnpj_mascarado  TEXT NOT NULL,
    status          SMALLINT NOT NULL,
    plano           SMALLINT NOT NULL,
    criado_em       TIMESTAMPTZ NOT NULL,
    suspenso_em     TIMESTAMPTZ NULL,
    arquivado_em    TIMESTAMPTZ NULL,
    updated_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_tenant_slug UNIQUE (slug)
);

CREATE INDEX ix_tenant_status ON tenant (status) WHERE status <> 3; -- 3 = Arquivado
```

Tenant **não é tenant-scoped** (é a tabela raiz). Sem `tenant_id` em si mesma.

---

## 4. Endpoints

Controller: `TenantsController` em `/api/v1/admin/tenants` com `[Authorize(Policy = Policies.SuperAdmin)]` no nível de classe.

| Método | Path | Descrição |
|--------|------|-----------|
| GET | `/api/v1/admin/tenants` | Lista paginada com filtro opcional `?status=Ativo` |
| GET | `/api/v1/admin/tenants/{idOrSlug}` | Detalhe por UUID ou slug |
| POST | `/api/v1/admin/tenants` | Cria novo tenant |
| PATCH | `/api/v1/admin/tenants/{idOrSlug}` | Atualiza plano ou nome |
| POST | `/api/v1/admin/tenants/{idOrSlug}/suspender` | Suspende (idempotente) |
| POST | `/api/v1/admin/tenants/{idOrSlug}/reativar` | Reativa (idempotente) |
| POST | `/api/v1/admin/tenants/{idOrSlug}/arquivar` | Arquiva (irreversível, exige confirmação no body) |

### 4.1 DTOs

```csharp
public sealed record TenantDto(
    Guid Id,
    string Slug,
    string Nome,
    string CnpjMascarado,
    string Status,
    string Plano,
    Instant CriadoEm,
    Instant? SuspensoEm,
    Instant? ArquivadoEm);

public sealed record CreateTenantRequest(
    string Slug,
    string Nome,
    string Cnpj,
    string Plano);

public sealed record UpdateTenantRequest(
    string? Nome,
    string? Plano);

public sealed record SuspendTenantRequest(string Motivo);

public sealed record ArchiveTenantRequest(string Confirmacao); // deve ser igual ao slug
```

### 4.2 `POST /api/v1/admin/tenants`

**Body:**

```json
{
  "slug": "acme-finance",
  "nome": "ACME Finance Ltda",
  "cnpj": "00.000.000/0001-00",
  "plano": "Padrao"
}
```

**Response 201:** `TenantDto` no body + `Location: /api/v1/admin/tenants/{id}`.

**Erros:**

- 400 — slug inválido, CNPJ inválido, plano inexistente.
- 409 — slug já existe.

`Idempotency-Key` recomendado.

### 4.3 `POST /api/v1/admin/tenants/{idOrSlug}/suspender`

**Body:** `{"motivo": "Falta de pagamento"}`. Idempotente. 204 No Content.

### 4.4 `POST /api/v1/admin/tenants/{idOrSlug}/arquivar`

**Body:** `{"confirmacao": "<slug-do-tenant>"}`. Exige que o `confirmacao` bata com o slug para evitar ação acidental. 204 No Content. Não permite reativar depois.

---

## 5. Commands MediatR

```csharp
public sealed record CriarTenantCommand(string Slug, string Nome, string Cnpj, PlanoAssinatura Plano) : IRequest<Tenant>;
public sealed record AtualizarTenantCommand(Guid Id, string? Nome, PlanoAssinatura? Plano) : IRequest<Unit>;
public sealed record SuspenderTenantCommand(Guid Id, string Motivo) : IRequest<Unit>;
public sealed record ReativarTenantCommand(Guid Id) : IRequest<Unit>;
public sealed record ArquivarTenantCommand(Guid Id) : IRequest<Unit>;

public sealed record GetTenantQuery(string IdOrSlug) : IRequest<TenantDto?>;
public sealed record ListTenantsQuery(StatusTenant? Status, int Page, int PageSize) : IRequest<PagedResult<TenantDto>>;
```

---

## 6. AuditLog

Toda mutação em `Tenant` registra evento em `AuditLog` com:

- `Entity = "Tenant"`.
- `EntityId = tenant.Id`.
- `Operation = "Criar" | "Atualizar" | "Suspender" | "Reativar" | "Arquivar"`.
- `DiffJson` com campos alterados.
- `ActorSub = User.Identity.Name` (super-admin Nordware).

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Slug `Acme-Finance` (maiúscula) | Normalizado para `acme-finance` no agregado |
| Tentativa de suspender tenant arquivado | 409 Conflict |
| Tentativa de arquivar com confirmação errada | 400 Bad Request |
| `idOrSlug` ambíguo (slug parece UUID) | Resolver: tenta UUID primeiro, depois slug |
| `GET /admin/tenants/proxys` | Retorna o tenant `proxys` |
| Listagem sem filtro | Default `status=Ativo`; arquivados não aparecem salvo `?status=Arquivado` |
| `Plano` desconhecido no POST | 400 com lista de valores aceitos |
| Slug com hífen no início ou fim (`-acme`, `acme-`) | 400 (regex bloqueia) |
| Mesmo CNPJ em dois tenants | Permitido (cliente pode ter sub-empresas) — sem unique constraint em CNPJ |

---

## 8. Critérios de Aceite

- [ ] Enums `StatusTenant` e `PlanoAssinatura` criados.
- [ ] Agregado `Tenant` com fábrica `Criar` + transições `Suspender`, `Reativar`, `Arquivar`, `AtualizarPlano`.
- [ ] `ITenantRepository` + implementação EF Core.
- [ ] `ITenantIdGenerator` com `UuidV7TenantIdGenerator`.
- [ ] Migration `<ts>_AddTenants.cs` com unique constraint em `slug`.
- [ ] `TenantsController` em `/api/v1/admin/tenants` com 7 endpoints.
- [ ] Autorização `Policies.SuperAdmin` em todos os endpoints.
- [ ] Resolver `{idOrSlug}` aceita UUID e slug.
- [ ] `Slug` validado por regex no agregado.
- [ ] `CnpjMascarado` calculado automaticamente.
- [ ] AuditLog registra todas as mutações.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~Tenant"

# Smoke
curl -X POST http://localhost:5000/api/v1/admin/tenants \
  -H "Authorization: Bearer $SUPER_ADMIN_TOKEN" \
  -d '{"slug":"acme","nome":"ACME","cnpj":"00000000000100","plano":"Padrao"}'
```

**Testes-chave:**

```csharp
[Theory]
[InlineData("a")] // muito curto
[InlineData("ACME")] // maiúsculas
[InlineData("acme!")] // caractere inválido
[InlineData("-acme")] // começa com hífen
public void Criar_slug_invalido_lanca(string slug)
{
    Action act = () => Tenant.Criar(Guid.NewGuid(), slug, "ACME", "00000000000100", PlanoAssinatura.Padrao, _clock);
    act.Should().Throw<ArgumentException>();
}

[Fact]
public void Arquivado_nao_pode_ser_reativado()
{
    var tenant = TenantFixture.Ativo();
    tenant.Arquivar(_clock);

    Action act = () => tenant.Reativar(_clock);
    act.Should().Throw<InvalidOperationException>();
}

[Fact]
public async Task Post_slug_duplicado_retorna_409()
{
    await PostTenant("acme");
    var response = await PostTenant("acme");

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Normalizar slug para lowercase no agregado.
- Mascarar CNPJ ao persistir (LGPD).
- AuditLog em toda mutação.
- Exigir `Policies.SuperAdmin` em todos os endpoints `/admin/tenants`.

### 10.2 Ask first
- Adicionar campo `OwnerEmail` ou semelhante (PII) — discutir com Compliance.
- Permitir mudar `Slug` após criação (afeta URLs e logs).
- Adicionar limites por plano (max contratos, max usuários).

### 10.3 Never do
- Persistir CNPJ não mascarado.
- Permitir reverter arquivamento.
- Aceitar `tenant_id` no body do POST (gerar internamente via `ITenantIdGenerator`).
- Hard-delete tenant: arquivamento é o caminho.

---

## 11. Arquivos esperados

- `src/Sgcf.Domain/Tenancy/Tenant.cs`
- `src/Sgcf.Domain/Tenancy/StatusTenant.cs`
- `src/Sgcf.Domain/Tenancy/PlanoAssinatura.cs`
- `src/Sgcf.Application/Tenancy/ITenantRepository.cs`
- `src/Sgcf.Application/Tenancy/ITenantIdGenerator.cs`
- `src/Sgcf.Application/Tenancy/TenantDto.cs`
- `src/Sgcf.Application/Tenancy/Commands/*` (5 commands + handlers)
- `src/Sgcf.Application/Tenancy/Queries/*` (2 queries + handlers)
- `src/Sgcf.Infrastructure/Tenancy/UuidV7TenantIdGenerator.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Repositories/TenantRepository.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddTenants.cs`
- `src/Sgcf.Api/Controllers/TenantsController.cs`
- `tests/Sgcf.Domain.Tests/Tenancy/TenantTests.cs`
- `tests/Sgcf.Api.IntegrationTests/Tenancy/TenantsControllerTests.cs`
