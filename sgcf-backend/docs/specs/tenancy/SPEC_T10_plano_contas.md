# SPEC — Task −1.10 — `PlanoContasGerencial` Per-Tenant + Modelo Global

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.10
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Dependências:** Tasks −1.5, −1.6

---

## 1. Objetivo

Tornar `PlanoContasGerencial` per-tenant — cada cliente tem plano próprio, personalizável. Manter um **modelo global** (`PlanoContasModelo`) que serve de base para clonagem na provisão.

---

## 2. Modelo de Domínio

### 2.1 `PlanoContasModelo` (global, novo)

```csharp
namespace Sgcf.Domain.Contabilidade;

/// <summary>
/// Modelo global de plano de contas — referência para clonagem em novos tenants.
/// Não é tenant-scoped. Mutável apenas por super-admin via endpoint admin.
/// </summary>
public sealed class PlanoContasModelo : Entity, IAuditable
{
    public string Codigo { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public NaturezaConta Natureza { get; private set; }
    public string? CodigoPai { get; private set; }
    public int Nivel { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private PlanoContasModelo() { }

    public static PlanoContasModelo Criar(
        string codigo, string nome, NaturezaConta natureza,
        string? codigoPai, int nivel, IClock clock)
    {
        // validações
        Instant agora = clock.GetCurrentInstant();
        return new PlanoContasModelo
        {
            Codigo = codigo, Nome = nome, Natureza = natureza,
            CodigoPai = codigoPai, Nivel = nivel,
            CreatedAt = agora, UpdatedAt = agora,
        };
    }
}
```

### 2.2 `PlanoContasGerencial` (per-tenant, refatorado)

```csharp
public sealed class PlanoContasGerencial : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public string Codigo { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public NaturezaConta Natureza { get; private set; }
    public string? CodigoPai { get; private set; }
    public int Nivel { get; private set; }
    public bool Ativa { get; private set; }
    public bool ClonadaDeModelo { get; private set; }  // rastreabilidade
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private PlanoContasGerencial() { }

    /// <summary>Clona uma entrada do modelo global para o tenant atual.</summary>
    internal static PlanoContasGerencial ClonarDeModelo(PlanoContasModelo modelo, IClock clock)
    {
        Instant agora = clock.GetCurrentInstant();
        return new PlanoContasGerencial
        {
            Codigo = modelo.Codigo, Nome = modelo.Nome, Natureza = modelo.Natureza,
            CodigoPai = modelo.CodigoPai, Nivel = modelo.Nivel,
            Ativa = true, ClonadaDeModelo = true,
            CreatedAt = agora, UpdatedAt = agora,
        };
    }

    /// <summary>Cria conta custom (não-modelo) no tenant.</summary>
    public static PlanoContasGerencial CriarCustom(
        string codigo, string nome, NaturezaConta natureza,
        string? codigoPai, int nivel, IClock clock) { /* ... */ }

    public void Renomear(string novoNome, IClock clock) { /* ... */ }
    public void Desativar(IClock clock) { Ativa = false; }
}
```

---

## 3. Schema PostgreSQL

```sql
-- Nova tabela: modelo global
CREATE TABLE sgcf.plano_contas_modelo (
    id          UUID PRIMARY KEY,
    codigo      TEXT NOT NULL UNIQUE,
    nome        TEXT NOT NULL,
    natureza    SMALLINT NOT NULL,
    codigo_pai  TEXT NULL,
    nivel       SMALLINT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL
);

-- Existente: plano_contas_gerencial — apenas adicionar coluna clonada_de_modelo
ALTER TABLE sgcf.plano_contas_gerencial
    ADD COLUMN clonada_de_modelo BOOLEAN NOT NULL DEFAULT FALSE;

-- Unique reescrito na Task −1.5: (tenant_id, codigo)
```

Migration:

```csharp
public partial class PlanoContasModeloEPerTenant : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        // Criar tabela modelo
        mb.CreateTable(
            name: "plano_contas_modelo",
            schema: "sgcf",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                Codigo = t.Column<string>(type: "text", nullable: false),
                // ...
            },
            constraints: t => t.PrimaryKey("PK_plano_contas_modelo", x => x.Id));

        // Seed do modelo a partir de plano padrão brasileiro (resumo abaixo)
        mb.Sql(SeedPlanoContasModeloPadrao);

        // Adicionar coluna clonada_de_modelo
        mb.AddColumn<bool>(
            name: "clonada_de_modelo",
            table: "plano_contas_gerencial",
            schema: "sgcf",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Migrar dados existentes do Proxys: marcar como clonadas se baterem com modelo
        mb.Sql(@"
            UPDATE sgcf.plano_contas_gerencial pcg
            SET clonada_de_modelo = TRUE
            WHERE EXISTS (
                SELECT 1 FROM sgcf.plano_contas_modelo m
                WHERE m.codigo = pcg.codigo
            );
        ");
    }

    private const string SeedPlanoContasModeloPadrao = @"
        INSERT INTO sgcf.plano_contas_modelo (id, codigo, nome, natureza, codigo_pai, nivel, created_at, updated_at)
        VALUES
            (gen_random_uuid(), '1', 'Ativo', 1, NULL, 1, NOW(), NOW()),
            (gen_random_uuid(), '1.1', 'Ativo Circulante', 1, '1', 2, NOW(), NOW()),
            (gen_random_uuid(), '1.1.1', 'Disponibilidades', 1, '1.1', 3, NOW(), NOW()),
            -- ... ~47 entradas cobrindo plano gerencial padrão para financiamentos
            ;
    ";
}
```

---

## 4. Endpoints

### 4.1 `PlanoContasController` (per-tenant — existente)

Sem mudança de contrato. Operações automaticamente isoladas pelo EF filter.

```
GET  /api/v1/plano-contas              ← lista do tenant
POST /api/v1/plano-contas              ← cria conta custom no tenant
PATCH /api/v1/plano-contas/{codigo}    ← rename/desativa
```

### 4.2 `PlanoContasModeloController` (global — novo)

```
GET  /api/v1/admin/plano-contas-modelo
POST /api/v1/admin/plano-contas-modelo
PATCH /api/v1/admin/plano-contas-modelo/{codigo}
```

Apenas `super-admin`. Cuidado: alterar o modelo **não retroage** aos tenants já provisionados (cada um tem cópia).

---

## 5. Provisionamento

Task −1.6 (`TenantProvisioner.SeedPlanoContasAsync`) já clona o modelo. Confirmar:

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
        .WithTenantBypass()  // modelo é global
        .OrderBy(m => m.Codigo)
        .ToListAsync(ct);

    foreach (var item in modelo)
    {
        db.PlanoContas.Add(PlanoContasGerencial.ClonarDeModelo(item, _clock));
    }
    return modelo.Count;
}
```

---

## 6. Sincronização modelo → tenants existentes

**Não implementado neste MVP.** Quando o modelo recebe nova conta, tenants existentes **não recebem automaticamente**. Operador Nordware pode:

1. Adicionar manualmente conta no tenant via `POST /plano-contas` (impersonando).
2. Ou aguardar próxima provisão (caso o tenant ainda esteja em onboarding).

Endpoint futuro `POST /admin/plano-contas-modelo/{codigo}/propagar` para sincronização ativa — fica como item de backlog (Fase 4).

---

## 7. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Tenant edita conta que veio do modelo (renomeia) | Edição preservada; `clonada_de_modelo` continua true mas conta é divergente |
| Tenant desativa conta clonada | `Ativa = false`; consultas FE filtram por `Ativa = true` |
| Modelo recebe nova conta após tenant ter sido provisionado | Tenant não vê — sincronização manual ou Fase 4 |
| Tenant tenta criar conta com `Codigo` igual a conta clonada | 409 (unique `(tenant_id, codigo)`) |
| Operador remove conta do modelo | Conta nos tenants permanece (cópia independente) |

---

## 8. Critérios de Aceite

- [ ] `PlanoContasModelo` criado (global, sem `ITenantScoped`).
- [ ] `PlanoContasGerencial` implementa `ITenantScoped`.
- [ ] Coluna `clonada_de_modelo` adicionada.
- [ ] Migration cria tabela `plano_contas_modelo` + seed padrão (~47 contas).
- [ ] Provisionamento clona modelo para tenant novo.
- [ ] `PlanoContasModeloController` em `/admin/plano-contas-modelo` (super-admin).
- [ ] `PlanoContasController` continua operacional (per-tenant).
- [ ] Edição em tenant A não afeta tenant B nem modelo.

---

## 9. Verificação

```bash
dotnet test --filter "FullyQualifiedName~PlanoContas"
```

**Teste-chave:**

```csharp
[Fact]
public async Task Edicao_em_tenant_A_nao_afeta_tenant_B()
{
    Guid tenantA = await ProvisionarTenant("acme");
    Guid tenantB = await ProvisionarTenant("beta");

    using (var scopeA = ScopeFor(tenantA))
    {
        var repo = scopeA.Resolve<IPlanoContasRepository>();
        var conta = await repo.GetByCodigoAsync("1.1.1");
        conta!.Renomear("Caixa e Equivalentes (ACME)", _clock);
        await scopeA.SaveChanges();
    }

    using (var scopeB = ScopeFor(tenantB))
    {
        var repo = scopeB.Resolve<IPlanoContasRepository>();
        var conta = await repo.GetByCodigoAsync("1.1.1");
        conta!.Nome.Should().Be("Disponibilidades"); // nome original do modelo
    }
}
```

---

## 10. Boundaries específicas

### 10.1 Always do
- Marcar `ClonadaDeModelo = true` ao copiar do modelo.
- Modelo global usa `WithTenantBypass` em queries.
- Modelo é mutável apenas por super-admin.

### 10.2 Ask first
- Propagar mudanças do modelo aos tenants existentes (sync automático).
- Permitir clientes editarem o modelo global (impossível por design).

### 10.3 Never do
- Compartilhar `PlanoContasGerencial` entre tenants.
- Aplicar EF filter ao `PlanoContasModelo` (é global).
- Hard-delete conta clonada (usar `Desativar`).

---

## 11. Arquivos esperados

- `src/Sgcf.Domain/Contabilidade/PlanoContasModelo.cs` (novo)
- `src/Sgcf.Domain/Contabilidade/PlanoContasGerencial.cs` (refatorar)
- `src/Sgcf.Application/Contabilidade/IPlanoContasModeloRepository.cs`
- `src/Sgcf.Application/Contabilidade/IPlanoContasRepository.cs` (per-tenant)
- `src/Sgcf.Api/Controllers/PlanoContasModeloController.cs` (super-admin)
- `src/Sgcf.Api/Controllers/PlanoContasController.cs` (atualizar — manter contrato)
- `src/Sgcf.Infrastructure/Persistence/Configurations/PlanoContasModeloConfiguration.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/PlanoContasGerencialConfiguration.cs` (atualizar)
- `src/Sgcf.Infrastructure/Migrations/<ts>_PlanoContasModeloEPerTenant.cs`
- `tests/Sgcf.Application.Tests/Contabilidade/PlanoContasPerTenantTests.cs`
- `tests/Sgcf.Api.IntegrationTests/Tenancy/PlanoContasModeloControllerTests.cs`
