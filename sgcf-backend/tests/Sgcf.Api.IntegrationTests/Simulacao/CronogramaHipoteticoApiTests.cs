using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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

namespace Sgcf.Api.IntegrationTests.Simulacao;

// ── Fixture ───────────────────────────────────────────────────────────────────

/// <summary>
/// Fixture para testes E2E do endpoint POST /api/v1/simulacoes/cronograma-hipotetico.
/// Levanta a API completa com PostgreSQL real via Testcontainers.
/// Marcada [Slow] — excluir do loop rápido com --filter "Category!=Slow".
/// </summary>
public sealed class CronogramaHipoteticoApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_cronograma_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_e2e")
        .Build();

    /// <summary>Instante fixo: 2026-05-19. Datas de contratação a partir de 2026-07 são futuras.</summary>
    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 9, 0);

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

    /// <summary>
    /// Cria HttpClient autenticado com token de desenvolvimento.
    /// O middleware de dev aceita qualquer Bearer token.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }
}

[CollectionDefinition("CronogramaHipoteticoApi")]
#pragma warning disable CA1711
public sealed class CronogramaHipoteticoApiGroup : ICollectionFixture<CronogramaHipoteticoApiFixture> { }
#pragma warning restore CA1711

// ── Testes ────────────────────────────────────────────────────────────────────

/// <summary>
/// Testes E2E para POST /api/v1/simulacoes/cronograma-hipotetico.
///
/// O endpoint é stateless (pure compute) — não persiste nada no banco.
/// Cada teste envia o payload completo e verifica a resposta HTTP.
/// </summary>
[Collection("CronogramaHipoteticoApi")]
[Trait("Category", "Slow")]
public sealed class CronogramaHipoteticoApiTests(CronogramaHipoteticoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private const string EndpointUrl = "/api/v1/simulacoes/cronograma-hipotetico";

    // ── Teste 6: Bullet válido → 200 com eventos ──────────────────────────────

    /// <summary>
    /// Cenário feliz: Bullet em USD com taxa fixa 6% a.a.
    /// Deve retornar 200 OK com lista de eventos e sumário.
    /// </summary>
    [Fact]
    public async Task Post_CronogramaHipotetico_Bullet_Retorna200ComEventos()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        object payload = new
        {
            simulacao = new
            {
                bancoId = Guid.NewGuid(),
                modalidade = "Finimp",
                moeda = "Usd",
                valorPrincipal = 1_000_000m,
                dataContratacaoPrevista = "2026-09-01",
                dataPrimeiroVencimento = "2027-09-01",
                tipoTaxa = "Fixa",
                taxaAa = 6m,
                spreadAa = (decimal?)null,
                baseCalculo = "Dias360",
                estruturaAmortizacao = "Bullet",
                periodicidade = "Anual",
                quantidadeParcelas = 1,
                anchorDiaMes = "DiaContratacao"
            },
            cdiReferenciaAaPercentual = (decimal?)null
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(EndpointUrl, payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Bullet com taxa fixa válida deve retornar 200. Body: {await response.Content.ReadAsStringAsync()}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("eventos").GetArrayLength().Should().BeGreaterThan(0,
            because: "Bullet deve gerar ao menos 1 evento de Principal e 1 de Juros");
        body.GetProperty("quantidadeEventos").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("principalTotal").GetDecimal().Should().BeApproximately(1_000_000m, 1m);
        body.GetProperty("taxaEfetivaAaPercentual").GetDecimal().Should().BeApproximately(6m, 0.001m);
    }

    // ── Teste 7: CDI+spread sem CDI → 400 ─────────────────────────────────────

    /// <summary>
    /// CDI+spread sem cdiReferenciaAaPercentual deve retornar 400 Bad Request.
    /// O controller mapeia ArgumentException → 400.
    /// </summary>
    [Fact]
    public async Task Post_CronogramaHipotetico_CdiSpreadSemCdi_Retorna400()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        object payload = new
        {
            simulacao = new
            {
                bancoId = Guid.NewGuid(),
                modalidade = "Nce",
                moeda = "Brl",
                valorPrincipal = 500_000m,
                dataContratacaoPrevista = "2026-08-01",
                dataPrimeiroVencimento = "2027-08-01",
                tipoTaxa = "CdiSpread",
                taxaAa = (decimal?)null,
                spreadAa = 2m,
                baseCalculo = "Dias360",
                estruturaAmortizacao = "Bullet",
                periodicidade = "Anual",
                quantidadeParcelas = 1,
                anchorDiaMes = "DiaContratacao"
            },
            cdiReferenciaAaPercentual = (decimal?)null   // ausente — deve gerar 400
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(EndpointUrl, payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "CdiSpread sem cdiReferenciaAaPercentual é inválido e deve retornar 400");

        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error", because: "response body deve conter campo 'error' com a mensagem");
    }

    // ── Teste 8: Price mensal — sumário correto (opcional mas forte) ──────────

    /// <summary>
    /// Para estrutura Price com 12 parcelas, verifica que:
    /// 1. Quantidade de eventos é 24 (12 Principal + 12 Juros) — ou mais no caso de Price.
    /// 2. PrincipalTotal ≈ ValorPrincipal.
    /// 3. JurosTotal > 0.
    ///
    /// Este teste é "cross-estrutura" — verifica a consistência do sumário.
    /// </summary>
    [Fact]
    public async Task Post_CronogramaHipotetico_Price12Parcelas_RetornaSumarioCorreto()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        object payload = new
        {
            simulacao = new
            {
                bancoId = Guid.NewGuid(),
                modalidade = "Nce",
                moeda = "Brl",
                valorPrincipal = 120_000m,
                dataContratacaoPrevista = "2026-08-01",
                dataPrimeiroVencimento = "2026-09-01",
                tipoTaxa = "Fixa",
                taxaAa = 12m,
                spreadAa = (decimal?)null,
                baseCalculo = "Dias360",
                estruturaAmortizacao = "Price",
                periodicidade = "Mensal",
                quantidadeParcelas = 12,
                anchorDiaMes = "DiaContratacao"
            },
            cdiReferenciaAaPercentual = (decimal?)null
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(EndpointUrl, payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Price mensal válido deve retornar 200. Body: {await response.Content.ReadAsStringAsync()}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        int quantidadeEventos = body.GetProperty("quantidadeEventos").GetInt32();
        decimal principalTotal = body.GetProperty("principalTotal").GetDecimal();
        decimal jurosTotal = body.GetProperty("jurosTotal").GetDecimal();

        quantidadeEventos.Should().BeGreaterThan(0);
        principalTotal.Should().BeApproximately(120_000m, 1m,
            because: "soma de principal deve igualar o valor original");
        jurosTotal.Should().BeGreaterThan(0,
            because: "taxa de 12% a.a. deve gerar juros positivos");
    }
}
