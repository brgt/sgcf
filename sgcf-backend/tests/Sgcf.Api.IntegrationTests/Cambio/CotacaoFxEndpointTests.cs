using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
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

namespace Sgcf.Api.IntegrationTests.Cambio;

/// <summary>
/// Fixture do endpoint manual de cotações FX. Registra o <see cref="TestAuthHandler"/>
/// como esquema padrão para permitir testar autorização por role (admin vs não-admin).
/// Relógio fixo em 2026-05-16 12:00 UTC para a regra "momento não-futuro".
/// </summary>
public sealed class CotacaoFxApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_cotacaofx")
        .WithUsername("sgcf")
        .WithPassword("sgcf_cotacaofx")
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
                        npgsql =>
                        {
                            npgsql.UseNodaTime();
                            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        }));

                services.RemoveAll<IClock>();
                services.AddSingleton(clockFake);

                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
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

    /// <summary>Cliente admin (roles padrão admin,tesouraria).</summary>
    public HttpClient CreateAdminClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        return client;
    }

    /// <summary>Cliente autenticado com roles específicas (para testes de autorização).</summary>
    public HttpClient CreateClientComRoles(string roles)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }
}

[CollectionDefinition("CotacaoFxApi")]
#pragma warning disable CA1711
public sealed class CotacaoFxApiGroup : ICollectionFixture<CotacaoFxApiFixture> { }
#pragma warning restore CA1711

[Collection("CotacaoFxApi")]
[Trait("Category", "Slow")]
public sealed class CotacaoFxEndpointTests(CotacaoFxApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── AC-6: cadastro grava PtaxD0 e é idempotente pela chave ────────────────

    [Fact]
    public async Task Post_Admin_GravaPtaxD0_EConsultaConfirmaValor()
    {
        using HttpClient client = fixture.CreateAdminClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes-fx", new
        {
            moedaBase = "Usd",
            momento = "2026-05-15T20:00:00Z",
            valorCompra = 5.10m,
            valorVenda = 5.15m
            // moedaQuote, tipo e fonte usam os defaults (Brl, PtaxD0, MANUAL)
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"corpo: {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("tipo").GetString().Should().Be("PtaxD0");
        body.GetProperty("moedaQuote").GetString().Should().Be("Brl");

        // Conferência via GET
        HttpResponseMessage getRes = await client.GetAsync(
            "/api/v1/cotacoes-fx?moeda=Usd&tipo=PtaxD0&ate=2026-05-15");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement getBody = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        getBody.GetProperty("valorVenda").GetDecimal().Should().Be(5.15m);
    }

    [Fact]
    public async Task Post_RepetidoMesmaChave_CorrigeValor_SemDuplicar()
    {
        using HttpClient client = fixture.CreateAdminClient();

        // Momento distinto deste teste para não colidir com outros casos da fixture.
        const string momento = "2026-05-14T20:00:00Z";

        HttpResponseMessage primeiro = await client.PostAsJsonAsync("/api/v1/cotacoes-fx", new
        {
            moedaBase = "Usd",
            momento,
            valorCompra = 4.90m,
            valorVenda = 4.95m
        });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        // Segundo POST com a MESMA chave (moeda+momento+tipo) e valores corrigidos.
        HttpResponseMessage segundo = await client.PostAsJsonAsync("/api/v1/cotacoes-fx", new
        {
            moedaBase = "Usd",
            momento,
            valorCompra = 9.90m,
            valorVenda = 9.95m
        });
        segundo.StatusCode.Should().Be(HttpStatusCode.Created);

        // A resposta do POST de correção reflete o valor realmente persistido.
        JsonElement segundoBody = await segundo.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        segundoBody.GetProperty("valorVenda").GetDecimal().Should().Be(9.95m);

        // A leitura confirma a CORREÇÃO (RF-06): a chave única não duplica; o valor é atualizado.
        HttpResponseMessage getRes = await client.GetAsync(
            "/api/v1/cotacoes-fx?moeda=Usd&tipo=PtaxD0&ate=2026-05-14");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement getBody = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        getBody.GetProperty("valorVenda").GetDecimal().Should().Be(9.95m,
            "re-enviar a mesma chave corrige o valor (true upsert), sem duplicar");
    }

    // ── AC-7: autorização e validação ─────────────────────────────────────────

    [Fact]
    public async Task Post_NaoAdmin_Retorna403()
    {
        using HttpClient client = fixture.CreateClientComRoles("leitura");

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes-fx", new
        {
            moedaBase = "Usd",
            momento = "2026-05-15T20:00:00Z",
            valorCompra = 5.10m,
            valorVenda = 5.15m
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Usd", "Brl", "2026-05-15T20:00:00Z", 0, 5.15, "valor <= 0")]
    [InlineData("Usd", "Usd", "2026-05-15T20:00:00Z", 5.10, 5.15, "quote != BRL")]
    [InlineData("Usd", "Brl", "2099-01-01T00:00:00Z", 5.10, 5.15, "momento futuro")]
    public async Task Post_PayloadInvalido_Retorna400(
        string moedaBase, string moedaQuote, string momento, double compra, double venda, string motivo)
    {
        using HttpClient client = fixture.CreateAdminClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes-fx", new
        {
            moedaBase,
            moedaQuote,
            momento,
            tipo = "PtaxD0",
            valorCompra = (decimal)compra,
            valorVenda = (decimal)venda
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"motivo: {motivo}");
    }
}
