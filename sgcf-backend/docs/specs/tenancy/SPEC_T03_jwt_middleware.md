# SPEC — Task −1.3 — JWT com `tenant_id` + Role `super-admin` + Middleware Resolver

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.3
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Tasks −1.1, −1.2

---

## 1. Objetivo

Inserir o `tenant_id` no JWT, adicionar role `super-admin`, criar `Policies.SuperAdmin` e implementar `TenantResolverMiddleware` que popula `TenantContext` em cada request. Atualizar dev mock para criar tenant `proxys` no boot.

---

## 2. Claims do JWT

| Claim | Tipo | Obrigatório | Observação |
|-------|------|-------------|------------|
| `sub` | string | sim | já existente |
| `name` | string | sim | já existente |
| `roles` | array | sim | inclui `super-admin` para operadores Nordware |
| `tenant_id` | string (UUID) | sim (exceto `/admin/*`) | novo — identifica tenant ativo |

**Nota:** `tenant_id` é opcional para tokens emitidos para `super-admin` que precisam acessar `/admin/*`. Quando ausente e o endpoint é não-admin, middleware retorna 401.

---

## 3. Policies

`src/Sgcf.Application/Authorization/Policies.cs`:

```csharp
namespace Sgcf.Application.Authorization;

public static class Policies
{
    public const string Leitura    = "Leitura";
    public const string Escrita    = "Escrita";
    public const string Gerencial  = "Gerencial";
    public const string Executivo  = "Executivo";
    public const string Auditoria  = "Auditoria";
    public const string Admin      = "Admin";       // existente — escopo do tenant
    public const string SuperAdmin = "SuperAdmin";  // NOVO — cross-tenant (Nordware)
}
```

Registro em `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.Leitura,    p => p.RequireAuthenticatedUser());
    options.AddPolicy(Policies.Escrita,    p => p.RequireRole("tesouraria", "admin"));
    options.AddPolicy(Policies.Gerencial,  p => p.RequireRole("gerente", "diretor", "admin"));
    options.AddPolicy(Policies.Executivo,  p => p.RequireRole("tesouraria", "gerente", "diretor", "admin"));
    options.AddPolicy(Policies.Auditoria,  p => p.RequireRole("contabilidade", "auditor", "admin"));
    options.AddPolicy(Policies.Admin,      p => p.RequireRole("admin"));
    options.AddPolicy(Policies.SuperAdmin, p => p.RequireRole("super-admin")); // NOVO
});
```

---

## 4. `TenantResolverMiddleware`

```csharp
namespace Sgcf.Api.Middleware;

public sealed class TenantResolverMiddleware(
    RequestDelegate next,
    ILogger<TenantResolverMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext ctx,
        TenantContext tenantContext,
        ITenantCache cache)
    {
        // Endpoints sem necessidade de tenant: /admin/*, /health/*, /swagger
        if (PathIsAdminOnly(ctx.Request.Path) || PathIsAnonymous(ctx.Request.Path))
        {
            await next(ctx);
            return;
        }

        // Usuário precisa estar autenticado a esta altura (Authorization middleware antes)
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            await next(ctx); // 401 será emitido por outro middleware
            return;
        }

        bool isSuperAdmin = ctx.User.IsInRole("super-admin");
        Guid? tenantIdFromClaim = ExtractTenantClaim(ctx.User);
        Guid? tenantIdFromHeader = ExtractTenantHeader(ctx.Request);

        Guid? resolvedTenantId;
        bool isImpersonating = false;

        if (isSuperAdmin && tenantIdFromHeader.HasValue)
        {
            // Impersonação assistida: super-admin via X-Tenant-Id
            resolvedTenantId = tenantIdFromHeader;
            isImpersonating = true;
        }
        else if (tenantIdFromClaim.HasValue)
        {
            resolvedTenantId = tenantIdFromClaim;
        }
        else
        {
            await EmitirErroAsync(ctx, 401, "tenant_id ausente no token.");
            return;
        }

        TenantInfo? tenant = await cache.GetByIdAsync(resolvedTenantId.Value, ctx.RequestAborted);
        if (tenant is null)
        {
            await EmitirErroAsync(ctx, 401, "Tenant não encontrado.");
            return;
        }

        if (tenant.Status == StatusTenant.Suspenso)
        {
            await EmitirErroAsync(ctx, 403, "Tenant suspenso.");
            return;
        }

        if (tenant.Status == StatusTenant.Arquivado)
        {
            await EmitirErroAsync(ctx, 403, "Tenant arquivado.");
            return;
        }

        tenantContext.Resolve(tenant.Id, tenant.Slug, isSuperAdmin, isImpersonating);

        // Disponibilizar em logs estruturados
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = tenant.Id,
            ["TenantSlug"] = tenant.Slug,
            ["Impersonating"] = isImpersonating,
        }))
        {
            await next(ctx);
        }
    }

    // helpers privados...
}
```

### 4.1 Registro

`Program.cs`, **depois de `UseAuthentication`** e **antes de `UseAuthorization`**:

```csharp
app.UseAuthentication();
app.UseMiddleware<TenantResolverMiddleware>();
app.UseAuthorization();
```

### 4.2 Cache de tenants com invalidação via Redis pub/sub

**Decisão sponsor 2026-05-20:** Redis pub/sub para invalidação cross-instância, com `IMemoryCache` local e TTL de 5 min como rede de segurança contra perda de mensagem.

**Por que Redis pub/sub:**

- Redis já é dependência (cache de cotações spot).
- Latência sub-segundo na invalidação — crítico para suspensão de tenants comprometidos.
- Sem novo componente operacional.
- TTL local de 5 min cobre raras perdas de mensagem.

```csharp
namespace Sgcf.Infrastructure.Tenancy;

public interface ITenantCache
{
    Task<TenantInfo?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<TenantInfo?> GetBySlugAsync(string slug, CancellationToken ct);
    Task InvalidateAsync(Guid id, CancellationToken ct);
}

public sealed record TenantInfo(Guid Id, string Slug, StatusTenant Status);

internal sealed class TenantCache(
    IMemoryCache memCache,
    IConnectionMultiplexer redis,
    ITenantRepository repo,
    ILogger<TenantCache> logger) : ITenantCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    public const string InvalidationChannel = "sgcf:tenant:invalidate";

    public async Task<TenantInfo?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        string key = $"tenant:id:{id}";
        if (memCache.TryGetValue(key, out TenantInfo? cached))
            return cached;

        Tenant? tenant = await repo.GetAsync(id, ct);
        if (tenant is null) return null;

        TenantInfo info = new(tenant.Id, tenant.Slug, tenant.Status);
        memCache.Set(key, info, Ttl);
        memCache.Set($"tenant:slug:{tenant.Slug}", info, Ttl);
        return info;
    }

    public async Task InvalidateAsync(Guid id, CancellationToken ct)
    {
        // 1. Invalida local imediatamente
        InvalidateLocal(id);

        // 2. Publica mensagem para outras instâncias
        ISubscriber sub = redis.GetSubscriber();
        await sub.PublishAsync(
            RedisChannel.Literal(InvalidationChannel),
            id.ToString());

        logger.LogInformation("Tenant {TenantId} invalidado (local + pub/sub).", id);
    }

    internal void InvalidateLocal(Guid id)
    {
        // Buscar slug para limpar chave secundária
        if (memCache.TryGetValue($"tenant:id:{id}", out TenantInfo? info) && info is not null)
        {
            memCache.Remove($"tenant:slug:{info.Slug}");
        }
        memCache.Remove($"tenant:id:{id}");
    }
}
```

### 4.3 Subscriber Redis (hosted service)

```csharp
namespace Sgcf.Infrastructure.Tenancy;

internal sealed class TenantCacheInvalidationSubscriber(
    IConnectionMultiplexer redis,
    IServiceProvider sp,
    ILogger<TenantCacheInvalidationSubscriber> logger)
    : IHostedService
{
    private ChannelMessageQueue? _queue;

    public async Task StartAsync(CancellationToken ct)
    {
        ISubscriber sub = redis.GetSubscriber();
        _queue = await sub.SubscribeAsync(
            RedisChannel.Literal(TenantCache.InvalidationChannel));

        _queue.OnMessage(msg =>
        {
            try
            {
                if (Guid.TryParse(msg.Message.ToString(), out Guid tenantId))
                {
                    using IServiceScope scope = sp.CreateScope();
                    var cache = (TenantCache)scope.ServiceProvider.GetRequiredService<ITenantCache>();
                    cache.InvalidateLocal(tenantId);
                    logger.LogDebug("Cache invalidado via pub/sub para tenant {TenantId}.", tenantId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar mensagem de invalidação.");
            }
        });
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_queue is not null) await _queue.UnsubscribeAsync();
    }
}
```

### 4.4 Registro DI

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));

builder.Services.AddScoped<ITenantCache, TenantCache>();
builder.Services.AddHostedService<TenantCacheInvalidationSubscriber>();
```

**Confiabilidade:**

- Se mensagem se perde (Redis fica indisponível brevemente), o TTL de 5 min garante que cache local fique stale por no máximo 5 min.
- `IConnectionMultiplexer` reconecta automaticamente após falha.
- Métricas: `sgcf_tenant_cache_invalidation_received_total`, `sgcf_tenant_cache_invalidation_published_total`.

`TenantsController` chama `cache.InvalidateAsync(id)` ao mudar status do tenant — propaga para todas as instâncias.

---

## 5. Dev Mock

Atualizar `Program.cs` Development:

```csharp
if (builder.Environment.IsDevelopment())
{
    // Seed automático: garante tenant "proxys" no boot
    builder.Services.AddHostedService<DevTenantSeederService>();

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            if (ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                bool superAdminRequested =
                    ctx.Request.Headers["X-Dev-Role"].ToString() == "super-admin";

                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, "dev-user"),
                    new(ClaimTypes.NameIdentifier, "dev-user-id"),
                    new(ClaimTypes.Role, "admin"),
                    new(ClaimTypes.Role, "tesouraria"),
                    new(ClaimTypes.Role, "gerente"),
                    new(ClaimTypes.Role, "diretor"),
                    new(ClaimTypes.Role, "contabilidade"),
                    new(ClaimTypes.Role, "auditor"),
                    new("tenant_id", DevTenantSeederService.ProxysTenantId.ToString()),
                };

                if (superAdminRequested)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "super-admin"));
                }

                ctx.Principal = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "DevMock"));
                ctx.Success();
            }
            return Task.CompletedTask;
        },
    };
}
```

### 5.1 `DevTenantSeederService`

Garante que o tenant `proxys` exista no boot (Dev):

```csharp
internal sealed class DevTenantSeederService(IServiceProvider sp) : IHostedService
{
    public static readonly Guid ProxysTenantId =
        new("00000000-0000-7000-8000-000000000001"); // UUID v7 fixo dev

    public async Task StartAsync(CancellationToken ct)
    {
        using IServiceScope scope = sp.CreateScope();
        ITenantRepository repo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        Tenant? existing = await repo.GetAsync(ProxysTenantId, ct);
        if (existing is not null) return;

        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();
        Tenant tenant = Tenant.Criar(
            ProxysTenantId, "proxys", "Proxys Comércio Eletrônico",
            "00000000000100", PlanoAssinatura.Padrao, clock);

        await repo.AddAsync(tenant, ct);
        await repo.SaveChangesAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

## 6. Path detection

```csharp
private static bool PathIsAdminOnly(PathString path) =>
    path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase);

private static bool PathIsAnonymous(PathString path) =>
    path.StartsWithSegments("/health") ||
    path.StartsWithSegments("/swagger");
```

`/health/rls` (Task −1.12) é também protegido por `Policies.SuperAdmin`, mas o middleware de tenant não resolve tenant para ele (super-admin não precisa).

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Token sem `tenant_id` em endpoint não-admin | 401 |
| Token com `tenant_id` apontando para tenant inexistente | 401 |
| Token com tenant suspenso | 403 |
| Token com tenant arquivado | 403 |
| `super-admin` sem `tenant_id` chamando `/contratos` | 401 (precisa header `X-Tenant-Id` para impersonar) |
| `super-admin` com `tenant_id` próprio (Proxys) e sem header | Funciona como tenant Proxys |
| Usuário comum com header `X-Tenant-Id` | Header ignorado (não é super-admin) |
| Status do tenant muda enquanto request em vôo | Próximo request reflete; request atual usa cache |

---

## 8. Critérios de Aceite

- [ ] `Policies.SuperAdmin` adicionada e registrada.
- [ ] `TenantResolverMiddleware` registrado entre `Authentication` e `Authorization`.
- [ ] `ITenantCache` com TTL 5 min implementado.
- [ ] `TenantsController` chama `cache.InvalidateAsync` ao mudar status.
- [ ] JWT dev mock injeta claim `tenant_id` automaticamente.
- [ ] Header `X-Dev-Role: super-admin` ativa role na sessão dev.
- [ ] `DevTenantSeederService` cria `proxys` no boot Dev.
- [ ] Endpoints `/admin/*` ignoram resolver de tenant.
- [ ] Endpoints `/health/*` e `/swagger` ignoram autenticação.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~TenantResolverMiddleware"
dotnet test --filter "FullyQualifiedName~TenantCache"

# Smoke
curl http://localhost:5000/api/v1/contratos
# → 401 sem token

curl http://localhost:5000/api/v1/contratos -H "Authorization: Bearer dev"
# → 200 (dev mock injeta tenant proxys)

curl http://localhost:5000/api/v1/admin/tenants -H "Authorization: Bearer dev"
# → 403 sem header super-admin

curl http://localhost:5000/api/v1/admin/tenants \
  -H "Authorization: Bearer dev" \
  -H "X-Dev-Role: super-admin"
# → 200
```

**Teste-chave:**

```csharp
[Fact]
public async Task Request_sem_tenant_id_retorna_401()
{
    using var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", _tokenSemTenantId);

    var response = await client.GetAsync("/api/v1/contratos");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task Request_com_tenant_suspenso_retorna_403()
{
    await SuspenderTenant(_proxysId);
    using var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", TokenParaTenant(_proxysId));

    var response = await client.GetAsync("/api/v1/contratos");

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task SuperAdmin_com_X_Tenant_Id_impersona_outro_tenant()
{
    using var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", _tokenSuperAdmin);
    client.DefaultRequestHeaders.Add("X-Tenant-Id", _acmeId.ToString());

    var response = await client.GetAsync("/api/v1/contratos");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // futuro: verificar que retornou contratos do ACME, não do Proxys
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Invalidar cache quando status do tenant muda.
- Logar `TenantId` e `Impersonating` em todo request resolvido.
- Aceitar `X-Tenant-Id` apenas se `super-admin`.

### 10.2 Ask first
- Mudar TTL do cache (afeta consistência cross-instância).
- Aceitar `tenant_id` via query param em algum endpoint específico.
- Permitir token sem `tenant_id` em endpoint não-admin.

### 10.3 Never do
- Aceitar `X-Tenant-Id` de usuário não-super-admin (IDOR).
- Cachear status `Suspenso` ou `Arquivado` por muito tempo (TTL ≤ 5 min).
- Resolver tenant em endpoints `/admin/*` (são cross-tenant por natureza).
- Ler `tenant_id` do body ou query em endpoint operacional.

---

## 11. Arquivos esperados

- `src/Sgcf.Application/Authorization/Policies.cs` (atualizar)
- `src/Sgcf.Api/Program.cs` (registrar policy + middleware + dev mock)
- `src/Sgcf.Api/Middleware/TenantResolverMiddleware.cs`
- `src/Sgcf.Api/HostedServices/DevTenantSeederService.cs`
- `src/Sgcf.Application/Tenancy/ITenantCache.cs`
- `src/Sgcf.Application/Tenancy/TenantInfo.cs`
- `src/Sgcf.Infrastructure/Tenancy/TenantCache.cs`
- `src/Sgcf.Infrastructure/Tenancy/TenantCacheInvalidationSubscriber.cs`
- `src/Sgcf.Api/Controllers/TenantsController.cs` (adicionar `cache.InvalidateAsync` em mutações)
- `tests/Sgcf.Api.IntegrationTests/Tenancy/TenantResolverMiddlewareTests.cs`
- `tests/Sgcf.Application.Tests/Tenancy/TenantCacheTests.cs`
