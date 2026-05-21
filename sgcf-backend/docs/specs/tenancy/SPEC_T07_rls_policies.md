# SPEC — Task −1.7 — Row Level Security (RLS) Policies

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.7
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** M
> **Dependências:** Task −1.5

---

## 1. Objetivo

Habilitar Row Level Security do PostgreSQL em todas as tabelas tenant-scoped como **segunda camada** de isolação. Se o EF Core global filter falhar por bug, o banco recusa retornar dados de outros tenants.

Implementa também o `DbConnectionInterceptor` que seta `app.tenant_id` no início de cada transação.

---

## 2. Migration RLS

`<ts>_EnableRowLevelSecurity.cs`:

```csharp
public partial class EnableRowLevelSecurity : Migration
{
    private static readonly string[] TabelasTenantScoped =
    {
        // mesma lista da Task −1.5
        "contrato", "parcela", "garantia", /* ... */
    };

    protected override void Up(MigrationBuilder mb)
    {
        // Criar role limitada (app) com bypass RLS desabilitado
        mb.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_app') THEN
                    CREATE ROLE sgcf_app NOLOGIN;
                END IF;
            END$$;
        ");

        foreach (string tabela in TabelasTenantScoped)
        {
            // Habilitar RLS
            mb.Sql($"ALTER TABLE sgcf.{tabela} ENABLE ROW LEVEL SECURITY;");
            mb.Sql($"ALTER TABLE sgcf.{tabela} FORCE ROW LEVEL SECURITY;");

            // Policy de isolação
            mb.Sql($@"
                CREATE POLICY tenant_isolation ON sgcf.{tabela}
                    AS PERMISSIVE
                    FOR ALL
                    TO sgcf_app
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
            ");
        }

        // Conceder permissões à role app
        mb.Sql("GRANT USAGE ON SCHEMA sgcf TO sgcf_app;");
        mb.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sgcf TO sgcf_app;");
        mb.Sql("GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA sgcf TO sgcf_app;");

        // Aplicar role à connection string em runtime
        // (alternativa: alterar appsettings para usar role sgcf_app)
    }

    protected override void Down(MigrationBuilder mb)
    {
        foreach (string tabela in TabelasTenantScoped)
        {
            mb.Sql($"DROP POLICY IF EXISTS tenant_isolation ON sgcf.{tabela};");
            mb.Sql($"ALTER TABLE sgcf.{tabela} DISABLE ROW LEVEL SECURITY;");
        }
    }
}
```

### 2.1 Por que `FORCE ROW LEVEL SECURITY`?

Sem `FORCE`, o owner da tabela (geralmente o usuário de migration) bypassa RLS. Com `FORCE`, **todos** os usuários (inclusive o owner) respeitam policies — exceto `super_user` Postgres. Garante que mesmo um bug operacional usando o usuário errado não vaze.

### 2.2 Por que role separada `sgcf_app`?

A aplicação conecta como `sgcf_app` (role com permissão DML mas sem bypass de RLS). Migrations rodam com role `sgcf_migrator` (com privilégios maiores). Separação reduz risco.

Connection strings:

- `Default` (app): `Username=sgcf_app;Password=...`
- `Migration` (CI/CD): `Username=sgcf_migrator;Password=...`

---

## 3. `TenantConnectionInterceptor`

Seta `app.tenant_id` no início de cada conexão/transação:

```csharp
namespace Sgcf.Infrastructure.Persistence;

public sealed class TenantConnectionInterceptor(ITenantContext tenantContext)
    : DbConnectionInterceptor
{
    public override async ValueTask<DbConnection> ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken ct)
    {
        await SetarTenantIdAsync(connection, ct);
        return await base.ConnectionOpenedAsync(connection, eventData, ct);
    }

    private async Task SetarTenantIdAsync(DbConnection connection, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            // Não setar — RLS recusará leituras. Comportamento seguro.
            return;
        }

        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', $1, false)";
        DbParameter p = cmd.CreateParameter();
        p.ParameterName = "$1";
        p.Value = tenantContext.TenantId.ToString();
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
```

**Por que `set_config` com `false`?** Define no nível da **session** (não da transaction). Como connection pool reaproveita conexões, garantimos que cada `Open` reseta. Alternativa `true` (`SET LOCAL`) só vale dentro da transaction — falha em consultas fora de transação explícita.

### 3.1 Registro

```csharp
builder.Services.AddDbContext<SgcfDbContext>((sp, opts) =>
{
    opts.UseNpgsql(connStr);
    opts.AddInterceptors(
        sp.GetRequiredService<TenantSaveInterceptor>(),
        sp.GetRequiredService<TenantConnectionInterceptor>());
});

builder.Services.AddScoped<TenantConnectionInterceptor>();
```

---

## 4. Bypass para super-admin

Operações cross-tenant precisam **temporariamente** desativar RLS para a transação. Helper:

```csharp
namespace Sgcf.Infrastructure.Tenancy;

public static class RlsBypass
{
    public static async Task ExecuteWithoutRlsAsync(
        SgcfDbContext db,
        Func<Task> body,
        CancellationToken ct)
    {
        // Apenas super-admin pode bypassar
        var tenantContext = db.GetService<ITenantContext>();
        if (!tenantContext.IsSuperAdmin)
            throw new UnauthorizedAccessException("RLS bypass exige super-admin.");

        // Resetar tenant_id no Postgres a "bypass"
        await using DbCommand cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', '00000000-0000-0000-0000-000000000000', false)";
        await cmd.ExecuteNonQueryAsync(ct);

        try
        {
            await body();
        }
        finally
        {
            // Restaurar
            cmd.CommandText = $"SELECT set_config('app.tenant_id', '{tenantContext.TenantId}', false)";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
```

**Problema:** o UUID zero não bate com nenhum tenant, então RLS continua retornando 0 linhas. Para bypass real, criar role separada `sgcf_super` que tem atributo `BYPASSRLS`:

```sql
CREATE ROLE sgcf_super NOLOGIN BYPASSRLS;
GRANT sgcf_app TO sgcf_super;
```

Conexão dedicada para super-admin operations usa essa role. Implementação fica no helper acima (switch de connection string ou `SET ROLE`).

---

## 5. Healthcheck pré-requisito

Validação em runtime que RLS está habilitada (usada pela Task −1.12 `/health/rls`):

```sql
-- Listar tabelas tenant-scoped que NÃO têm RLS habilitada
SELECT c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'sgcf'
  AND c.relkind = 'r'
  AND c.relname IN ('contrato', 'parcela', /* ... lista completa */)
  AND NOT c.relrowsecurity;
-- Deve retornar 0 linhas.

-- Listar policies por tabela
SELECT schemaname, tablename, policyname
FROM pg_policies
WHERE schemaname = 'sgcf'
ORDER BY tablename;
```

---

## 6. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Conexão sem `app.tenant_id` setado | RLS retorna 0 linhas em qualquer SELECT (comportamento seguro) |
| `app.tenant_id` setado com UUID inválido | `current_setting(..., true)` retorna null → policy avalia false → 0 linhas |
| Connection pool reaproveita conexão de outro request | `ConnectionOpenedAsync` reseta `app.tenant_id` no próximo `Open` |
| Transação aninhada | `set_config` com `false` é session-level — persiste durante toda a sessão |
| Super-admin precisa listar todos os tenants | Connection via role `sgcf_super` com `BYPASSRLS` |
| Migrations rodam | Role `sgcf_migrator` tem `BYPASSRLS` — não afetado |
| Backup `pg_dump` | Roda como super-user — não afetado |
| Hosted job | `TenantScope.ExecuteForTenantAsync` resolve contexto antes de abrir conexão |

---

## 7. Critérios de Aceite

- [ ] Migration cria roles `sgcf_app` (sem bypass) e `sgcf_super` (com `BYPASSRLS`).
- [ ] RLS habilitado em todas as ~30 tabelas tenant-scoped com `FORCE`.
- [ ] Policy `tenant_isolation` criada em cada tabela.
- [ ] `TenantConnectionInterceptor` seta `app.tenant_id` em `ConnectionOpenedAsync`.
- [ ] Conexão sem `app.tenant_id` retorna 0 linhas (validado manualmente em psql).
- [ ] `dotnet test` continua passando (testes usam `sgcf_app` + interceptor).
- [ ] Connection string da aplicação aponta para `sgcf_app`.

---

## 8. Verificação

```bash
# Aplicar migration
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Validar RLS habilitada
psql -U sgcf_app -d sgcf_dev -c "
SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'sgcf' AND c.relkind = 'r'
ORDER BY c.relname;"

# Tentar SELECT sem setar tenant
psql -U sgcf_app -d sgcf_dev -c "SELECT * FROM sgcf.contrato LIMIT 5;"
# → 0 rows

# Setar tenant e tentar de novo
psql -U sgcf_app -d sgcf_dev <<SQL
SELECT set_config('app.tenant_id', '00000000-0000-7000-8000-000000000001', false);
SELECT COUNT(*) FROM sgcf.contrato;
SQL
# → contagem real
```

**Teste-chave:**

```csharp
[Fact]
[Trait("Category", "Slow")]
public async Task Conexao_sem_app_tenant_id_retorna_zero_linhas()
{
    using var conn = new NpgsqlConnection(_connStringApp);
    await conn.OpenAsync();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM sgcf.contrato";
    long count = (long)(await cmd.ExecuteScalarAsync())!;

    count.Should().Be(0);
}

[Fact]
public async Task Tentativa_de_inserir_em_outro_tenant_via_RLS_falha()
{
    using var conn = new NpgsqlConnection(_connStringApp);
    await conn.OpenAsync();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"""
        SELECT set_config('app.tenant_id', '{_tenantA}', false);
        INSERT INTO sgcf.contrato (id, tenant_id, /* ... */)
        VALUES ('{Guid.NewGuid()}', '{_tenantB}', /* ... */);
    """;

    Func<Task> act = () => cmd.ExecuteNonQueryAsync();
    // RLS WITH CHECK bloqueia INSERT com tenant_id != app.tenant_id
    await act.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == "42501");
}
```

---

## 9. Boundaries específicas

### 9.1 Always do
- Aplicar `ENABLE ROW LEVEL SECURITY` + `FORCE` em toda nova tabela tenant-scoped.
- Criar policy `tenant_isolation` na mesma migration que cria a tabela.
- Conectar aplicação como `sgcf_app` (sem bypass).

### 9.2 Ask first
- Adicionar role nova com `BYPASSRLS` (escalada de privilégio).
- Disable RLS temporariamente em produção (exige plano).

### 9.3 Never do
- Conectar aplicação como `sgcf_super` (bypass) em rotas normais.
- Hardcode tenant_id em queries SQL (`WHERE tenant_id = 'fixed-uuid'`).
- Commitar credenciais de `sgcf_super` no repositório.
- Habilitar `BYPASSRLS` em role usada por jobs operacionais (que devem rodar com tenant scope).

---

## 10. Arquivos esperados

- `src/Sgcf.Infrastructure/Migrations/<ts>_EnableRowLevelSecurity.cs`
- `src/Sgcf.Infrastructure/Persistence/TenantConnectionInterceptor.cs`
- `src/Sgcf.Infrastructure/Tenancy/RlsBypass.cs`
- `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` (registrar interceptor)
- `src/Sgcf.Api/Program.cs` (configurar conn string para sgcf_app)
- `tests/Sgcf.Application.Tests/Tenancy/RlsPoliciesTests.cs`
- `tests/Sgcf.Application.Tests/Tenancy/TenantConnectionInterceptorTests.cs`
- `infra/dev/scripts/setup_roles.sql` (criação inicial de roles no Docker Compose)
