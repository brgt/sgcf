using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Sistema;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Sistema;

/// <summary>
/// Verifica que concurrent reads para um tenant existente não causam problemas.
///
/// Com a migração para per-tenant, não existe mais lógica get-or-create — o
/// provisionamento é responsável por criar o registro antes de qualquer operação.
/// Este teste valida que:
/// <list type="bullet">
///   <item>Leituras concorrentes de um tenant provisionado retornam o mesmo registro.</item>
///   <item>Leituras de tenant não provisionado retornam null (não criam duplicatas).</item>
/// </list>
/// </summary>
[Trait("Category", "Slow")]
public sealed class ParametroSistemaRepositoryRaceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test_sistema_race")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    // Tenant fixo de teste.
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-7000-8000-000000000099");

    private static readonly IClock Clock = CreateClock(Instant.FromUtc(2026, 5, 20, 10, 0));

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Aplica migrations e semeia o ParametroSistema para o tenant de teste.
        DbContextOptions<SgcfDbContext> opts = BuildOptions(_connectionString);
        ITenantContext ctx = CreateResolvedTestContext(TestTenantId);
        await using SgcfDbContext seedCtx = new(
            new DbContextOptionsBuilder<SgcfDbContext>()
                .UseNpgsql(_connectionString, npgsql => npgsql.UseNodaTime())
                .AddInterceptors(new TenantSaveInterceptor(ctx, NullLogger<TenantSaveInterceptor>.Instance))
                .Options,
            ctx);

        await seedCtx.Database.MigrateAsync();

        ParametroSistema parametro = ParametroSistema.CriarDefault(TestTenantId, Clock);
        seedCtx.Set<ParametroSistema>().Add(parametro);
        await seedCtx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// 5 chamadas concorrentes a GetAsync para um tenant provisionado devem:
    /// - Todas retornar um <see cref="ParametroSistema"/> não-nulo.
    /// - Todas apontarem para o mesmo registro (mesmo Id).
    /// </summary>
    [Fact]
    public async Task GetAsync_Concorrente_RetornaMesmoRegistro()
    {
        // Arrange — 5 contextos independentes simulando 5 requests simultâneas
        const int concorrentes = 5;

        // Act — dispara todas as leituras ao mesmo tempo
        ParametroSistema?[] resultados = await Task.WhenAll(
            Enumerable.Range(0, concorrentes).Select(_ => InvocarGetAsync()));

        // Assert — todas as chamadas retornaram um resultado
        resultados.Should().AllSatisfy(r =>
            r.Should().NotBeNull(because: "tenant provisionado deve ter ParametroSistema"));

        // Todos os resultados apontam para o mesmo registro (mesmo Id)
        Guid primeiroId = resultados[0]!.Id;
        resultados.Should().AllSatisfy(r =>
            r!.Id.Should().Be(primeiroId,
                because: "leituras concorrentes devem retornar o mesmo registro"));

        // Verifica no banco que há exatamente 1 linha para o tenant de teste
        await using SgcfDbContext ctxVerificacao = new(BuildOptions(_connectionString), CreateResolvedTestContext(TestTenantId));
        int totalLinhas = await ctxVerificacao.Set<ParametroSistema>().CountAsync();
        totalLinhas.Should().Be(1,
            because: "leituras concorrentes não devem criar registros extras");
    }

    /// <summary>
    /// GetAsync para tenant não provisionado deve retornar null — não criar registro.
    /// </summary>
    [Fact]
    public async Task GetAsync_TenantNaoProvisionado_RetornaNull()
    {
        // Arrange — tenant diferente do que foi semeado no InitializeAsync
        Guid tenantNaoProvisionado = Guid.Parse("00000000-0000-7000-8000-000000000088");
        var repo = CriarRepo(tenantNaoProvisionado);

        // Act
        ParametroSistema? resultado = await repo.GetAsync();

        // Assert
        resultado.Should().BeNull(
            because: "tenant não provisionado não deve ter ParametroSistema");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ParametroSistema?> InvocarGetAsync()
    {
        var repo = CriarRepo(TestTenantId);
        return await repo.GetAsync();
    }

    private ParametroSistemaRepository CriarRepo(Guid tenantId)
    {
        ITenantContext tenantCtx = CreateResolvedTestContext(tenantId);
        SgcfDbContext ctx = new(BuildOptions(_connectionString), tenantCtx);
        return new ParametroSistemaRepository(ctx);
    }

    private static DbContextOptions<SgcfDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime())
            .Options;

    private static IClock CreateClock(Instant instant)
    {
        IClock c = Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(instant);
        return c;
    }

    /// <summary>
    /// Cria ITenantContext resolvido com tenant de teste fixo.
    /// IsResolved=true ativa o global query filter e o TenantSaveInterceptor.
    /// </summary>
    private static ITenantContext CreateResolvedTestContext(Guid tenantId)
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(true);
        ctx.TenantId.Returns(tenantId);
        ctx.TenantIdOrDefault.Returns(tenantId);
        return ctx;
    }
}
