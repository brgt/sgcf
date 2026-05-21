using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NSubstitute;
using Sgcf.Api.IntegrationTests.TestAuth;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Fixture exclusiva do módulo LimitesBanco — container PostgreSQL isolado para não
/// interferir na fixture de Cotações.
/// Marcada [Slow] — exclua do loop rápido com --filter "Category!=Slow".
/// </summary>
public sealed class LimitesBancoApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_limites_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_limites_e2e")
        .Build();

    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 16, 12, 0);

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        IClock clockFake = Substitute.For<IClock>();
        clockFake.GetCurrentInstant().Returns(InstanteFixo);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SgcfDbContext>>();
                services.RemoveAll<SgcfDbContext>();

                services.AddDbContext<SgcfDbContext>(opts =>
                    opts.UseNpgsql(
                        _db.GetConnectionString(),
                        npgsql => npgsql.UseNodaTime()));

                services.RemoveAll<IClock>();
                services.AddSingleton(clockFake);
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        SgcfDbContext ctx = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();
        await TenantTestSeeder.SeedProxysAsync(Factory.Services);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }
}

[CollectionDefinition("LimitesBancoApi")]
#pragma warning disable CA1711
public sealed class LimitesBancoApiGroup : ICollectionFixture<LimitesBancoApiFixture> { }
#pragma warning restore CA1711
