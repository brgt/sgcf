# SPEC — Task −1.5 — Migration Big-Bang `tenant_id` + Backfill

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.5
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** L
> **Dependências:** Task −1.4

---

## 1. Objetivo

Migrar o esquema PostgreSQL existente para multi-tenant em uma única migration coordenada que:

1. Cria tenant `proxys` (seed).
2. Adiciona `tenant_id UUID NULL` em ~30 tabelas operacionais.
3. Faz backfill com `tenant_id = proxys_id` em todas as linhas existentes.
4. Promove `tenant_id` para `NOT NULL`.
5. Reescreve unique constraints e foreign keys para incluir `tenant_id`.
6. Adiciona índices `(tenant_id, ...)` em todas as chaves de consulta frequente.

Não habilita RLS (Task −1.7 cuida).

---

## 2. Estrutura da Migration

`<ts>_AddTenantIdToAllTables.cs` — arquivo único, formato EF Core padrão.

```csharp
public partial class AddTenantIdToAllTables : Migration
{
    private static readonly Guid ProxysTenantId =
        new("00000000-0000-7000-8000-000000000001"); // mesmo UUID do DevTenantSeederService

    protected override void Up(MigrationBuilder mb)
    {
        // Passo 1: Seed do tenant proxys (caso não exista)
        mb.Sql($@"
            INSERT INTO sgcf.tenant (id, slug, nome, cnpj_mascarado, status, plano, criado_em, updated_at)
            VALUES (
                '{ProxysTenantId}',
                'proxys',
                'Proxys Comércio Eletrônico',
                '00.***.***/****-00',
                1, 2,
                NOW() AT TIME ZONE 'UTC',
                NOW() AT TIME ZONE 'UTC'
            )
            ON CONFLICT (id) DO NOTHING;
        ");

        // Passo 2: Adicionar coluna NULL em cada tabela tenant-scoped
        foreach (string tabela in TabelasTenantScoped)
        {
            mb.AddColumn<Guid>(
                name: "tenant_id",
                table: tabela,
                schema: "sgcf",
                type: "uuid",
                nullable: true);
        }

        // Passo 3: Backfill com proxys_id
        foreach (string tabela in TabelasTenantScoped)
        {
            mb.Sql($"UPDATE sgcf.{tabela} SET tenant_id = '{ProxysTenantId}' WHERE tenant_id IS NULL;");
        }

        // Passo 4: Promover para NOT NULL
        foreach (string tabela in TabelasTenantScoped)
        {
            mb.AlterColumn<Guid>(
                name: "tenant_id",
                table: tabela,
                schema: "sgcf",
                type: "uuid",
                nullable: false);
        }

        // Passo 5: Reescrever unique constraints existentes
        ReescreverUniqueConstraints(mb);

        // Passo 6: Adicionar índices (tenant_id, ...)
        AdicionarIndices(mb);

        // Passo 7: Reescrever foreign keys com composite key (tenant_id, contrato_id)
        ReescreverForeignKeys(mb);
    }

    protected override void Down(MigrationBuilder mb)
    {
        // Reverte só em ambiente de teste (perda de dados aceitável)
        foreach (string tabela in TabelasTenantScoped.Reverse())
        {
            mb.DropColumn("tenant_id", tabela, "sgcf");
        }
    }

    private static readonly string[] TabelasTenantScoped =
    {
        "contrato", "parcela", "garantia", "evento_cronograma",
        "finimp_detail", "lei4131_detail", "refinimp_detail",
        "nce_detail", "capital_de_giro_detail", "fgi_detail",
        "garantia_cdb_cativo_detail", "garantia_sblc_detail",
        "garantia_aval_detail", "garantia_alienacao_fiduciaria_detail",
        "garantia_duplicatas_detail", "garantia_recebiveis_cartao_detail",
        "garantia_boleto_bancario_detail", "garantia_fgi_detail",
        "cotacao", "proposta", "economia_negociacao",
        "limite_banco", "limite_banco_historico", "garantia_exigida_limite",
        "instrumento_hedge", "posicao_snapshot",
        "alerta_vencimento", "alerta_exposicao_banco",
        "simulacao_antecipacao", "simulacao_contratacao", "cenario_simulacao",
        "ebitda_mensal", "snapshot_mensal_posicao",
        "lancamento_contabil", "plano_contas_gerencial",
        "parametro_sistema", "parametro_cotacao",
        "audit_log",
        // Tabelas criadas posteriormente (cockpit Fase 0/3) já nascem com tenant_id —
        // não entram aqui mas no SPEC da própria task do cockpit.
    };
}
```

### 2.1 Helpers `ReescreverUniqueConstraints`

Unique constraints existentes que viram composite com `tenant_id`:

```csharp
private static void ReescreverUniqueConstraints(MigrationBuilder mb)
{
    // Exemplo: contrato.numero_externo era único globalmente; vira único por tenant.
    mb.DropIndex(
        name: "ix_contrato_numero_externo",
        schema: "sgcf",
        table: "contrato");

    mb.CreateIndex(
        name: "ix_contrato_tenant_numero_externo",
        schema: "sgcf",
        table: "contrato",
        columns: new[] { "tenant_id", "numero_externo" },
        unique: true);

    // Cotação.codigo_interno idem
    mb.DropIndex(
        name: "ix_cotacao_codigo_interno",
        schema: "sgcf",
        table: "cotacao");

    mb.CreateIndex(
        name: "ix_cotacao_tenant_codigo_interno",
        schema: "sgcf",
        table: "cotacao",
        columns: new[] { "tenant_id", "codigo_interno" },
        unique: true);

    // EbitdaMensal: (ano, mes) → (tenant_id, ano, mes)
    mb.DropIndex(name: "ix_ebitda_ano_mes", schema: "sgcf", table: "ebitda_mensal");
    mb.CreateIndex(
        name: "ix_ebitda_tenant_ano_mes",
        schema: "sgcf",
        table: "ebitda_mensal",
        columns: new[] { "tenant_id", "ano", "mes" },
        unique: true);

    // ParametroSistema.chave deixa de ser globalmente único
    mb.DropIndex(name: "ix_parametro_sistema_chave", schema: "sgcf", table: "parametro_sistema");
    mb.CreateIndex(
        name: "ix_parametro_sistema_tenant_chave",
        schema: "sgcf",
        table: "parametro_sistema",
        columns: new[] { "tenant_id", "chave" },
        unique: true);

    // ... outros uniques afetados
}
```

### 2.2 Helpers `AdicionarIndices`

Índices não-unique de performance (queries de painel filtram por `tenant_id` + outros campos):

```csharp
private static void AdicionarIndices(MigrationBuilder mb)
{
    // contrato: tenant_id + status (queries de painel filtram ambos)
    mb.CreateIndex(
        name: "ix_contrato_tenant_status",
        schema: "sgcf",
        table: "contrato",
        columns: new[] { "tenant_id", "status" });

    // parcela: tenant_id + data_vencimento
    mb.CreateIndex(
        name: "ix_parcela_tenant_data_vencimento",
        schema: "sgcf",
        table: "parcela",
        columns: new[] { "tenant_id", "data_vencimento" });

    // audit_log: tenant_id + occurred_at
    mb.CreateIndex(
        name: "ix_audit_log_tenant_occurred_at",
        schema: "sgcf",
        table: "audit_log",
        columns: new[] { "tenant_id", "occurred_at" });

    // ... mais ~15 índices
}
```

### 2.3 Helpers `ReescreverForeignKeys`

Foreign keys composite garantem consistência `tenant_id`:

```csharp
private static void ReescreverForeignKeys(MigrationBuilder mb)
{
    // parcela referencia contrato — agora composite (tenant_id, contrato_id)
    mb.DropForeignKey(
        name: "fk_parcela_contrato",
        schema: "sgcf",
        table: "parcela");

    mb.AddForeignKey(
        name: "fk_parcela_contrato",
        schema: "sgcf",
        table: "parcela",
        columns: new[] { "tenant_id", "contrato_id" },
        principalSchema: "sgcf",
        principalTable: "contrato",
        principalColumns: new[] { "tenant_id", "id" },
        onDelete: ReferentialAction.Cascade);

    // Pré-requisito: contrato precisa ter unique constraint em (tenant_id, id).
    // Como id é PK, e (tenant_id, id) é único naturalmente, criar índice unique explícito:
    mb.CreateIndex(
        name: "ix_contrato_tenant_id_pk",
        schema: "sgcf",
        table: "contrato",
        columns: new[] { "tenant_id", "id" },
        unique: true);

    // Replicar para: garantia → contrato, evento_cronograma → contrato,
    // proposta → cotacao, economia_negociacao → cotacao,
    // limite_banco_historico → limite_banco, garantia_exigida_limite → limite_banco
}
```

---

## 3. EF Core Configurations atualizadas

Cada `*Configuration.cs` recebe configuração de `tenant_id`:

```csharp
internal sealed class ContratoConfiguration : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> b)
    {
        // ... existing config

        b.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
    }
}
```

---

## 4. Estratégia para produção (Proxys atual)

A Proxys ainda não tem dados em produção (sistema não lançado). Sem janela de manutenção necessária. Migration roda no deploy normal.

Para futuro: documentar em `docs/operacao/multi-tenancy.md` o procedimento canônico:

1. Backup completo.
2. Habilitar modo manutenção (banner no FE).
3. Rodar migration (até 5 min para 10 000 contratos).
4. Validar via query: `SELECT COUNT(*) FROM contrato WHERE tenant_id IS NULL;` deve retornar 0.
5. Desabilitar modo manutenção.

---

## 5. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Migration roda em base vazia (sem `Banco`) | OK — seed do tenant funciona; updates atualizam 0 linhas |
| Linha pré-existente com FK órfã (referencia entidade não-existente) | Falha no passo 7 (composite FK); precisa limpar dados antes |
| Migration falha no meio | Transação completa rola back (todos os passos em transaction implícita do EF) |
| Reaplicar migration | EF detecta que `tenant_id` já existe; passa direto |
| Tenant `proxys` já existe (seed manual) | `ON CONFLICT (id) DO NOTHING` ignora |

---

## 6. Critérios de Aceite

- [ ] Migration aplica em base de dev (200 contratos) em < 60 s.
- [ ] Pós-migração: `SELECT COUNT(*) WHERE tenant_id IS NULL = 0` em todas as tabelas tenant-scoped.
- [ ] Tenant `proxys` existe com UUID `00000000-0000-7000-8000-000000000001`.
- [ ] Unique constraints reescritos como composite com `tenant_id`.
- [ ] Foreign keys reescritos como composite onde aplicável.
- [ ] Índices `(tenant_id, ...)` criados nas chaves de consulta frequente.
- [ ] `dotnet test` passa após aplicar migration.
- [ ] Migration reverte em ambiente de teste (descarte aceitável).

---

## 7. Verificação

```bash
# Aplicar migration em base de teste
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Validar
psql sgcf_dev -c "SELECT COUNT(*) FROM sgcf.contrato WHERE tenant_id IS NULL"
# → 0

# Validar índices criados
psql sgcf_dev -c "\d sgcf.contrato"
# → deve listar ix_contrato_tenant_*

# Reverter (apenas em dev)
dotnet ef migrations remove --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
```

**Teste-chave (integração):**

```csharp
[Fact]
[Trait("Category", "Slow")]
public async Task BigBangMigration_aplica_e_preserva_dados()
{
    using PostgreSqlContainer pg = new PostgreSqlBuilder().Build();
    await pg.StartAsync();

    await AplicarMigrationsAteAntesDe("AddTenantIdToAllTables", pg);
    await SeedContratosLegado(pg, count: 100);
    await AplicarMigration("AddTenantIdToAllTables", pg);

    int contratosComTenant = await ContarContratosComTenantId(pg);
    contratosComTenant.Should().Be(100);

    int linhasNulo = await ContarTenantIdNulo(pg);
    linhasNulo.Should().Be(0);
}
```

---

## 8. Riscos e Mitigações

| Risco | Mitigação |
|-------|-----------|
| Migration demora muito em base grande | Testar em base sintética 10x do volume atual antes de produção |
| Unique constraints quebram em dados pré-existentes (duplicatas) | Dry-run em dev com snapshot de produção; relatório de conflitos antes do GA |
| FK composite não respeitada por dados legados inconsistentes | Validation step antes do passo 7: contar `JOIN` órfãos |
| Espaço em disco aumenta com novos índices | Estimar +15% no tamanho da base; alocar previamente |
| Rollback não recupera dados | Política: jamais reverter em produção sem backup confirmado |

---

## 9. Boundaries específicas

### 9.1 Always do
- Testar migration em base com volume sintético antes de produção.
- Reescrever unique constraints como composite com `tenant_id`.
- Adicionar índice `(tenant_id, ...)` antes de adicionar FK composite.

### 9.2 Ask first
- Pular reescrita de algum unique (manter global) — exige análise de negócio.
- Dropar coluna ou tabela legada durante a migration (separar em outra migration).

### 9.3 Never do
- Aplicar em produção sem backup verificado.
- Misturar mudança de schema com transformação de dados em migration separada (manter atômico).
- Usar `ON DELETE CASCADE` em FK composite sem confirmar comportamento desejado.

---

## 10. Arquivos esperados

- `src/Sgcf.Infrastructure/Migrations/<ts>_AddTenantIdToAllTables.cs` (arquivo grande, ~500 linhas)
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddTenantIdToAllTables.Designer.cs`
- ~30 atualizações em `src/Sgcf.Infrastructure/Persistence/Configurations/*.cs` (adicionar `tenant_id` config)
- `tests/Sgcf.Application.Tests/Migrations/BigBangMigrationTests.cs`
