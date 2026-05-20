using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NodaTime;

using NSubstitute;

using Sgcf.Api.IntegrationTests.TestAuth;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;
using Sgcf.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

// ── Super-admin auth handler ──────────────────────────────────────────────────

/// <summary>
/// Auth handler para a suite de isolamento cross-tenant.
/// <list type="bullet">
///   <item>Lê <c>X-Test-Tenant-Id</c> para impersonar qualquer tenant.</item>
///   <item>
///     Lê <c>X-Test-Super-Admin: true</c> para incluir o role "super-admin".
///     Quando ausente, o cliente é tratado como usuário regular (sem super-admin).
///   </item>
/// </list>
/// </summary>
internal sealed class CrossTenantTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName      = "CrossTenantTestAuth";
    public const string SuperAdminHeader = "X-Test-Super-Admin";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        string sub = Request.Headers[TestAuthHandler.SubHeader].FirstOrDefault() ?? "cross-tenant-test-user";

        // X-Test-Tenant-Id lets each test client impersonate a specific tenant.
        string tenantId = Request.Headers[TestAuthHandler.TenantIdHeader].FirstOrDefault()
            ?? TenantTestData.TenantAId.ToString();

        bool isSuperAdmin = string.Equals(
            Request.Headers[SuperAdminHeader].FirstOrDefault(),
            "true",
            StringComparison.OrdinalIgnoreCase);

        List<Claim> claimsList =
        [
            new Claim("sub",                    sub),
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim(ClaimTypes.Name,           "cross-tenant-test-user"),
            new Claim("tenant_id",               tenantId),
            new Claim(ClaimTypes.Role,           "admin"),
            new Claim(ClaimTypes.Role,           "tesouraria"),
        ];

        if (isSuperAdmin)
        {
            claimsList.Add(new Claim(ClaimTypes.Role, "super-admin"));
        }

        Claim[] claims = [.. claimsList];

        ClaimsIdentity  identity  = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

// ── Fixture ───────────────────────────────────────────────────────────────────

/// <summary>
/// Fixture compartilhada por todos os testes de isolamento cross-tenant.
///
/// Provisiona um único container PostgreSQL com DOIS tenants:
/// <list type="bullet">
///   <item>TenantA = Proxys (00000000-0000-7000-8000-000000000001)</item>
///   <item>TenantB = ACME (00000000-0000-7000-8000-000000000002)</item>
/// </list>
///
/// Usa <see cref="CrossTenantTestAuthHandler"/> como esquema padrão para que cada
/// teste possa impersonar qualquer tenant via <c>X-Test-Tenant-Id</c>.
/// </summary>
public sealed class MultiTenantFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_cross_tenant_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_cross_tenant_e2e")
        .Build();

    /// <summary>Instante fixo injetado via IClock fake: 2026-05-20 10:00 UTC.</summary>
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

                // CrossTenantTestAuthHandler lê X-Test-Tenant-Id para impersonar tenants.
                services.AddAuthentication(CrossTenantTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, CrossTenantTestAuthHandler>(
                        CrossTenantTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = CrossTenantTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme    = CrossTenantTestAuthHandler.SchemeName;
                });
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        SgcfDbContext ctx = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();

        await SeedTenantAAsync(scope.ServiceProvider);
        await SeedTenantBAsync(scope.ServiceProvider);

        // Provisiona ambos os tenants (ParametrosSistema + ParametrosCotacao).
        await ProvisionarTenantAsync(TenantTestData.TenantASlug);
        await ProvisionarTenantAsync(TenantTestData.TenantBSlug);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    // ── Client factories ──────────────────────────────────────────────────────

    /// <summary>
    /// Cria um <see cref="HttpClient"/> autenticado como o tenant especificado.
    /// Todos os requests desse client usarão o tenant_id informado.
    /// </summary>
    public HttpClient ClientFor(Guid tenantId)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer cross-tenant-test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantIdHeader, tenantId.ToString());
        return client;
    }

    /// <summary>
    /// Cria um <see cref="HttpClient"/> com role super-admin e sem tenant_id fixo,
    /// para acessar endpoints administrativos.
    /// </summary>
    public HttpClient SuperAdminClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer cross-tenant-super-admin-token");
        // X-Test-Super-Admin: true instrui o CrossTenantTestAuthHandler a incluir o role "super-admin".
        client.DefaultRequestHeaders.Add(CrossTenantTestAuthHandler.SuperAdminHeader, "true");
        return client;
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task SeedTenantAAsync(IServiceProvider services)
    {
        ITenantRepository repo  = services.GetRequiredService<ITenantRepository>();
        IClock            clock = services.GetRequiredService<IClock>();

        if (await repo.GetAsync(TenantTestData.TenantAId) is not null)
        {
            return;
        }

        Tenant tenantA = Tenant.Criar(
            TenantTestData.TenantAId,
            TenantTestData.TenantASlug,
            TenantTestData.TenantANome,
            TenantTestData.TenantACnpj,
            PlanoAssinatura.Padrao,
            clock);

        await repo.AddAsync(tenantA);
        await repo.SaveChangesAsync();
    }

    private static async Task SeedTenantBAsync(IServiceProvider services)
    {
        ITenantRepository repo  = services.GetRequiredService<ITenantRepository>();
        IClock            clock = services.GetRequiredService<IClock>();

        if (await repo.GetAsync(TenantTestData.TenantBId) is not null)
        {
            return;
        }

        Tenant tenantB = Tenant.Criar(
            TenantTestData.TenantBId,
            TenantTestData.TenantBSlug,
            TenantTestData.TenantBNome,
            TenantTestData.TenantBCnpj,
            PlanoAssinatura.Padrao,
            clock);

        await repo.AddAsync(tenantB);
        await repo.SaveChangesAsync();
    }

    private async Task ProvisionarTenantAsync(string slug)
    {
        HttpClient client = SuperAdminClient();
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/admin/tenants/{slug}/provisionar", content: null);

        // 200 = provisionado, qualquer outro status indica problema no fixture setup.
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Falha ao provisionar tenant '{slug}': HTTP {(int)res.StatusCode} — {body}");
        }
    }
}

// ── Collection definition ─────────────────────────────────────────────────────

[CollectionDefinition("MultiTenantIsolation")]
#pragma warning disable CA1711
public sealed class MultiTenantIsolationGroup : ICollectionFixture<MultiTenantFixture> { }
#pragma warning restore CA1711
