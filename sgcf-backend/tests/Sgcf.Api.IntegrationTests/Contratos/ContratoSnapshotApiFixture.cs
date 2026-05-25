using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NSubstitute;
using Sgcf.Api.IntegrationTests.TestAuth;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Contratos;

/// <summary>
/// Fixture isolada para os testes de snapshot de garantias exigidas (T2.3).
/// Usa container PostgreSQL dedicado para não interferir com outras fixtures.
/// Clock fixo em 2026-05-20 — data de referência para vigência de limites.
/// </summary>
public sealed class ContratoSnapshotApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_snapshot_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_snapshot_e2e")
        .Build();

    /// <summary>Instante fixo: 2026-05-20 10:00 UTC.</summary>
    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 20, 10, 0);

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
                        npgsql =>
                        {
                            npgsql.UseNodaTime();
                            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        }));

                services.RemoveAll<IClock>();
                services.AddSingleton(clockFake);
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        SgcfDbContext ctx = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();
        await TenantTestSeeder.SeedProxysAsync(Factory.Services);

        // Seed CotacaoFx PTAX D-1 para a data de abertura 2026-05-20.
        // O conversor de cotação depende desse registro para calcular o valor em BRL.
        ICotacaoFxRepository cotacaoFxRepo =
            scope.ServiceProvider.GetRequiredService<ICotacaoFxRepository>();
        CotacaoFx ptax = CotacaoFx.Criar(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            new Money(5.15m, Moeda.Brl),
            new Money(5.20m, Moeda.Brl),
            fonte: "BACEN-seed-snapshot-e2e",
            momento: InstanteFixo - Duration.FromHours(13)); // 2026-05-19T21:00Z
        await cotacaoFxRepo.UpsertAsync(ptax);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>Cria HttpClient autenticado com Bearer token de teste.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }
}

[CollectionDefinition("ContratoSnapshotApi")]
#pragma warning disable CA1711
public sealed class ContratoSnapshotApiGroup : ICollectionFixture<ContratoSnapshotApiFixture> { }
#pragma warning restore CA1711
