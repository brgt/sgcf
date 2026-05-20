using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// Testa a proteção a race condition no <see cref="ParametroSistemaRepository.GetOrCreateGlobalAsync"/>.
///
/// Marcado como Slow — sobe container PostgreSQL real via Testcontainers.
/// O índice único em <c>parametro_sistema.chave</c> é o guard de integridade.
/// A estratégia catch-then-reread garante exatamente 1 linha mesmo com N concorrentes.
/// </summary>
[Trait("Category", "Slow")]
public sealed class ParametroSistemaRepositoryRaceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test_sistema")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Aplica migrations para criar o schema e o índice único
        DbContextOptions<SgcfDbContext> opts = BuildOptions(_connectionString);
        await using SgcfDbContext ctx = new(opts, CreateUnresolvedTenantContext());
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// 5 chamadas concorrentes a GetOrCreateGlobalAsync devem resultar em:
    /// - Todas retornando um <see cref="ParametroSistema"/> não-nulo
    /// - Exatamente 1 linha na tabela (singleton invariante)
    /// </summary>
    [Fact]
    public async Task GetOrCreateGlobalAsync_Concorrente_CriaSingletonSemDuplicata()
    {
        // Arrange — 5 contextos independentes simulando 5 requests simultâneas
        const int concorrentes = 5;

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 20, 10, 0));

        // Act — dispara todas as chamadas ao mesmo tempo
        ParametroSistema?[] resultados = await Task.WhenAll(
            Enumerable.Range(0, concorrentes).Select(_ => InvocarGetOrCreate(clock)));

        // Assert — todas as chamadas retornaram um resultado
        resultados.Should().AllSatisfy(r =>
            r.Should().NotBeNull(because: "GetOrCreateGlobalAsync nunca retorna null"));

        // Verifica no banco que há exatamente 1 linha
        await using SgcfDbContext ctxVerificacao = new(BuildOptions(_connectionString), CreateUnresolvedTenantContext());
        int totalLinhas = await ctxVerificacao.Set<ParametroSistema>().CountAsync();
        totalLinhas.Should().Be(1,
            because: "o índice único em chave=GLOBAL garante que apenas 1 linha existe, independente de race");

        // Todos os resultados apontam para o mesmo singleton (mesma Chave)
        Guid primeiroId = resultados[0]!.Id;
        resultados.Should().AllSatisfy(r =>
            r!.Id.Should().Be(primeiroId,
                because: "todos os concorrentes devem retornar o mesmo singleton"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ParametroSistema?> InvocarGetOrCreate(IClock clock)
    {
        // Cada task usa seu próprio DbContext — simula requests HTTP independentes
        DbContextOptions<SgcfDbContext> opts = BuildOptions(_connectionString);
        await using SgcfDbContext ctx = new(opts, CreateUnresolvedTenantContext());
        ParametroSistemaRepository repo = new(ctx);
        return await repo.GetOrCreateGlobalAsync(clock);
    }

    private static DbContextOptions<SgcfDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<SgcfDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime())
            .Options;

    /// <summary>
    /// Cria ITenantContext não resolvido — desativa o global query filter nos testes,
    /// permitindo que o repositório veja todas as linhas independente de TenantId.
    /// </summary>
    private static ITenantContext CreateUnresolvedTenantContext()
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(false);
        return ctx;
    }
}
