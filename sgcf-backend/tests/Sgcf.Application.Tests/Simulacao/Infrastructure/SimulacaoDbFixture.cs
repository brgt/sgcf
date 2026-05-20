using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NSubstitute;
using Sgcf.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Infrastructure;

/// <summary>
/// Fixture compartilhada para testes de integração do módulo Simulação.
/// Sobe um container PostgreSQL via Testcontainers e aplica todas as migrations.
/// Testes usando esta fixture devem ser marcados com [Trait("Category", "Slow")].
/// </summary>
public sealed class SimulacaoDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test_simulacao")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    // Context é privado para forçar testes a usar CreateFreshContext().
    // Um contexto compartilhado e mutável entre testes polui o ChangeTracker:
    // entidades rastreadas por um teste afetam consultas do próximo.
    private SgcfDbContext Context { get; set; } = default!;

    /// <summary>Clock fixo em 2026-06-01 09:00 UTC para datas futuras nos testes.</summary>
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

    /// <summary>
    /// Cria um novo DbContext com a mesma connection string.
    /// Útil para leituras isoladas que confirmam persistência real
    /// (não depende do cache do contexto que escreveu).
    /// </summary>
    public SgcfDbContext CreateFreshContext()
    {
        DbContextOptions<SgcfDbContext> opts = new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.UseNodaTime())
            .Options;
        return new SgcfDbContext(opts);
    }

    // Clock em 2026-06-01 garante que DataContratacaoPrevista (2026-07-01) seja futura.
    private static IClock CreateFixedClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 6, 1, 9, 0));
        return clock;
    }
}
