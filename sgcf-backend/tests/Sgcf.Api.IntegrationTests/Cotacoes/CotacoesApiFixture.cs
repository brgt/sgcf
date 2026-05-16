using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NSubstitute;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Fixture compartilhada que sobe a API completa via WebApplicationFactory
/// apontando para um container PostgreSQL descartável.
/// Marcada [Slow] — exclua do loop rápido com --filter "Category!=Slow".
/// </summary>
public sealed class CotacoesApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_e2e")
        .Build();

    /// <summary>Instante fixo usado como "agora" em todos os testes desta fixture.</summary>
    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 16, 12, 0);

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // Relógio determinístico: garante que testes não dependem do horário real
        IClock clockFake = Substitute.For<IClock>();
        clockFake.GetCurrentInstant().Returns(InstanteFixo);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Substitui o DbContext real pelo de teste (mesmo schema, container diferente)
                services.RemoveAll<DbContextOptions<SgcfDbContext>>();
                services.RemoveAll<SgcfDbContext>();

                services.AddDbContext<SgcfDbContext>(opts =>
                    opts.UseNpgsql(
                        _db.GetConnectionString(),
                        npgsql => npgsql.UseNodaTime()));

                // Substitui IClock por relógio fixo determinístico
                services.RemoveAll<IClock>();
                services.AddSingleton(clockFake);
            });
        });

        // Aplicar migrations com o contexto do container
        using IServiceScope scope = Factory.Services.CreateScope();
        SgcfDbContext ctx = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>
    /// Cria HttpClient com header Authorization preenchido.
    /// Em modo de desenvolvimento, o middleware aceita qualquer Bearer token.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }
}

[CollectionDefinition("CotacoesApi")]
#pragma warning disable CA1711 // xUnit requer o sufixo "Collection" neste atributo; não é um sufixo de Framework
public sealed class CotacoesApiGroup : ICollectionFixture<CotacoesApiFixture> { }
#pragma warning restore CA1711
