using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;

using NSubstitute;

using Sgcf.Api.Filters;
using Sgcf.Api.IntegrationTests.TestAuth;
using Sgcf.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Envelope;

// ── Smoke controller (somente testes) ────────────────────────────────────────

/// <summary>
/// Controller mínimo registrado apenas na WebApplicationFactory de testes.
/// Serve como endpoint controlado para verificar o comportamento do
/// <see cref="EnvelopeResultFilter"/> sem depender de dados reais de negócio.
/// </summary>
[ApiController]
[Route("api/v1/test/envelope")]
public sealed class EnvSmokeController : ControllerBase
{
    /// <summary>Endpoint que declara [ProducesEnvelope] — deve ser envelopado.</summary>
    [HttpGet("com-envelope")]
    [ProducesEnvelope]
    [ServiceFilter<EnvelopeResultFilter>]
    public IActionResult GetComEnvelope() =>
        Ok(new { mensagem = "olá envelope", valor = 42 });

    /// <summary>Endpoint sem [ProducesEnvelope] — resposta não deve ser envelopada.</summary>
    [HttpGet("sem-envelope")]
    public IActionResult GetSemEnvelope() =>
        Ok(new { mensagem = "sem envelope" });

    /// <summary>Endpoint que retorna NoContent — filtro deve passar sem modificar.</summary>
    [HttpGet("no-content")]
    [ProducesEnvelope]
    [ServiceFilter<EnvelopeResultFilter>]
    public IActionResult GetNoContent() => NoContent();
}

// ── Fixture ───────────────────────────────────────────────────────────────────

/// <summary>
/// Fixture para testes do EnvelopeResultFilter.
/// PostgreSQL real via Testcontainers.
/// O <see cref="EnvSmokeController"/> é registrado dinamicamente via ApplicationPart
/// para não poluir os controllers de produção.
/// </summary>
[Trait("Category", "Slow")]
public sealed class EnvelopeResponseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_envelope_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_envelope_e2e")
        .Build();

    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 12, 0);

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

                // Registra o EnvelopeResultFilter como serviço para que [ServiceFilter] funcione.
                services.AddScoped<EnvelopeResultFilter>();

                // Injeta o controller de smoke no pipeline MVC via ApplicationPart.
                // Isso evita adicionar o controller ao projeto de produção.
                services.AddMvc()
                    .AddApplicationPart(typeof(EnvSmokeController).Assembly);

                // Substitui o esquema JWT por TestAuth para autenticação em testes.
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme    = TestAuthHandler.SchemeName;
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

    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "envelope-test-user");
        return client;
    }
}

[CollectionDefinition("EnvelopeResponse")]
#pragma warning disable CA1711
public sealed class EnvelopeResponseGroup : ICollectionFixture<EnvelopeResponseFixture> { }
#pragma warning restore CA1711

// ── Testes de integração ──────────────────────────────────────────────────────

/// <summary>
/// Testes E2E do <see cref="EnvelopeResultFilter"/>.
///
/// Cenários:
///   1. Endpoint com [ProducesEnvelope] → resposta tem shape { data, meta }.
///   2. Endpoint sem [ProducesEnvelope] → resposta não é envelopada.
///   3. Endpoint com [ProducesEnvelope] que retorna NoContent → passa sem modificar.
///   4. meta.dataHoraCalculo corresponde ao instante fixo do IClock fake.
///   5. meta.completude é "Completo" por padrão.
///   6. meta.fontesConsultadas é array vazio por padrão.
/// </summary>
[Collection("EnvelopeResponse")]
[Trait("Category", "Slow")]
public sealed class EnvelopeResponseTests(EnvelopeResponseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── Teste 1: shape { data, meta } ────────────────────────────────────────

    /// <summary>
    /// Endpoint marcado com [ProducesEnvelope] deve retornar JSON com propriedades
    /// raiz "data" e "meta" — shape canônico do envelope.
    /// </summary>
    [Fact]
    public async Task Get_ComEnvelope_RetornaShapeDataMeta()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/test/envelope/com-envelope");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        body.TryGetProperty("data", out _).Should().BeTrue(
            because: "resposta envelopada deve conter propriedade 'data'");

        body.TryGetProperty("meta", out _).Should().BeTrue(
            because: "resposta envelopada deve conter propriedade 'meta'");
    }

    // ── Teste 2: sem [ProducesEnvelope] → não envelopado ─────────────────────

    /// <summary>
    /// Endpoint sem [ProducesEnvelope] deve retornar o payload de domínio diretamente,
    /// sem o wrapper { data, meta }.
    /// </summary>
    [Fact]
    public async Task Get_SemEnvelope_RetornaPayloadDireto()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/test/envelope/sem-envelope");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        body.TryGetProperty("data", out _).Should().BeFalse(
            because: "sem [ProducesEnvelope] a resposta não deve ser envelopada");

        body.TryGetProperty("mensagem", out JsonElement msg).Should().BeTrue();
        msg.GetString().Should().Be("sem envelope");
    }

    // ── Teste 3: NoContent não é modificado ───────────────────────────────────

    /// <summary>
    /// Endpoint com [ProducesEnvelope] que retorna 204 NoContent não deve ser
    /// modificado pelo filtro — não há corpo para envolver.
    /// </summary>
    [Fact]
    public async Task Get_NoContent_ComEnvelopeAttr_Retorna204SemCorpo()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/test/envelope/no-content");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string body = await res.Content.ReadAsStringAsync();
        body.Should().BeEmpty(because: "NoContent não deve ter corpo");
    }

    // ── Teste 4: meta.dataHoraCalculo captura instante do IClock ─────────────

    /// <summary>
    /// O campo meta.dataHoraCalculo deve corresponder ao instante retornado pelo
    /// <c>IClock</c> injetado — prova que o filtro não usa DateTime.UtcNow.
    /// </summary>
    [Fact]
    public async Task Get_ComEnvelope_MetaDataHoraCalculoEhInstanteDoClock()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/test/envelope/com-envelope");

        // Assert
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        JsonElement meta = body.GetProperty("meta");
        meta.TryGetProperty("dataHoraCalculo", out JsonElement dataHora).Should().BeTrue(
            because: "meta deve conter dataHoraCalculo");

        // NodaTime Instant serializa em ISO 8601 UTC (ex: "2026-05-21T12:00:00Z")
        string valorInstante = dataHora.GetString()!;
        valorInstante.Should().NotBeNullOrEmpty(
            because: "dataHoraCalculo deve ser uma string ISO 8601");

        // Verifica que o valor corresponde ao instante fixo do clock fake.
        // InvariantCulture garante parse determinístico independente do locale do runner CI.
        DateTimeOffset parsed = DateTimeOffset.Parse(valorInstante, CultureInfo.InvariantCulture);
        DateTimeOffset esperado = EnvelopeResponseFixture.InstanteFixo.ToDateTimeOffset();
        parsed.Should().Be(esperado,
            because: "dataHoraCalculo deve refletir o IClock injetado, não DateTime.UtcNow");
    }

    // ── Teste 5: meta.completude padrão ──────────────────────────────────────

    /// <summary>
    /// Quando o handler não enriquece o envelope, a completude padrão é "Completo".
    /// </summary>
    [Fact]
    public async Task Get_ComEnvelope_MetaCompletudePadraoEhCompleto()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/test/envelope/com-envelope");

        // Assert
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        JsonElement meta = body.GetProperty("meta");
        meta.GetProperty("completude").GetString().Should().Be("Completo",
            because: "filtro usa Completude.Completo como padrão mínimo");
    }

    // ── Teste 6: meta.fontesConsultadas vazio por padrão ─────────────────────

    /// <summary>
    /// Quando o handler não enriquece o envelope, fontesConsultadas deve ser array vazio.
    /// </summary>
    [Fact]
    public async Task Get_ComEnvelope_MetaFontesConsultadasEhArrayVazio()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/test/envelope/com-envelope");

        // Assert
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        JsonElement fontes = body.GetProperty("meta").GetProperty("fontesConsultadas");
        fontes.ValueKind.Should().Be(JsonValueKind.Array,
            because: "fontesConsultadas deve ser um array JSON");

        fontes.GetArrayLength().Should().Be(0,
            because: "filtro usa lista vazia como padrão quando handler não fornece fontes");
    }
}
