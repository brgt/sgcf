# SPEC — Task −1.6 — Provisionamento Idempotente de Tenant

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.6
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Task −1.5

---

## 1. Objetivo

Criar fluxo idempotente que popula dados-base de um tenant recém criado: parâmetros de sistema (tetão), parâmetros de cotação (defaults), plano de contas (clone do modelo global). Sem provisionamento, o tenant existe na tabela mas não opera.

---

## 2. Endpoint

```
POST /api/v1/admin/tenants/{idOrSlug}/provisionar
```

**Auth:** `Policies.SuperAdmin`.
**Body:** `{}` (vazio) ou `{"sobrescrever": false}`.
**Header:** `Idempotency-Key` recomendado.

### 2.1 Response 200

```json
{
  "tenantId": "...",
  "tenantSlug": "acme-finance",
  "criados": {
    "parametros_sistema": 1,
    "parametros_cotacao": 1,
    "plano_contas": 47
  },
  "ignorados": {
    "parametros_sistema": 0,
    "parametros_cotacao": 0,
    "plano_contas": 0
  },
  "provisionadoEm": "2026-05-20T14:00:00Z"
}
```

### 2.2 Erros

- 404 — tenant não existe.
- 409 — tenant `Arquivado` (não pode ser provisionado).
- 400 — tenant `Suspenso` (descongelar antes).

---

## 3. Domain Service

```csharp
namespace Sgcf.Application.Tenancy.Services;

public interface ITenantProvisioner
{
    Task<ResultadoProvisionamento> ProvisionarAsync(Guid tenantId, CancellationToken ct);
}

public sealed record ResultadoProvisionamento(
    Guid TenantId,
    string TenantSlug,
    Dictionary<string, int> Criados,
    Dictionary<string, int> Ignorados,
    Instant ProvisionadoEm);

internal sealed class TenantProvisioner(
    ITenantRepository tenantRepo,
    SgcfDbContext db,
    IClock clock,
    ITenantContext tenantContext,
    ILogger<TenantProvisioner> logger) : ITenantProvisioner
{
    public async Task<ResultadoProvisionamento> ProvisionarAsync(Guid tenantId, CancellationToken ct)
    {
        Tenant? tenant = await tenantRepo.GetAsync(tenantId, ct);
        if (tenant is null) throw new KeyNotFoundException("Tenant não encontrado.");
        if (tenant.Status == StatusTenant.Arquivado)
            throw new InvalidOperationException("Tenant arquivado.");

        // Set tenant context manualmente (não há HTTP request neste contexto)
        var ctx = (TenantContext)tenantContext;
        if (!ctx.IsResolved)
        {
            ctx.Resolve(tenant.Id, tenant.Slug, isSuperAdmin: true, isImpersonating: true);
        }

        Dictionary<string, int> criados = new();
        Dictionary<string, int> ignorados = new();

        criados["parametros_sistema"] = await SeedParametrosSistemaAsync(ct, ignorados);
        criados["parametros_cotacao"] = await SeedParametrosCotacaoAsync(ct, ignorados);
        criados["plano_contas"] = await SeedPlanoContasAsync(ct, ignorados);

        await db.SaveChangesAsync(ct);

        Instant agora = clock.GetCurrentInstant();
        logger.LogInformation("Tenant {Slug} provisionado: {@Criados}", tenant.Slug, criados);

        return new ResultadoProvisionamento(tenant.Id, tenant.Slug, criados, ignorados, agora);
    }
    // ... seed helpers
}
```

### 3.1 Seed de `ParametroSistema`

```csharp
private async Task<int> SeedParametrosSistemaAsync(CancellationToken ct, Dictionary<string, int> ignorados)
{
    bool jaExiste = await db.ParametrosSistema.AnyAsync(ct);
    if (jaExiste)
    {
        ignorados["parametros_sistema"] = 1;
        return 0;
    }

    db.ParametrosSistema.Add(ParametroSistema.CriarDefault(_clock)); // método novo (Task −1.9)
    return 1;
}
```

### 3.2 Seed de `ParametroCotacao`

Copia defaults globais (atual `singleton` global → vira per-tenant):

```csharp
private async Task<int> SeedParametrosCotacaoAsync(CancellationToken ct, Dictionary<string, int> ignorados)
{
    bool jaExiste = await db.ParametrosCotacao.AnyAsync(ct);
    if (jaExiste)
    {
        ignorados["parametros_cotacao"] = 1;
        return 0;
    }

    // Lê defaults do appsettings ou de seed pré-definido
    db.ParametrosCotacao.Add(ParametroCotacao.CriarDefault(_clock));
    return 1;
}
```

### 3.3 Seed de `PlanoContasGerencial`

Clona modelo global (Task −1.10 cuida da modelagem):

```csharp
private async Task<int> SeedPlanoContasAsync(CancellationToken ct, Dictionary<string, int> ignorados)
{
    bool jaExiste = await db.PlanoContas.AnyAsync(ct);
    if (jaExiste)
    {
        ignorados["plano_contas"] = await db.PlanoContas.CountAsync(ct);
        return 0;
    }

    IReadOnlyList<PlanoContasModelo> modelo = await db.PlanoContasModelo
        .WithTenantBypass() // modelo é global
        .ToListAsync(ct);

    foreach (var item in modelo)
    {
        db.PlanoContas.Add(PlanoContasGerencial.ClonarDeModelo(item, _clock));
    }
    return modelo.Count;
}
```

---

## 4. Idempotência

Provisionamento **não usa flag** persistido em `Tenant` ("provisionado: true"). Em vez disso, cada seed verifica se a tabela já tem dados para aquele tenant — se sim, ignora.

Vantagens:

- Repetir provisionamento sempre é seguro.
- Recuperar de falha parcial é trivial: chamar de novo.
- Sem estado adicional para gerenciar.

Idempotency-Key no header garante semântica HTTP idempotente (mesma chave + mesmo body = mesma resposta) via `IdempotencyFilter` existente.

---

## 5. AuditLog

Registra evento `TenantProvisionado`:

- `Entity = "Tenant"`.
- `EntityId = tenant.Id`.
- `Operation = "Provisionar"`.
- `DiffJson = { criados, ignorados, atorSub }`.

Como provisionamento é cross-tenant (operador Nordware atuando no tenant do cliente), o `AuditLog` é registrado **no tenant alvo** com flag `Impersonating = true`.

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Provisionar 2x sem mudanças | Segunda chamada retorna `criados = 0, ignorados > 0` |
| Provisionar tenant `Suspenso` | 400 com `detail: "Descongelar tenant antes."` |
| Provisionar tenant `Arquivado` | 409 |
| Provisionar enquanto outra requisição provisiona | Lock otimista via constraint `UNIQUE` em seeds; segunda chamada ignora |
| Falha no meio (ex.: erro no PlanoContas) | Transação completa rola back; tenant fica em estado consistente — chamar novamente |
| Tenant criado sem provisionar e usuário tenta operar | Listagens retornam vazias; UI exibe "Tenant não provisionado" (FE) |
| Falta de modelo global de plano de contas | Falha com `detail: "Modelo global de plano de contas não encontrado"` |

---

## 7. Critérios de Aceite

- [ ] Endpoint `POST /admin/tenants/{idOrSlug}/provisionar` operacional.
- [ ] `ITenantProvisioner` implementado em `Sgcf.Application.Tenancy.Services`.
- [ ] Provisionamento popula 3 áreas: `ParametroSistema`, `ParametroCotacao`, `PlanoContasGerencial`.
- [ ] Idempotência via "já existe? ignora.".
- [ ] AuditLog registra evento.
- [ ] Operação inteira em transação única (rollback total em falha).
- [ ] Tenant suspenso retorna 400; arquivado retorna 409.

---

## 8. Verificação

```bash
dotnet test --filter "FullyQualifiedName~Provisionar"

# Smoke
curl -X POST http://localhost:5000/api/v1/admin/tenants/acme-finance/provisionar \
  -H "Authorization: Bearer $SUPER_ADMIN_TOKEN"
```

**Testes-chave:**

```csharp
[Fact]
public async Task Provisionar_chamado_duas_vezes_eh_idempotente()
{
    Guid tenantId = await CriarTenant("acme");

    var r1 = await PostProvisionar(tenantId);
    var r2 = await PostProvisionar(tenantId);

    r1.Criados["plano_contas"].Should().BeGreaterThan(0);
    r2.Criados["plano_contas"].Should().Be(0);
    r2.Ignorados["plano_contas"].Should().BeGreaterThan(0);
}

[Fact]
public async Task Provisionar_falha_no_meio_deixa_tenant_consistente()
{
    Guid tenantId = await CriarTenant("acme");
    SimularFalhaApos("parametros_cotacao");

    Func<Task> act = () => PostProvisionar(tenantId);
    await act.Should().ThrowAsync<Exception>();

    int parametros = await ContarParametrosSistema(tenantId);
    parametros.Should().Be(0); // rollback total
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Operação em transação única.
- Idempotência por inspeção de estado, não por flag.
- AuditLog em todo provisionamento.

### 9.2 Ask first
- Adicionar área nova ao provisionamento (impacta tempo + transação).
- Permitir provisionamento parcial sem rollback (perigoso).

### 9.3 Never do
- Provisionar tenant arquivado.
- Compartilhar seed entre tenants (cada um recebe cópia).
- Skip de auditoria (toda operação cross-tenant precisa rastro).

---

## 10. Arquivos esperados

- `src/Sgcf.Application/Tenancy/Services/ITenantProvisioner.cs`
- `src/Sgcf.Application/Tenancy/Services/TenantProvisioner.cs`
- `src/Sgcf.Application/Tenancy/Commands/ProvisionarTenantCommand.cs` + Handler
- `src/Sgcf.Api/Controllers/TenantsController.cs` (endpoint novo)
- `tests/Sgcf.Application.Tests/Tenancy/TenantProvisionerTests.cs`
- `tests/Sgcf.Api.IntegrationTests/Tenancy/ProvisionarTenantControllerTests.cs`
