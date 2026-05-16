using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NSubstitute;
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
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    public SgcfDbContext Context { get; private set; } = default!;
    public IClock Clock { get; } = CreateFixedClock();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ServiceCollection services = new();
        services.AddDbContext<SgcfDbContext>(opts =>
            opts.UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.UseNodaTime()));

        ServiceProvider sp = services.BuildServiceProvider();
        Context = sp.GetRequiredService<SgcfDbContext>();
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Cria novo contexto com mesma connection string (útil para leituras isoladas).</summary>
    public SgcfDbContext CreateFreshContext()
    {
        DbContextOptions<SgcfDbContext> opts = new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.UseNodaTime())
            .Options;
        return new SgcfDbContext(opts);
    }

    private static IClock CreateFixedClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 16, 9, 0));
        return clock;
    }
}
