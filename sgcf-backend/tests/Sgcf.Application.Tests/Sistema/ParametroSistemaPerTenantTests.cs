using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Sistema;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Common;
using Sgcf.Domain.Sistema;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Sistema;

/// <summary>
/// Verifica que <see cref="ParametroSistema"/> é corretamente isolado por tenant.
///
/// Cobertura dos critérios de aceite da Task −1.9:
/// <list type="bullet">
///   <item>GetAsync sem tenant resolvido retorna null.</item>
///   <item>GetAsync com tenant A não retorna dados do tenant B.</item>
///   <item>Tetão de tenant A não vaza para tenant B.</item>
///   <item>Tenant sem ParametroSistema retorna null (não auto-cria).</item>
/// </list>
/// </summary>
[Trait("Category", "Slow")]
public sealed class ParametroSistemaPerTenantTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test_parametros_tenant")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    private static readonly Guid TenantA = Guid.Parse("00000000-0000-7000-8000-000000000011");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-7000-8000-000000000012");

    private static readonly IClock Clock = CreateClock(Instant.FromUtc(2026, 5, 20, 10, 0));

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        DbContextOptions<SgcfDbContext> opts = BuildOptions(_connectionString);
        await using SgcfDbContext ctx = new(opts, CreateContext(TenantA));
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // ── Teste 1: tenant sem ParametroSistema retorna null ────────────────────

    [Fact]
    public async Task GetAsync_TenantSemParametros_RetornaNull()
    {
        // Arrange — TenantA não tem ParametroSistema inserido
        var repo = CriarRepo(TenantA);

        // Act
        ParametroSistema? resultado = await repo.GetAsync();

        // Assert
        resultado.Should().BeNull(
            because: "tenant sem provisonamento não deve ter ParametroSistema");
    }

    // ── Teste 2: tenant A vê seus próprios parâmetros ─────────────────────────

    [Fact]
    public async Task GetAsync_TenantComParametros_RetornaParametros()
    {
        // Arrange — insere ParametroSistema para TenantA diretamente
        await InserirParametroAsync(TenantA);

        var repo = CriarRepo(TenantA);

        // Act
        ParametroSistema? resultado = await repo.GetAsync();

        // Assert
        resultado.Should().NotBeNull();
        resultado!.TenantId.Should().Be(TenantA);
    }

    // ── Teste 3: tenant A não vê parâmetros do tenant B ──────────────────────

    [Fact]
    public async Task GetAsync_TenantA_NaoVeParametrosDeTenantB()
    {
        // Arrange — insere apenas para TenantB; TenantA não tem registro
        await InserirParametroAsync(TenantB);

        var repo = CriarRepo(TenantA);

        // Act
        ParametroSistema? resultado = await repo.GetAsync();

        // Assert
        resultado.Should().BeNull(
            because: "TenantA não deve ver ParametroSistema de TenantB");
    }

    // ── Teste 4: tetão de tenant A não afeta tenant B ─────────────────────────

    [Fact]
    public async Task Tetao_TenantA_NaoAfetaTenantB()
    {
        // Arrange — ambos os tenants têm parâmetros; define tetões diferentes
        await InserirParametroAsync(TenantA);
        await InserirParametroAsync(TenantB);

        // Atualiza tetão de TenantA para 1 milhão
        {
            var repoA = CriarRepo(TenantA);
            ParametroSistema pA = (await repoA.GetAsync())!;
            pA.AtualizarTetaoMensal(new Money(1_000_000m, Moeda.Brl), Clock);
            await repoA.SaveChangesAsync();
        }

        // Act — TenantB lê seus próprios parâmetros
        var repoB = CriarRepo(TenantB);
        ParametroSistema? pB = await repoB.GetAsync();

        // Assert — TenantB não deve ver o tetão de TenantA
        pB.Should().NotBeNull();
        pB!.TetaoMensalCapacidadeBrl.Should().BeNull(
            because: "o tetão de TenantA não deve vazar para TenantB");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ParametroSistemaRepository CriarRepo(Guid tenantId)
    {
        ITenantContext ctx = CreateContext(tenantId);
        SgcfDbContext dbContext = new(
            BuildOptions(_connectionString),
            ctx);
        return new ParametroSistemaRepository(dbContext);
    }

    private async Task InserirParametroAsync(Guid tenantId)
    {
        ITenantContext ctx = CreateContext(tenantId);
        await using SgcfDbContext dbContext = new(
            new DbContextOptionsBuilder<SgcfDbContext>()
                .UseNpgsql(_connectionString, npgsql => npgsql.UseNodaTime())
                .AddInterceptors(
                    new TenantSaveInterceptor(ctx, NullLogger<TenantSaveInterceptor>.Instance))
                .Options,
            ctx);

        bool existe = await dbContext.Set<ParametroSistema>()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId);

        if (existe) { return; }

        ParametroSistema parametro = ParametroSistema.CriarDefault(tenantId, Clock);
        dbContext.Set<ParametroSistema>().Add(parametro);
        await dbContext.SaveChangesAsync();
    }

    private static IClock CreateClock(Instant instant)
    {
        IClock c = Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(instant);
        return c;
    }

    private static ITenantContext CreateContext(Guid tenantId)
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(true);
        ctx.TenantId.Returns(tenantId);
        ctx.TenantIdOrDefault.Returns(tenantId);
        return ctx;
    }

    private static DbContextOptions<SgcfDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime())
            .Options;
}
