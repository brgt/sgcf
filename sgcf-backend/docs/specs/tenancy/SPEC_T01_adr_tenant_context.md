# SPEC — Task −1.1 — ADR-020 + `ITenantContext`

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.1
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Dependências:** Nenhuma

---

## 1. Objetivo

Estabelecer o contrato e o registro arquitetural da multi-tenancy. Cria o ADR-020 que documenta a estratégia e a interface `ITenantContext` que será injetada em handlers, middlewares e jobs.

---

## 2. ADR-020 (conteúdo a publicar)

`docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md`:

### Cabeçalho

- **Status:** Aceito
- **Data:** 2026-05-20
- **Autor:** Time de arquitetura
- **Sponsor:** Welysson Soares
- **Substitui:** Nenhum
- **Substituído por:** —

### Contexto

O SGCF nasceu como instância única servindo apenas a Proxys. A estratégia comercial exige que o sistema suporte múltiplos clientes (SaaS) sem comprometer custo operacional nem segurança LGPD.

### Decisão

Adotar **Shared Schema + `tenant_id` + Row Level Security do PostgreSQL**:

- Uma instância PostgreSQL.
- Coluna `tenant_id UUID NOT NULL` em toda entidade operacional (~30 tabelas).
- Catálogos universais permanecem globais (PTAX, CDI, feriados, cadastro Compe de bancos).
- EF Core global query filter como **primeira camada** de isolação.
- Postgres RLS como **segunda camada** (rede de segurança).
- Identificadores de tenant: `Guid` PK + `Slug` kebab-case humano.
- Hierarquia de roles: `super-admin` (Nordware, cross-tenant) > `admin` (cliente, tenant-scoped) > demais roles operacionais existentes.

### Alternativas consideradas

| Alternativa | Por que não foi escolhida |
|-------------|---------------------------|
| DB por tenant | Custo operacional alto (backup, monitoring, migration por DB) para o estágio atual |
| Schema por tenant | Migrations 10x mais caras; complexidade EF Core; ganho marginal de isolação |
| Apenas EF filter, sem RLS | Sem rede de segurança em caso de bug em handler novo |
| Apenas RLS, sem EF filter | Quebra abstração; queries via LINQ ficariam menos previsíveis |

### Consequências

**Positivas:**

- Custo operacional baixo.
- Migration única por mudança de schema.
- Isolação suficiente para LGPD.
- Hot path em queries simples (sem JOIN entre schemas).

**Negativas:**

- Risco residual se filter for esquecido + RLS desabilitada (probabilidade baixa, mas existe).
- Migração inicial (big-bang) exige janela coordenada.
- Indexes ficam maiores (cardinalidade `tenant_id`).

**Neutras:**

- Todo handler que precisa de tenant precisa injetar `ITenantContext`.
- Toda migration nova de tabela operacional precisa habilitar RLS na criação.

---

## 3. Contrato `ITenantContext`

```csharp
namespace Sgcf.Application.Tenancy;

/// <summary>
/// Contexto de tenant resolvido por requisição HTTP ou iteração de job.
/// Lifetime: Scoped (uma instância por request).
/// </summary>
public interface ITenantContext
{
    /// <summary>UUID do tenant. Sempre populado dentro de um scope válido.</summary>
    Guid TenantId { get; }

    /// <summary>Slug humano. Útil para logs e auditoria.</summary>
    string TenantSlug { get; }

    /// <summary>True quando o usuário atual tem role super-admin (Nordware).</summary>
    bool IsSuperAdmin { get; }

    /// <summary>
    /// True quando o request está em modo de impersonação assistida:
    /// super-admin acessando dados de outro tenant via X-Tenant-Id.
    /// Auditoria precisa registrar com flag específica.
    /// </summary>
    bool IsImpersonating { get; }

    /// <summary>Indica se o contexto foi inicializado. Falso em escopos sem tenant resolvido.</summary>
    bool IsResolved { get; }
}
```

### Implementação concreta

```csharp
namespace Sgcf.Infrastructure.Tenancy;

internal sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;
    private string? _tenantSlug;

    public Guid TenantId => _tenantId
        ?? throw new MissingTenantContextException(
            "TenantContext acessado antes da resolução pelo middleware.");

    public string TenantSlug => _tenantSlug
        ?? throw new MissingTenantContextException(
            "TenantSlug acessado antes da resolução pelo middleware.");

    public bool IsSuperAdmin { get; private set; }
    public bool IsImpersonating { get; private set; }
    public bool IsResolved => _tenantId.HasValue;

    internal void Resolve(Guid tenantId, string slug, bool isSuperAdmin, bool isImpersonating)
    {
        if (_tenantId.HasValue)
        {
            throw new InvalidOperationException("TenantContext já resolvido neste scope.");
        }

        _tenantId = tenantId;
        _tenantSlug = slug;
        IsSuperAdmin = isSuperAdmin;
        IsImpersonating = isImpersonating;
    }
}
```

### Exceção

```csharp
namespace Sgcf.Application.Tenancy;

public sealed class MissingTenantContextException(string message) : InvalidOperationException(message);
```

---

## 4. Registro DI

`Program.cs`:

```csharp
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
```

Registra a implementação concreta separadamente para permitir injeção do `Resolve` no middleware (sem expor método mutável na interface).

---

## 5. Critérios de Aceite

- [ ] `docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md` publicado conforme estrutura §2.
- [ ] Interface `ITenantContext` em `Sgcf.Application.Tenancy/ITenantContext.cs`.
- [ ] Classe `TenantContext` em `Sgcf.Infrastructure.Tenancy/TenantContext.cs` com método `internal Resolve`.
- [ ] Exceção `MissingTenantContextException` em `Sgcf.Application.Tenancy/`.
- [ ] DI registrado em `Program.cs` como `Scoped`.
- [ ] Acesso a `TenantId` antes de `Resolve` lança `MissingTenantContextException`.

---

## 6. Verificação

```bash
dotnet build sgcf-backend.sln
dotnet test --filter "FullyQualifiedName~TenantContext"
```

**Teste-chave:**

```csharp
[Fact]
public void TenantContext_TenantId_sem_resolucao_lanca()
{
    var ctx = new TenantContext();
    Action act = () => _ = ctx.TenantId;
    act.Should().Throw<MissingTenantContextException>();
}

[Fact]
public void TenantContext_Resolve_duplicado_lanca()
{
    var ctx = new TenantContext();
    ctx.Resolve(Guid.NewGuid(), "tenant-a", false, false);

    Action act = () => ctx.Resolve(Guid.NewGuid(), "tenant-b", false, false);
    act.Should().Throw<InvalidOperationException>();
}
```

---

## 7. Boundaries específicas

### 7.1 Always do
- Lançar exceção explícita ao acessar `TenantId` sem resolução.
- Manter `Resolve` como `internal` na implementação concreta (não expor na interface).

### 7.2 Ask first
- Adicionar propriedade nova em `ITenantContext` (afeta todos os handlers).
- Tornar `Resolve` chamável de múltiplos pontos (atualmente só o middleware chama).

### 7.3 Never do
- Disponibilizar `Resolve` via interface pública.
- Permitir mudança de tenant dentro do mesmo scope após resolução.
- Cachear `ITenantContext` em campo estático ou DI Singleton.

---

## 8. Arquivos esperados

- `docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md`
- `src/Sgcf.Application/Tenancy/ITenantContext.cs`
- `src/Sgcf.Application/Tenancy/MissingTenantContextException.cs`
- `src/Sgcf.Infrastructure/Tenancy/TenantContext.cs`
- `src/Sgcf.Api/Program.cs` (registro DI)
- `tests/Sgcf.Application.Tests/Tenancy/TenantContextTests.cs`
