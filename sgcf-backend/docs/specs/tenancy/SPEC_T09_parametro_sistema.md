# SPEC — Task −1.9 — `ParametroSistema` Per-Tenant

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.9
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Tasks −1.5, −1.6

---

## 1. Objetivo

Refatorar `ParametroSistema` para deixar de ser singleton global ("GLOBAL") e virar per-tenant. Cada cliente passa a ter sua própria configuração de tetão mensal e outros parâmetros de controle.

Mesmo padrão para `ParametroCotacao` (defaults de IRRF, IOF, FGI fee).

---

## 2. Mudanças no domínio

### 2.1 `ParametroSistema`

**Antes (singleton global):**

```csharp
public sealed class ParametroSistema : Entity, IAuditable
{
    public const string ChaveGlobal = "GLOBAL";
    public string Chave { get; private set; } = ChaveGlobal; // sempre "GLOBAL"
    public decimal? TetaoMensalCapacidadeBrlDecimal { get; private set; }
    // ...
}
```

**Depois (per-tenant):**

```csharp
public sealed class ParametroSistema : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public string Chave { get; private set; } = "DEFAULT"; // discriminador para extensão futura
    public Money? TetaoMensalCapacidadeBrl { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public string AtualizadoPor { get; private set; } = default!;

    private ParametroSistema() { }

    public static ParametroSistema CriarDefault(IClock clock)
    {
        Instant agora = clock.GetCurrentInstant();
        return new ParametroSistema
        {
            Chave = "DEFAULT",
            TetaoMensalCapacidadeBrl = null, // sem tetão configurado
            CreatedAt = agora,
            UpdatedAt = agora,
            AtualizadoPor = "system",
        };
    }

    public void AtualizarTetao(Money? novoTetao, string usuario, IClock clock)
    {
        if (novoTetao is { } valor && valor.Valor < 0)
            throw new ArgumentException("Tetão não pode ser negativo.");

        TetaoMensalCapacidadeBrl = novoTetao;
        UpdatedAt = clock.GetCurrentInstant();
        AtualizadoPor = usuario;
    }
}
```

Constante `ChaveGlobal = "GLOBAL"` é **removida**. Singleton por tenant.

### 2.2 Repositório

```csharp
public interface IParametroSistemaRepository
{
    Task<ParametroSistema?> GetAsync(CancellationToken ct);
    Task UpsertAsync(ParametroSistema parametros, CancellationToken ct);
}
```

Por causa do EF filter, `GetAsync()` sem parâmetro retorna o `ParametroSistema` do tenant atual. Não precisa passar `tenantId` no método.

### 2.3 `ParametroCotacao` (mesmo refactor)

```csharp
public sealed class ParametroCotacao : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public decimal AliqIrrfPctDefault { get; private set; }
    public decimal AliqIofCambioPctDefault { get; private set; }
    public decimal TarifaRofBrlDefault { get; private set; }
    public decimal TarifaCadempBrlDefault { get; private set; }
    public decimal TaxaFgiAaPctDefault { get; private set; }
    // ...

    public static ParametroCotacao CriarDefault(IClock clock)
    {
        // Valores conservadores que cobrem maioria dos casos brasileiros
        return new ParametroCotacao
        {
            AliqIrrfPctDefault = 15.0m,
            AliqIofCambioPctDefault = 6.38m,
            TarifaRofBrlDefault = 800.00m,
            TarifaCadempBrlDefault = 250.00m,
            TaxaFgiAaPctDefault = 1.0m,
            // ...
        };
    }
}
```

---

## 3. Schema PostgreSQL

A coluna `tenant_id` já foi adicionada na Task −1.5. Esta task **apenas reescreve a unique constraint**:

```sql
-- Antes
CREATE UNIQUE INDEX ix_parametro_sistema_chave ON sgcf.parametro_sistema (chave);

-- Depois (já feito na Task −1.5)
CREATE UNIQUE INDEX ix_parametro_sistema_tenant_chave
    ON sgcf.parametro_sistema (tenant_id, chave);
```

Nada mais a fazer no schema. Migration desta task é apenas **data migration**:

```csharp
public partial class ParametroSistemaPerTenant : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        // Garantir que tenant Proxys já tem ParametroSistema (idempotente)
        mb.Sql(@"
            INSERT INTO sgcf.parametro_sistema (id, tenant_id, chave, tetao_mensal_capacidade_brl_decimal, created_at, updated_at, atualizado_por)
            SELECT
                gen_random_uuid(),
                '00000000-0000-7000-8000-000000000001',
                'DEFAULT',
                NULL,
                NOW() AT TIME ZONE 'UTC',
                NOW() AT TIME ZONE 'UTC',
                'migration'
            WHERE NOT EXISTS (
                SELECT 1 FROM sgcf.parametro_sistema
                WHERE tenant_id = '00000000-0000-7000-8000-000000000001'
            );

            -- Atualizar registro 'GLOBAL' legado (se existir) para 'DEFAULT'
            UPDATE sgcf.parametro_sistema
            SET chave = 'DEFAULT'
            WHERE chave = 'GLOBAL';
        ");
    }
}
```

---

## 4. Endpoints (existentes — sem mudança de contrato)

`ParametrosSistemaController` continua com mesmo path:

```
GET  /api/v1/parametros-sistema           ← retorna o do tenant atual
PATCH /api/v1/parametros-sistema           ← atualiza tetão (mesma estrutura)
```

DTO inalterado, comportamento muda: queries automaticamente filtradas por `tenant_id`.

---

## 5. `ValidadorTetaoMensal`

Antes lia o registro global `"GLOBAL"`. Agora lê via EF filter (tenant atual):

```csharp
namespace Sgcf.Application.Painel;

public sealed class ValidadorTetaoMensal(IParametroSistemaRepository repo)
{
    public async Task<bool> ExcedeAsync(Money valorPropostoMes, CancellationToken ct)
    {
        ParametroSistema? p = await repo.GetAsync(ct); // <- filter aplica tenant_id automaticamente
        if (p is null || p.TetaoMensalCapacidadeBrl is null) return false;

        return valorPropostoMes.Valor > p.TetaoMensalCapacidadeBrl.Value.Valor;
    }
}
```

---

## 6. Provisionamento

Task −1.6 (provisionamento) já chama `ParametroSistema.CriarDefault` para tenants novos. Confirmar que está usando o construtor novo.

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Tenant sem `ParametroSistema` (não provisionado) | `GET /parametros-sistema` retorna 404; FE pede provisionamento |
| Tenant A define tetão 1mi, B define 5mi | Validador respeita o tetão do tenant atual em cada operação |
| Endpoint legado consumido por client antigo esperando `chave: GLOBAL` | Migration troca para `DEFAULT`; FE precisa atualizar contrato (decisão: comunicar com 1 sprint de antecedência) |
| Tetão null = sem limite | Validador retorna `false` (não excede) |
| Tetão zero | Tratado como "sem tetão" ou "tetão zero válido"? **Decisão:** zero é zero (toda operação excede) — usuário define null para "ilimitado" |

---

## 8. Critérios de Aceite

- [ ] `ParametroSistema` implementa `ITenantScoped` + propriedade `TenantId`.
- [ ] Constante `ChaveGlobal` removida; `Chave` agora é `DEFAULT`.
- [ ] `ParametroSistema.CriarDefault(IClock)` substitui criação manual.
- [ ] `ParametroSistema.AtualizarTetao(...)` substitui atualização ad-hoc.
- [ ] `IParametroSistemaRepository.GetAsync()` (sem `tenantId`) retorna o do tenant atual.
- [ ] `ValidadorTetaoMensal` lê do tenant atual.
- [ ] Migration de data migration aplicada (`chave: GLOBAL` → `DEFAULT`; tenant proxys recebe registro).
- [ ] `ParametroCotacao` mesma transformação.
- [ ] Provisionamento (Task −1.6) cria parâmetros default per-tenant.
- [ ] Teste cross-tenant: tetão A != tetão B.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~ParametroSistema"
dotnet test --filter "FullyQualifiedName~ValidadorTetao"
```

**Teste-chave:**

```csharp
[Fact]
public async Task Tetao_eh_isolado_por_tenant()
{
    Guid tenantA = await CriarTenantProvisionado();
    Guid tenantB = await CriarTenantProvisionado();

    using (var scopeA = ScopeFor(tenantA))
    {
        var repo = scopeA.Resolve<IParametroSistemaRepository>();
        var p = await repo.GetAsync();
        p!.AtualizarTetao(Money.Brl(1_000_000m), "user-a", _clock);
        await scopeA.SaveChanges();
    }

    using (var scopeB = ScopeFor(tenantB))
    {
        var repo = scopeB.Resolve<IParametroSistemaRepository>();
        var p = await repo.GetAsync();
        p!.AtualizarTetao(Money.Brl(5_000_000m), "user-b", _clock);
        await scopeB.SaveChanges();
    }

    using (var scopeA = ScopeFor(tenantA))
    {
        var repo = scopeA.Resolve<IParametroSistemaRepository>();
        var p = await repo.GetAsync();
        p!.TetaoMensalCapacidadeBrl!.Value.Valor.Should().Be(1_000_000m);
    }
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Manter `Chave` como discriminador (uso futuro para múltiplos parâmetros por tenant).
- Usar `Money` em vez de `decimal` cru.
- Audit log em mutações de tetão.

### 10.2 Ask first
- Adicionar `ParametroSistema` global (não-tenant) para configurações de sistema (versão, feature flags).
- Tornar tetão obrigatório (não-nullable).

### 10.3 Never do
- Reintroduzir constante `ChaveGlobal`.
- Compartilhar `ParametroSistema` entre tenants via lookup global.
- Persistir `decimal` direto sem `Money`.

---

## 11. Arquivos esperados

- `src/Sgcf.Domain/Sistema/ParametroSistema.cs` (refatorar)
- `src/Sgcf.Domain/Cambio/ParametroCotacao.cs` (refatorar)
- `src/Sgcf.Application/Sistema/IParametroSistemaRepository.cs` (assinatura limpa)
- `src/Sgcf.Application/Painel/ValidadorTetaoMensal.cs` (atualizar)
- `src/Sgcf.Infrastructure/Migrations/<ts>_ParametroSistemaPerTenant.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/ParametroSistemaConfiguration.cs` (atualizar)
- `tests/Sgcf.Application.Tests/Sistema/ParametroSistemaPerTenantTests.cs`
- `tests/Sgcf.Application.Tests/Painel/ValidadorTetaoMensalPerTenantTests.cs`
