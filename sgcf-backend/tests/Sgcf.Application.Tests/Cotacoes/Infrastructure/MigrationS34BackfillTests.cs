using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

/// <summary>
/// Testes de integração que validam o backfill da migration S34_SnapshotGarantiasContrato.
///
/// Estratégia (TC-01 a TC-06): aplica todas as migrations via <see cref="DatabaseFacade.MigrateAsync"/>
/// num container Testcontainers isolado e verifica as pós-condições estruturais (tabelas, índices, RLS).
///
/// Estratégia (TC-08): usa um container separado, aplica migrations até S33 via
/// <see cref="DatabaseFacadeExtensions.MigrateAsyncUpTo"/>, insere dados legados em
/// <c>limite_banco_garantia_exigida</c>, depois aplica S34 via o mesmo método e verifica
/// que o backfill criou revisões e vinculou os itens corretamente.
///
/// Nota de implementação: <see cref="CreateContextFor"/> configura explicitamente
/// <c>MigrationsHistoryTable("__EFMigrationsHistory", "public")</c> para garantir que
/// todos os contextos de teste leiam/escrevam o histórico de migrations no mesmo lugar.
/// Sem isso, o PostgreSQL pode resolver o schema implícito de modo diferente após
/// <c>InitialCreate</c> criar o schema <c>sgcf</c> no search_path.
///
/// SPEC §6.3, §8.3 (backfill test).
/// </summary>
[Trait("Category", "Slow")]
public sealed class MigrationS34BackfillTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-7000-8000-000000000034");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_s34_test")
        .WithUsername("sgcf")
        .WithPassword("sgcf_s34_test")
        .Build();

    private SgcfDbContext _context = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Fase 1: aplicar todas as migrations incluindo S34 via MigrateAsync padrão.
        // O seed de dados legados é injetado antes de S34 via trigger de seed SQL
        // (ver comentário no corpo do teste).
        _context = CreateContext();
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ── Testes de pós-condições estruturais ──────────────────────────────────

    /// <summary>
    /// TC-01: tabela garantia_exigida_revisao existe após S34.
    /// </summary>
    [Fact]
    public async Task S34_TabelaGarantiaExigidaRevisao_Existe()
    {
        int count = await QueryScalarAsync<int>(@"
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = 'sgcf'
              AND table_name = 'garantia_exigida_revisao'
        ");

        count.Should().Be(1, because: "a tabela garantia_exigida_revisao deve ser criada pela migration S34");
    }

    /// <summary>
    /// TC-02: tabela garantia_exigida_item existe e possui coluna revisao_id (não limite_banco_id).
    /// </summary>
    [Fact]
    public async Task S34_TabelaGarantiaExigidaItem_TemColunaRevisaoId()
    {
        int hasRevisaoId = await QueryScalarAsync<int>(@"
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'sgcf'
              AND table_name = 'garantia_exigida_item'
              AND column_name = 'revisao_id'
        ");

        int hasLimiteBancoId = await QueryScalarAsync<int>(@"
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'sgcf'
              AND table_name = 'garantia_exigida_item'
              AND column_name = 'limite_banco_id'
        ");

        hasRevisaoId.Should().Be(1, because: "a coluna revisao_id deve existir em garantia_exigida_item");
        hasLimiteBancoId.Should().Be(0, because: "a coluna limite_banco_id foi removida da tabela de itens");
    }

    /// <summary>
    /// TC-03: tabela limite_banco_garantia_exigida não deve mais existir após S34.
    /// </summary>
    [Fact]
    public async Task S34_TabelaAntiga_NaoExiste()
    {
        int count = await QueryScalarAsync<int>(@"
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = 'sgcf'
              AND table_name = 'limite_banco_garantia_exigida'
        ");

        count.Should().Be(0, because: "a tabela legada deve ter sido removida pela migration S34");
    }

    /// <summary>
    /// TC-04: tabela contrato possui as 3 novas colunas de rastreabilidade.
    /// </summary>
    [Fact]
    public async Task S34_Contrato_TemTresNovasColunas()
    {
        string[] expectedColumns = ["limite_banco_id", "limite_global_banco_id", "garantias_exigidas_revisao_id"];

        foreach (string column in expectedColumns)
        {
            int count = await QueryScalarAsync<int>($@"
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = 'sgcf'
                  AND table_name = 'contrato'
                  AND column_name = '{column}'
            ");

            count.Should().Be(1, because: $"a coluna {column} deve existir em contrato após S34");
        }
    }

    /// <summary>
    /// TC-05: índice único parcial para SLB-01 (uma revisão vigente por limite_banco_id).
    /// </summary>
    [Fact]
    public async Task S34_IndiceUnicoParciaal_RevisaoVigente_Existe()
    {
        int count = await QueryScalarAsync<int>(@"
            SELECT COUNT(*) FROM pg_indexes
            WHERE schemaname = 'sgcf'
              AND tablename = 'garantia_exigida_revisao'
              AND indexname = 'ux_garantia_exigida_revisao_vigente'
        ");

        count.Should().Be(1, because: "o índice único parcial ux_garantia_exigida_revisao_vigente deve existir (SLB-01)");
    }

    /// <summary>
    /// TC-06: RLS está habilitada na tabela garantia_exigida_revisao.
    /// </summary>
    [Fact]
    public async Task S34_Rls_HabilitadaEmGarantiaExigidaRevisao()
    {
        int count = await QueryScalarAsync<int>(@"
            SELECT COUNT(*) FROM pg_class
            WHERE relname = 'garantia_exigida_revisao'
              AND relrowsecurity = true
        ");

        count.Should().Be(1, because: "RLS deve estar habilitada em garantia_exigida_revisao");
    }

    // ── Teste de backfill com dados reais ────────────────────────────────────

    /// <summary>
    /// TC-07: backfill in-migration cria revisões iniciais e vincula itens corretamente.
    ///
    /// Estratégia: dado que a migration S34 já foi aplicada via InitializeAsync,
    /// este teste usa um banco limpo (sem dados pre-S34) e verifica o fluxo
    /// via INSERT direto em garantia_exigida_revisao + garantia_exigida_item,
    /// simulando o que a aplicação fará após S34 estar aplicada.
    ///
    /// O cenário de backfill real (dados legados) é exercitado pelo test TC-08
    /// que usa uma instância isolada com seed pré-S34 via SQL.
    /// </summary>
    [Fact]
    public async Task S34_GarantiaExigidaItem_RevisaoId_NaoNulo_PosInsercao()
    {
        // Arrange: inserir um banco e um limite diretamente via SQL
        Guid bancoId = Guid.NewGuid();
        Guid limiteId = Guid.NewGuid();

        // padrao_antecipacao foi movido de banco_config para limite_banco pela migration S32.
        await _context.Database.ExecuteSqlAsync($@"
            INSERT INTO sgcf.banco_config
                (id, codigo_compe, razao_social, apelido,
                 aceita_liquidacao_total, aceita_liquidacao_parcial, exige_anuencia_expressa,
                 exige_parcela_inteira, aceita_refinimp, aviso_previo_min_dias_uteis,
                 created_at, updated_at)
            VALUES ({bancoId}, 'T07', 'Banco TC-07', 'TC07',
                true, true, false, false, true, 0,
                '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
            ON CONFLICT DO NOTHING
        ");

        await _context.Database.ExecuteSqlAsync($@"
            INSERT INTO sgcf.limite_banco
                (id, tenant_id, banco_id, modalidade, valor_limite_brl, valor_utilizado_brl,
                 data_vigencia_inicio, created_at, updated_at)
            VALUES ({limiteId}, {TenantId}, {bancoId}, 'Finimp', 1000000.00, 0.00,
                '2026-01-01', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
        ");

        // Inserir revisão e item diretamente — verifica que o schema pós-S34 aceita os dados
        Guid revisaoId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();

        await _context.Database.ExecuteSqlAsync($@"
            INSERT INTO sgcf.garantia_exigida_revisao
                (id, tenant_id, limite_banco_id, vigencia_inicio, vigencia_fim,
                 registrado_em, motivo, observacoes, created_at, updated_at)
            VALUES ({revisaoId}, {TenantId}, {limiteId}, '2026-01-01 00:00:00+00', NULL,
                '2026-01-01 00:00:00+00', 'Teste TC-07', NULL,
                '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
        ");

        await _context.Database.ExecuteSqlAsync($@"
            INSERT INTO sgcf.garantia_exigida_item
                (id, revisao_id, tipo, percentual_sobre_limite, valor_fixo_brl,
                 obrigatoria, observacoes, created_at, updated_at)
            VALUES ({itemId}, {revisaoId}, 1, 30.0, NULL,
                true, NULL,
                '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
        ");

        // Assert: item com revisao_id não nulo
        int nulos = await QueryScalarAsync<int>($@"
            SELECT COUNT(*) FROM sgcf.garantia_exigida_item
            WHERE revisao_id IS NULL
              AND id = '{itemId}'
        ");

        nulos.Should().Be(0, because: "revisao_id não pode ser nulo após S34");

        // Assert: FK funcional — a revisão existe para o item
        int revisoes = await QueryScalarAsync<int>($@"
            SELECT COUNT(*) FROM sgcf.garantia_exigida_revisao
            WHERE id = '{revisaoId}'
              AND limite_banco_id = '{limiteId}'
        ");

        revisoes.Should().Be(1);
    }

    /// <summary>
    /// TC-08: backfill real — dados inseridos no estado pré-S34 (limite_banco_garantia_exigida)
    /// em um banco separado; verifica que após S34 revisões são criadas e itens estão vinculados.
    ///
    /// Esta variante usa uma segunda instância do container com migrations até S33,
    /// insere seed, depois aplica S34 via migrator.
    /// </summary>
    [Fact]
    public async Task S34_Backfill_DadosPreS34_CriaRevisoesEVinculaItens()
    {
        // Arrange: subir container separado só para este cenário
        await using PostgreSqlContainer backfillContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("sgcf_backfill_test")
            .WithUsername("sgcf")
            .WithPassword("sgcf_backfill_test")
            .Build();

        await backfillContainer.StartAsync();

        // Aplicar apenas até S33 (exclusive S34)
        await using SgcfDbContext preS34Ctx = CreateContextFor(backfillContainer.GetConnectionString());
        await preS34Ctx.Database.MigrateAsyncUpTo("20260524014035_S33_LimiteGlobalBanco");

        // Seed pré-S34: 3 LimiteBanco, cada um com 2 itens em limite_banco_garantia_exigida
        Guid tenantId = Guid.Parse("00000000-0000-7000-8000-000000000034");
        Guid bancoId = Guid.NewGuid();

        // padrao_antecipacao foi movido de banco_config para limite_banco pela migration S32.
        // Após S32, banco_config não possui mais essa coluna.
        await preS34Ctx.Database.ExecuteSqlAsync($@"
            INSERT INTO sgcf.banco_config
                (id, codigo_compe, razao_social, apelido,
                 aceita_liquidacao_total, aceita_liquidacao_parcial, exige_anuencia_expressa,
                 exige_parcela_inteira, aceita_refinimp, aviso_previo_min_dias_uteis,
                 created_at, updated_at)
            VALUES ({bancoId}, '001', 'Banco Backfill', 'BF1',
                true, true, false, false, true, 0,
                '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
        ");

        Guid[] limiteIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        string[] modalidades = ["Finimp", "Nce", "Lei4131"];

        for (int i = 0; i < 3; i++)
        {
            await preS34Ctx.Database.ExecuteSqlAsync($@"
                INSERT INTO sgcf.limite_banco
                    (id, tenant_id, banco_id, modalidade, valor_limite_brl, valor_utilizado_brl,
                     data_vigencia_inicio, created_at, updated_at)
                VALUES ({limiteIds[i]}, {tenantId}, {bancoId}, {modalidades[i]}, 1000000.00, 0.00,
                    '2026-01-01', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
            ");

            // 2 itens por limite: tipo 1 (Fcc) e tipo 2 (Cdb)
            for (int tipo = 1; tipo <= 2; tipo++)
            {
                await preS34Ctx.Database.ExecuteSqlAsync($@"
                    INSERT INTO sgcf.limite_banco_garantia_exigida
                        (id, limite_banco_id, tipo, percentual_sobre_limite, valor_fixo_brl,
                         obrigatoria, observacoes, created_at, updated_at)
                    VALUES ({Guid.NewGuid()}, {limiteIds[i]}, {tipo}, 30.0, NULL,
                        true, NULL,
                        '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00')
                ");
            }
        }

        // Act: aplicar S34 via novo contexto (mesmo IMigrator path que aplicou S33).
        // CreateContextFor configura MigrationsHistoryTable explicitamente em "public",
        // garantindo que ambos os contextos leiam e escrevam no mesmo public.__EFMigrationsHistory.
        await using SgcfDbContext postSeedCtx = CreateContextFor(backfillContainer.GetConnectionString());
        await postSeedCtx.Database.MigrateAsyncUpTo("20260525154536_S34_SnapshotGarantiasContrato");

        // Assert: 3 revisões criadas (uma por LimiteBanco)
        int revisoes = await QueryScalarForConnectionAsync<int>(
            backfillContainer.GetConnectionString(),
            "SELECT COUNT(*) FROM sgcf.garantia_exigida_revisao");

        revisoes.Should().Be(3, because: "cada LimiteBanco com itens deve ter uma revisão inicial");

        // Assert: 6 itens vinculados (2 por limite × 3 limites), nenhum com revisao_id nulo
        int itensSemRevisao = await QueryScalarForConnectionAsync<int>(
            backfillContainer.GetConnectionString(),
            "SELECT COUNT(*) FROM sgcf.garantia_exigida_item WHERE revisao_id = '00000000-0000-0000-0000-000000000000'");

        itensSemRevisao.Should().Be(0, because: "nenhum item deve ter revisao_id não populado após backfill");

        int totalItens = await QueryScalarForConnectionAsync<int>(
            backfillContainer.GetConnectionString(),
            "SELECT COUNT(*) FROM sgcf.garantia_exigida_item");

        totalItens.Should().Be(6, because: "todos os 6 itens legados devem ter sido migrados para garantia_exigida_item");

        // Assert: tabela antiga não existe
        int tabelaAntiga = await QueryScalarForConnectionAsync<int>(
            backfillContainer.GetConnectionString(),
            @"SELECT COUNT(*) FROM information_schema.tables
              WHERE table_schema = 'sgcf' AND table_name = 'limite_banco_garantia_exigida'");

        tabelaAntiga.Should().Be(0, because: "a tabela legada deve ser removida pela migration S34");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private SgcfDbContext CreateContext() =>
        CreateContextFor(_container.GetConnectionString());

    private static SgcfDbContext CreateContextFor(string connectionString)
    {
        ITenantContext tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.IsResolved.Returns(true);
        tenantCtx.TenantId.Returns(TenantId);
        tenantCtx.TenantIdOrDefault.Returns(TenantId);

        // MigrationsHistoryTable especificado explicitamente em "public" para garantir que
        // todos os contextos de teste leiam e escrevam o histórico de migrations no mesmo lugar.
        // Por padrão, o Npgsql pode resolver o schema a partir do search_path da conexão,
        // que muda após InitialCreate criar o schema "sgcf".
        DbContextOptions<SgcfDbContext> opts = new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNodaTime();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            })
            .AddInterceptors(new TenantSaveInterceptor(tenantCtx, NullLogger<TenantSaveInterceptor>.Instance))
            .Options;

        return new SgcfDbContext(opts, tenantCtx);
    }

    /// <summary>Executa scalar SQL no contexto principal e retorna o valor tipado.</summary>
    private async Task<T> QueryScalarAsync<T>(string sql) where T : struct
    {
        await using var conn = new Npgsql.NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        object? result = await cmd.ExecuteScalarAsync();
        return result is T typed ? typed : (T)Convert.ChangeType(result!, typeof(T), CultureInfo.InvariantCulture);
    }

    /// <summary>Executa scalar SQL em uma connection string arbitrária.</summary>
    private static async Task<T> QueryScalarForConnectionAsync<T>(string connectionString, string sql) where T : struct
    {
        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        object? result = await cmd.ExecuteScalarAsync();
        return result is T typed ? typed : (T)Convert.ChangeType(result!, typeof(T), CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Extensão para aplicar migrations até uma versão específica por nome de migração.
/// Simula <c>dotnet ef database update {migrationName}</c> programaticamente.
/// </summary>
internal static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Aplica todas as migrations cujo MigrationId seja &lt;= <paramref name="targetMigrationId"/>.
    /// </summary>
    public static async Task MigrateAsyncUpTo(
        this DatabaseFacade database,
        string targetMigrationId)
    {
        IMigrator migrator = database.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigrationId);
    }
}
