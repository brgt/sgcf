using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Painel.Infrastructure;

/// <summary>
/// Fixture compartilhada para testes de integração do módulo Painel.
/// Sobe um container PostgreSQL via Testcontainers e aplica todas as migrations.
/// Testes usando esta fixture devem ser marcados com [Trait("Category", "Slow")].
/// </summary>
public sealed class PainelDbFixture : IAsyncLifetime
{
    /// <summary>Tenant fixo de teste — todas as entidades criadas pertencem a ele.</summary>
    public static readonly Guid TestTenantId = Guid.Parse("00000000-0000-7000-8000-000000000099");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test_painel")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    private readonly ITenantContext _tenantCtx = CreateResolvedTestContext();

    /// <summary>Clock fixo em 2026-05-21 09:00 UTC para controle determinístico de datas.</summary>
    public IClock Clock { get; } = CreateFixedClock();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ServiceCollection services = new();
        services.AddSingleton(_tenantCtx);
        services.AddDbContext<SgcfDbContext>((sp, opts) =>
            opts.UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.UseNodaTime())
            .AddInterceptors(
                new TenantSaveInterceptor(
                    sp.GetRequiredService<ITenantContext>(),
                    NullLogger<TenantSaveInterceptor>.Instance)));

        ServiceProvider sp2 = services.BuildServiceProvider();
        SgcfDbContext ctx = sp2.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();
        await ctx.DisposeAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Cria um novo DbContext com a mesma connection string e o mesmo tenant de teste.
    /// Cada teste deve usar seu próprio contexto para evitar que o ChangeTracker
    /// compartilhado polua queries subsequentes.
    /// </summary>
    public SgcfDbContext CreateFreshContext()
    {
        DbContextOptions<SgcfDbContext> opts = new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.UseNodaTime())
            .AddInterceptors(new TenantSaveInterceptor(
                _tenantCtx, NullLogger<TenantSaveInterceptor>.Instance))
            .Options;
        return new SgcfDbContext(opts, _tenantCtx);
    }

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
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 21, 9, 0));
        return clock;
    }
}
