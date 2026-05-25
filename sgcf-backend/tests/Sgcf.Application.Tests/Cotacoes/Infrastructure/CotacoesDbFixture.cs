using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

/// <summary>
/// Fixture compartilhada para testes de integração do módulo de Cotações.
/// Sobe um container PostgreSQL via Testcontainers e aplica migrations.
/// Marcados com [Trait("Category", "Slow")] para excluir do loop rápido.
/// </summary>
public sealed class CotacoesDbFixture : IAsyncLifetime
{
    // Tenant fixo de teste — todas as entidades criadas neste fixture pertencem a ele.
    public static readonly Guid TestTenantId = Guid.Parse("00000000-0000-7000-8000-000000000099");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    // Contexto resolvido compartilhado — IsResolved=true para que o EF query filter
    // e o TenantSaveInterceptor funcionem como em produção.
    private readonly ITenantContext _tenantCtx = CreateResolvedTestContext();

    public SgcfDbContext Context { get; private set; } = default!;
    public IClock Clock { get; } = CreateFixedClock();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ServiceCollection services = new();
        services.AddSingleton(_tenantCtx);
        services.AddDbContext<SgcfDbContext>((sp, opts) =>
            opts.UseNpgsql(
                _container.GetConnectionString(),
                npgsql =>
                {
                    npgsql.UseNodaTime();
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                })
            .AddInterceptors(
                new TenantSaveInterceptor(
                    sp.GetRequiredService<ITenantContext>(),
                    NullLogger<TenantSaveInterceptor>.Instance)));

        ServiceProvider sp2 = services.BuildServiceProvider();
        Context = sp2.GetRequiredService<SgcfDbContext>();
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Cria novo contexto com mesma connection string e mesmo tenant de teste (útil para leituras isoladas).</summary>
    public SgcfDbContext CreateFreshContext()
    {
        DbContextOptions<SgcfDbContext> opts = new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql =>
            {
                npgsql.UseNodaTime();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            })
            .AddInterceptors(new TenantSaveInterceptor(
                _tenantCtx, NullLogger<TenantSaveInterceptor>.Instance))
            .Options;
        return new SgcfDbContext(opts, _tenantCtx);
    }

    /// <summary>
    /// Cria ITenantContext resolvido com tenant de teste fixo.
    /// IsResolved=true ativa o global query filter e o TenantSaveInterceptor,
    /// garantindo que os testes exercitem o mesmo caminho de produção.
    /// </summary>
    private static ITenantContext CreateResolvedTestContext()
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(true);
        ctx.TenantId.Returns(TestTenantId);
        ctx.TenantIdOrDefault.Returns(TestTenantId);
        return ctx;
    }

    private static IClock CreateFixedClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 16, 9, 0));
        return clock;
    }
}
