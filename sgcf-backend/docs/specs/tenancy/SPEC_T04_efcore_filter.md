# SPEC — Task −1.4 — Marker `ITenantScoped` + EF Core Global Query Filter

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.4
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** L (propagação repetitiva em ~30 entidades)
> **Dependências:** Tasks −1.1, −1.3

---

## 1. Objetivo

Introduzir marker `ITenantScoped` e fazer com que toda entidade tenant-scoped:

1. **Filtre automaticamente** queries por `tenant_id = ITenantContext.TenantId` via global filter EF Core.
2. **Preencha automaticamente** `TenantId` em entities `Added` no `SaveChanges`.
3. **Lance exceção** se persistir entidade sem `ITenantContext` resolvido.

---

## 2. Marker

```csharp
namespace Sgcf.Domain.Tenancy;

/// <summary>
/// Marca entidades operacionais que pertencem a um tenant específico.
/// Implementação automática de global query filter no SgcfDbContext.
/// TenantId é populado em SaveChanges via ITenantContext quando a entidade é Added.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
```

### 2.1 Implementação em agregados

Todos os agregados tenant-scoped recebem:

```csharp
public sealed class Contrato : Entity, IAuditable, ITenantScoped // <-- marker
{
    public Guid TenantId { get; private set; } // <-- coluna nova

    // ... resto inalterado

    // Fábricas Criar(...) NÃO recebem tenantId como parâmetro.
    // O TenantId é populado no SaveChanges interceptor.
}
```

**Lista completa** (~30 agregados — referência §2.3 do master):

- `Contrato`, `Parcela`, `Garantia`, `EventoCronograma`, `FinimpDetail`, `Lei4131Detail`, `RefinimpDetail`, `NceDetail`, `CapitalDeGiroDetail`, `FgiDetail`, todas `Garantia*Detail`
- `Cotacao`, `Proposta`, `EconomiaNegociacao`, `LimiteBanco`, `LimiteBancoHistorico`, `GarantiaExigidaLimite`
- `InstrumentoHedge`, `PosicaoSnapshot`
- `AlertaVencimento`, `AlertaExposicaoBanco`, `Alerta` (Task 0.2 cockpit)
- `SimulacaoAntecipacao`, `SimulacaoContratacao`, `CenarioSimulacao`
- `EbitdaMensal` (→ `DadosContabeisMensal` Task 1.3 cockpit), `SnapshotMensalPosicao`
- `LancamentoContabil`, `PlanoContasGerencial`
- `ParametroSistema`, `ParametroCotacao`
- `AuditLog`
- `ContaBancaria`, `SaldoCaixa`, `EventoFluxoCaixa` (Fase 3 cockpit)

---

## 3. Global Query Filter

`SgcfDbContext.OnModelCreating` aplica filter automaticamente por reflection:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("sgcf");
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(SgcfDbContext).Assembly);

    AplicarFiltroDeTenant(modelBuilder);

    base.OnModelCreating(modelBuilder);
}

private void AplicarFiltroDeTenant(ModelBuilder modelBuilder)
{
    foreach (IMutableEntityType et in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(ITenantScoped).IsAssignableFrom(et.ClrType)) continue;

        // x => EF.Property<Guid>(x, "TenantId") == _tenantContextProvider().TenantId
        ParameterExpression param = Expression.Parameter(et.ClrType, "x");
        MethodInfo efProp = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(Guid));
        MethodCallExpression propAccess = Expression.Call(efProp, param, Expression.Constant("TenantId"));

        // Acessar TenantId via field do DbContext (avalia em runtime, por request)
        MemberExpression tenantIdAccess = Expression.Property(
            Expression.Constant(this),
            nameof(CurrentTenantId));

        BinaryExpression equals = Expression.Equal(propAccess, tenantIdAccess);
        LambdaExpression filter = Expression.Lambda(equals, param);

        et.SetQueryFilter(filter);
    }
}

internal Guid CurrentTenantId => _tenantContext.IsResolved
    ? _tenantContext.TenantId
    : Guid.Empty; // resulta em zero linhas — comportamento seguro
```

### 3.1 Injeção do `ITenantContext` no DbContext

```csharp
public class SgcfDbContext(
    DbContextOptions<SgcfDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;
    // ... DbSets
}
```

**Lifetime do DbContext:** `Scoped` (default ASP.NET Core), alinhado com `ITenantContext`.

---

## 4. `SaveChanges` Interceptor

Popula `TenantId` em entries `Added`:

```csharp
namespace Sgcf.Infrastructure.Persistence;

public sealed class TenantSaveInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        PopularTenantId(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        PopularTenantId(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void PopularTenantId(DbContext? db)
    {
        if (db is null) return;

        foreach (EntityEntry entry in db.ChangeTracker.Entries())
        {
            if (entry.Entity is not ITenantScoped) continue;
            if (entry.State != EntityState.Added) continue;

            PropertyEntry tenantProp = entry.Property("TenantId");
            if (tenantProp.CurrentValue is Guid current && current != Guid.Empty)
            {
                // Já preenchido manualmente (caso raro de provisioner) — não sobrescreve.
                continue;
            }

            if (!tenantContext.IsResolved)
            {
                throw new MissingTenantContextException(
                    $"Tentativa de persistir {entry.Entity.GetType().Name} sem ITenantContext resolvido.");
            }

            tenantProp.CurrentValue = tenantContext.TenantId;
        }
    }
}
```

### 4.1 Registro no DbContext

```csharp
builder.Services.AddDbContext<SgcfDbContext>((sp, opts) =>
{
    opts.UseNpgsql(connStr);
    opts.AddInterceptors(sp.GetRequiredService<TenantSaveInterceptor>());
});

builder.Services.AddScoped<TenantSaveInterceptor>();
```

---

## 5. Bypass para `super-admin`

Operações cross-tenant (consulta de auditoria pelo Nordware) usam helper explícito:

```csharp
namespace Sgcf.Application.Tenancy;

public static class TenantBypass
{
    public static IQueryable<T> WithTenantBypass<T>(this IQueryable<T> source)
        => source.IgnoreQueryFilters();
}
```

Uso (raro, sempre auditado):

```csharp
// Em handler do TenantsController ou AuditoriaAdminController
public async Task<IReadOnlyList<AuditLog>> ListAllAuditAsync(/* ... */, CancellationToken ct)
{
    if (!_tenantContext.IsSuperAdmin)
        throw new UnauthorizedAccessException("Bypass exige super-admin.");

    return await _db.AuditLogs
        .WithTenantBypass()
        .Where(/* filtros */)
        .ToListAsync(ct);
}
```

**Boundary:** uso de `WithTenantBypass` exige PR com revisão obrigatória e label `cross-tenant-bypass` para rastrear.

---

## 6. Hosted Services e Tenant Scope

Para jobs (`Sgcf.Jobs`), `ITenantContext` não tem origem natural (não há HTTP request). Helper:

```csharp
namespace Sgcf.Infrastructure.Tenancy;

public static class TenantScope
{
    public static async Task ExecuteForTenantAsync(
        IServiceProvider rootSp,
        Tenant tenant,
        Func<IServiceProvider, Task> body)
    {
        using IServiceScope scope = rootSp.CreateScope();
        TenantContext ctx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        ctx.Resolve(tenant.Id, tenant.Slug, isSuperAdmin: false, isImpersonating: false);

        await body(scope.ServiceProvider);
    }

    public static async Task ExecuteForAllActiveTenantsAsync(
        IServiceProvider rootSp,
        Func<IServiceProvider, Task> body,
        CancellationToken ct)
    {
        using IServiceScope outerScope = rootSp.CreateScope();
        ITenantRepository repo = outerScope.ServiceProvider.GetRequiredService<ITenantRepository>();
        PagedResult<Tenant> page = await repo.ListAsync(StatusTenant.Ativo, 1, 1000, ct);

        foreach (Tenant t in page.Items)
        {
            await ExecuteForTenantAsync(rootSp, t, body);
        }
    }
}
```

Uso (Task 0.4 cockpit — `AlertasHostedService` itera sobre tenants):

```csharp
await TenantScope.ExecuteForAllActiveTenantsAsync(_serviceProvider, async sp =>
{
    var repo = sp.GetRequiredService<IAlertaRepository>();
    var regra = sp.GetRequiredService<RegraVencimentoIminente>();
    var candidatos = await regra.AvaliarAsync(_clock, ct);
    foreach (var alerta in candidatos) await repo.AddAsync(alerta, ct);
}, ct);
```

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Query LINQ sem tenant context resolvido | Retorna zero linhas (`CurrentTenantId == Guid.Empty`) |
| `Add` em entidade `ITenantScoped` sem contexto | `MissingTenantContextException` |
| `Update` em entidade já carregada (TenantId já preenchido) | Não muda (interceptor só atua em `Added`) |
| Joinings entre tenant-scoped e global (`Contrato.Banco`) | Filter aplica só em `Contrato`; `Banco` é global, sem filter |
| `IgnoreQueryFilters()` chamado sem `super-admin` | Permitido pela API EF, mas convenção exige helper `WithTenantBypass` (PR review captura uso direto) |
| Entidade filha (`Parcela`) carregada via `Include` do pai (`Contrato`) | Filter aplica em ambas; ambas têm `tenant_id` denormalizado |
| `Find<T>(id)` retorna entidade de outro tenant | Não retorna — `Find` respeita global filter no EF Core 11+ |

---

## 8. Critérios de Aceite

- [ ] Interface `ITenantScoped` em `Sgcf.Domain.Tenancy/ITenantScoped.cs`.
- [ ] Todas as ~30 entidades operacionais implementam `ITenantScoped` + propriedade `TenantId`.
- [ ] `SgcfDbContext.OnModelCreating` aplica filter via reflection para toda `ITenantScoped`.
- [ ] `TenantSaveInterceptor` registrado e popula `TenantId` em `Added`.
- [ ] `MissingTenantContextException` lançada quando persiste sem contexto.
- [ ] `WithTenantBypass()` extension funcional.
- [ ] `TenantScope.ExecuteForAllActiveTenantsAsync` funcional para jobs.
- [ ] Query no contexto do tenant A não retorna dados do tenant B (testado).
- [ ] `Find<Contrato>(id)` de outro tenant retorna null.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~GlobalFilter"
dotnet test --filter "FullyQualifiedName~TenantSaveInterceptor"
```

**Teste-chave:**

```csharp
[Fact]
public async Task Query_no_tenant_A_nao_retorna_dados_do_tenant_B()
{
    Guid tenantA = Guid.NewGuid();
    Guid tenantB = Guid.NewGuid();

    await CriarContratoNoTenant(tenantA, "CT-A-001");
    await CriarContratoNoTenant(tenantB, "CT-B-001");

    using (var scopeA = CriarScopeParaTenant(tenantA))
    {
        var db = scopeA.ServiceProvider.GetRequiredService<SgcfDbContext>();
        var contratos = await db.Contratos.ToListAsync();

        contratos.Should().HaveCount(1);
        contratos[0].NumeroExterno.Should().Be("CT-A-001");
    }
}

[Fact]
public async Task Persistir_entidade_sem_contexto_lanca()
{
    using var scope = CriarScopeSemTenantResolvido();
    var db = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();

    db.Contratos.Add(FixtureContrato());
    Func<Task> act = () => db.SaveChangesAsync();

    await act.Should().ThrowAsync<MissingTenantContextException>();
}
```

---

## 10. Riscos e Mitigações

| Risco | Mitigação |
|-------|-----------|
| Esquecer de implementar `ITenantScoped` em entidade nova | Analisador customizado (Roslyn) que lista entidades operacionais sem marker — fail no CI |
| Filter falha em entidade complexa com TPH/TPT | Cobertura por teste integration para cada DbSet |
| Performance da reflection no startup | Reflection só roda em `OnModelCreating`, uma vez por DbContext lifetime |
| Acesso `IgnoreQueryFilters` sem super-admin | Code review + label PR + auditoria em produção |

---

## 11. Boundaries específicas

### 11.1 Always do
- Implementar `ITenantScoped` em toda entidade operacional nova.
- Usar `WithTenantBypass` (helper) quando precisar ignorar filter, nunca `IgnoreQueryFilters` direto.
- Validar via teste que cada DbSet aplica filter.

### 11.2 Ask first
- Adicionar entidade nova sem `ITenantScoped` (provavelmente é catálogo global).
- Usar `WithTenantBypass` em handler não-admin.

### 11.3 Never do
- Adicionar `WHERE tenant_id = ...` manualmente (confiar no filter).
- Carregar entidade tenant-scoped sem contexto resolvido (lança exceção).
- Persistir `TenantId` diferente do contexto atual (interceptor proíbe).

---

## 12. Arquivos esperados

- `src/Sgcf.Domain/Tenancy/ITenantScoped.cs`
- `src/Sgcf.Domain/Contratos/Contrato.cs` (e ~30 outros agregados — adicionar `TenantId` + marker)
- `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` (filter via reflection)
- `src/Sgcf.Infrastructure/Persistence/TenantSaveInterceptor.cs`
- `src/Sgcf.Application/Tenancy/TenantBypass.cs`
- `src/Sgcf.Infrastructure/Tenancy/TenantScope.cs`
- `tests/Sgcf.Application.Tests/Tenancy/GlobalFilterTests.cs`
- `tests/Sgcf.Application.Tests/Tenancy/TenantSaveInterceptorTests.cs`
- `tests/Sgcf.Application.Tests/Tenancy/TenantScopeTests.cs`
