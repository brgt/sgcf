using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Contabilidade;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Application.Tests.Contabilidade;

/// <summary>
/// Verifica que <see cref="PlanoContasGerencial"/> é corretamente isolado por tenant.
///
/// Cobertura dos critérios de aceite da Task −1.10:
/// <list type="bullet">
///   <item>Modelo global (<see cref="PlanoContasModelo"/>) visível independente de tenant.</item>
///   <item>Provisionamento clona o modelo para o tenant.</item>
///   <item>Edição em tenant A não afeta tenant B.</item>
///   <item>Tenant sem PlanoContas retorna lista vazia (não clona automaticamente).</item>
/// </list>
/// </summary>
[Trait("Category", "Slow")]
public sealed class PlanoContasPerTenantTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_test_plano_contas")
        .WithUsername("sgcf")
        .WithPassword("sgcf_test")
        .Build();

    private static readonly Guid TenantA = Guid.Parse("00000000-0000-7000-8000-000000000021");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-7000-8000-000000000022");

    private static readonly IClock Clock = CreateClock(Instant.FromUtc(2026, 5, 20, 12, 0));

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        DbContextOptions<SgcfDbContext> opts = BuildOptions(_connectionString);
        await using SgcfDbContext ctx = new(opts, CreateContext(TenantA));
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // ── Teste 1: modelo global tem contas após migration ─────────────────────

    [Fact]
    public async Task PlanoContasModelo_AposMigration_ContemContasSeed()
    {
        // Arrange
        var modeloRepo = CriarModeloRepo();

        // Act
        IReadOnlyList<PlanoContasModelo> contas = await modeloRepo.ListAllAsync(CancellationToken.None);

        // Assert
        contas.Should().NotBeEmpty(
            because: "a migration seed deve inserir o plano de contas modelo padrão");
    }

    // ── Teste 2: tenant sem PlanoContas retorna lista vazia ──────────────────

    [Fact]
    public async Task ListAllAsync_TenantSemPlanoContas_RetornaListaVazia()
    {
        // Arrange — TenantA não foi provisionado (sem seed de PlanoContas)
        var repo = CriarRepo(TenantA);

        // Act
        IReadOnlyList<PlanoContasGerencial> contas = await repo.ListAllAsync(CancellationToken.None);

        // Assert
        contas.Should().BeEmpty(
            because: "tenant não provisionado não deve ter PlanoContas");
    }

    // ── Teste 3: clonagem cria cópias independentes por tenant ───────────────

    [Fact]
    public async Task ClonarDeModelo_CriaContasIndependentes_ParaTenantA()
    {
        // Arrange — clona modelo para TenantA
        var modeloRepo = CriarModeloRepo();
        IReadOnlyList<PlanoContasModelo> modelo = await modeloRepo.ListAllAsync(CancellationToken.None);
        modelo.Should().NotBeEmpty();

        await ClonarModeloParaTenantAsync(TenantA, modelo);

        // Act
        var repoA = CriarRepo(TenantA);
        IReadOnlyList<PlanoContasGerencial> contasA = await repoA.ListAllAsync(CancellationToken.None);

        // Assert
        contasA.Should().HaveCount(modelo.Count,
            because: "cada entrada do modelo deve gerar uma conta no tenant");
        contasA.Should().AllSatisfy(c => c.ClonadaDeModelo.Should().BeTrue());
        contasA.Should().AllSatisfy(c => c.TenantId.Should().Be(TenantA));
    }

    // ── Teste 4: edição em tenant A não afeta tenant B ───────────────────────

    [Fact]
    public async Task Edicao_em_TenantA_NaoAfeta_TenantB()
    {
        // Arrange — clona modelo para ambos os tenants
        var modeloRepo = CriarModeloRepo();
        IReadOnlyList<PlanoContasModelo> modelo = await modeloRepo.ListAllAsync(CancellationToken.None);

        await ClonarModeloParaTenantAsync(TenantA, modelo);
        await ClonarModeloParaTenantAsync(TenantB, modelo);

        // Pega o primeiro código para referenciar em ambos os tenants
        string codigoPrimeiro = modelo.OrderBy(m => m.CodigoGerencial).First().CodigoGerencial;
        string novoNome = "Nome Customizado de Tenant A";

        // Edita a conta em TenantA
        {
            var repoA = CriarRepo(TenantA);
            PlanoContasGerencial? contaA = await repoA.GetByCodigoAsync(codigoPrimeiro, CancellationToken.None);
            contaA.Should().NotBeNull();
            contaA!.Atualizar(novoNome, contaA.Natureza, contaA.CodigoSapB1, Clock);
            await repoA.SaveChangesAsync(CancellationToken.None);
        }

        // Act — TenantB lê a mesma conta
        var repoB = CriarRepo(TenantB);
        PlanoContasGerencial? contaB = await repoB.GetByCodigoAsync(codigoPrimeiro, CancellationToken.None);

        // Assert — TenantB não deve ver o nome customizado de TenantA
        contaB.Should().NotBeNull();
        contaB!.Nome.Should().NotBe(novoNome,
            because: "edição em TenantA não deve afetar TenantB");
        contaB.TenantId.Should().Be(TenantB);
    }

    // ── Teste 5: TenantA não vê contas de TenantB ────────────────────────────

    [Fact]
    public async Task ListAllAsync_TenantA_NaoVeContasDeTenantB()
    {
        // Arrange — somente TenantB tem PlanoContas
        var modeloRepo = CriarModeloRepo();
        IReadOnlyList<PlanoContasModelo> modelo = await modeloRepo.ListAllAsync(CancellationToken.None);
        await ClonarModeloParaTenantAsync(TenantB, modelo);

        // Act
        var repoA = CriarRepo(TenantA);
        IReadOnlyList<PlanoContasGerencial> contasA = await repoA.ListAllAsync(CancellationToken.None);

        // Assert
        contasA.Should().BeEmpty(
            because: "TenantA não deve ver contas de TenantB");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private PlanoContasRepository CriarRepo(Guid tenantId)
    {
        ITenantContext ctx = CreateContext(tenantId);
        SgcfDbContext dbContext = new(
            new DbContextOptionsBuilder<SgcfDbContext>()
                .UseNpgsql(_connectionString, npgsql => npgsql.UseNodaTime())
                .AddInterceptors(new TenantSaveInterceptor(ctx, NullLogger<TenantSaveInterceptor>.Instance))
                .Options,
            ctx);
        return new PlanoContasRepository(dbContext);
    }

    private PlanoContasModeloRepository CriarModeloRepo()
    {
        // Modelo é global — não usa query filter nem TenantSaveInterceptor.
        DbContextOptions<SgcfDbContext> opts = BuildOptions(_connectionString);
        // Contexto sem tenant resolvido — modelo não é ITenantScoped.
        ITenantContext ctx = CreateUnresolvedContext();
        SgcfDbContext dbContext = new(opts, ctx);
        return new PlanoContasModeloRepository(dbContext);
    }

    private async Task ClonarModeloParaTenantAsync(Guid tenantId, IReadOnlyList<PlanoContasModelo> modelo)
    {
        var repo = CriarRepo(tenantId);
        foreach (PlanoContasModelo item in modelo)
        {
            PlanoContasGerencial conta = PlanoContasGerencial.ClonarDeModelo(item, Clock);
            repo.Add(conta);
        }
        await repo.SaveChangesAsync(CancellationToken.None);
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

    private static ITenantContext CreateContext(Guid tenantId)
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(true);
        ctx.TenantId.Returns(tenantId);
        ctx.TenantIdOrDefault.Returns(tenantId);
        return ctx;
    }

    private static ITenantContext CreateUnresolvedContext()
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(false);
        ctx.TenantIdOrDefault.Returns(Guid.Empty);
        return ctx;
    }
}
