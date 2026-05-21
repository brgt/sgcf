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

namespace Sgcf.Api.IntegrationTests.Alertas;

/// <summary>
/// Fixture compartilhada que sobe a API completa via WebApplicationFactory
/// apontando para um container PostgreSQL descartável.
/// Marcada [Slow] — exclua do loop rápido com <c>--filter "Category!=Slow"</c>.
/// </summary>
public sealed class AlertasApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_alertas_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_alertas_e2e")
        .Build();

    /// <summary>Instante fixo injetado via IClock fake para testes determinísticos.</summary>
    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 10, 0);

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

    /// <summary>Cria HttpClient com header Authorization preenchido para o tenant padrão (Proxys).</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }

    /// <summary>
    /// Cria HttpClient autenticado para o tenant informado via <c>X-Test-Tenant-Id</c>.
    /// </summary>
    public HttpClient CreateClientForTenant(Guid tenantId)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantIdHeader, tenantId.ToString());
        return client;
    }
}

[CollectionDefinition("AlertasApi")]
#pragma warning disable CA1711 // xUnit requer o sufixo "Collection" neste atributo; não é um sufixo de Framework
public sealed class AlertasApiGroup : ICollectionFixture<AlertasApiFixture> { }
#pragma warning restore CA1711
