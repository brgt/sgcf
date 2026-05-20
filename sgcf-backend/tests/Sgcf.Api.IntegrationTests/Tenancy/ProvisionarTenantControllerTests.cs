using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;
using Sgcf.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Tenancy;

// ── Auth Handler ──────────────────────────────────────────────────────────────

/// <summary>
/// Handler de autenticação para testes de endpoints que exigem a policy SuperAdmin.
/// Inclui o role "super-admin" para que a policy <c>Policies.SuperAdmin</c> seja satisfeita.
/// </summary>
internal sealed class SuperAdminTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SuperAdminTestAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        Claim[] claims =
        [
            new Claim(ClaimTypes.Name,           "super-admin-user"),
            new Claim(ClaimTypes.NameIdentifier, "super-admin-id"),
            new Claim(ClaimTypes.Role,           "super-admin"),
            new Claim(ClaimTypes.Role,           "admin"),
        ];

        ClaimsIdentity  identity  = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

// ── Fixture ───────────────────────────────────────────────────────────────────

/// <summary>
/// Fixture para testes do endpoint <c>POST /api/v1/admin/tenants/{idOrSlug}/provisionar</c>.
/// Usa container PostgreSQL isolado — marcado como Slow.
/// </summary>
[Trait("Category", "Slow")]
public sealed class ProvisionarTenantApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_provisionar_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_provisionar_e2e")
        .Build();

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
                        npgsql => npgsql.UseNodaTime()));

                services.RemoveAll<IClock>();
                services.AddSingleton(clockFake);

                // Substitui JWT Bearer por handler de teste que inclui role "super-admin".
                services.AddAuthentication(SuperAdminTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, SuperAdminTestAuthHandler>(
                        SuperAdminTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = SuperAdminTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme    = SuperAdminTestAuthHandler.SchemeName;
                });
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        SgcfDbContext ctx = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    public HttpClient CreateClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        return client;
    }

    /// <summary>
    /// Cria um tenant no banco e retorna o slug para uso nos testes.
    /// </summary>
    public async Task<(Guid Id, string Slug)> CriarTenantAsync(
        string slug,
        string nome,
        StatusTenant status = StatusTenant.Ativo)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITenantRepository repo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        Guid id = Guid.CreateVersion7();
        Tenant tenant = Tenant.Criar(id, slug, nome, "12345678000195", PlanoAssinatura.Padrao, clock);

        if (status == StatusTenant.Suspenso)
        {
            tenant.Suspender("motivo-teste", clock);
        }
        else if (status == StatusTenant.Arquivado)
        {
            tenant.Arquivar(clock);
        }

        await repo.AddAsync(tenant, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        return (id, slug);
    }
}

[CollectionDefinition("ProvisionarTenantApi")]
#pragma warning disable CA1711
public sealed class ProvisionarTenantApiGroup : ICollectionFixture<ProvisionarTenantApiFixture> { }
#pragma warning restore CA1711

// ── Testes ────────────────────────────────────────────────────────────────────

/// <summary>
/// Testes de integração para o endpoint idempotente de provisionamento de tenant.
/// </summary>
[Collection("ProvisionarTenantApi")]
[Trait("Category", "Slow")]
public sealed class ProvisionarTenantControllerTests(ProvisionarTenantApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── Cenário 1: tenant ativo — primeiro provisionamento ───────────────────

    [Fact]
    public async Task Provisionar_TenantAtivo_PrimeiraVez_Retorna200ComCriadosMaiorQueZero()
    {
        // Arrange
        (Guid id, string slug) = await fixture.CriarTenantAsync(
            slug: UnicoSlug("prov-ativo"),
            nome: "Empresa Prov Ativo");
        HttpClient client = fixture.CreateClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/admin/tenants/{slug}/provisionar", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);

        body.GetProperty("tenantId").GetGuid().Should().Be(id);
        body.GetProperty("criados").GetProperty("parametrosSistema").GetInt32().Should().Be(1);
        body.GetProperty("criados").GetProperty("parametrosCotacao").GetInt32().Should().Be(1);
    }

    // ── Cenário 2: idempotência — segundo provisionamento ────────────────────

    [Fact]
    public async Task Provisionar_TenantAtivo_SegundaVez_Retorna200ComCriadosZero()
    {
        // Arrange — cria tenant e provisiona uma primeira vez
        (_, string slug) = await fixture.CriarTenantAsync(
            slug: UnicoSlug("prov-idem"),
            nome: "Empresa Prov Idempotente");
        HttpClient client = fixture.CreateClient();
        string url = $"/api/v1/admin/tenants/{slug}/provisionar";

        await client.PostAsync(url, content: null);

        // Act — segunda chamada
        HttpResponseMessage res2 = await client.PostAsync(url, content: null);

        // Assert — criados = 0, ignorados > 0
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await res2.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);

        body.GetProperty("criados").GetProperty("parametrosSistema").GetInt32()
            .Should().Be(0, because: "segunda chamada não deve criar duplicata (idempotência)");
        body.GetProperty("criados").GetProperty("parametrosCotacao").GetInt32()
            .Should().Be(0, because: "segunda chamada não deve criar duplicata (idempotência)");
        body.GetProperty("ignorados").GetProperty("parametrosSistema").GetInt32()
            .Should().BeGreaterThan(0, because: "segunda chamada deve indicar registro já existente");
        body.GetProperty("ignorados").GetProperty("parametrosCotacao").GetInt32()
            .Should().BeGreaterThan(0, because: "segunda chamada deve indicar registro já existente");
    }

    // ── Cenário 3: tenant inexistente → 404 ──────────────────────────────────

    [Fact]
    public async Task Provisionar_TenantInexistente_Retorna404()
    {
        // Arrange
        HttpClient client = fixture.CreateClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            "/api/v1/admin/tenants/slug-que-nao-existe/provisionar", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Cenário 4: tenant arquivado → 409 ────────────────────────────────────

    [Fact]
    public async Task Provisionar_TenantArquivado_Retorna409()
    {
        // Arrange
        (_, string slug) = await fixture.CriarTenantAsync(
            slug: UnicoSlug("prov-arq"),
            nome: "Empresa Prov Arquivada",
            status: StatusTenant.Arquivado);
        HttpClient client = fixture.CreateClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/admin/tenants/{slug}/provisionar", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "tenant arquivado não pode ser provisionado");
    }

    // ── Cenário 5: tenant suspenso → 400 ─────────────────────────────────────

    [Fact]
    public async Task Provisionar_TenantSuspenso_Retorna400()
    {
        // Arrange
        (_, string slug) = await fixture.CriarTenantAsync(
            slug: UnicoSlug("prov-susp"),
            nome: "Empresa Prov Suspensa",
            status: StatusTenant.Suspenso);
        HttpClient client = fixture.CreateClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/admin/tenants/{slug}/provisionar", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "tenant suspenso não pode ser provisionado até ser reativado");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Gera um slug único para evitar conflito entre execuções paralelas.
    private static string UnicoSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
