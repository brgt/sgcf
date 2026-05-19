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

using Sgcf.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Simulacao;

// ── Fixture ───────────────────────────────────────────────────────────────────

/// <summary>
/// Fixture dedicada ao endpoint <c>POST /api/v1/simulacoes/comparar</c>.
///
/// PostgreSQL real via Testcontainers — banco isolado dos demais módulos.
/// Clock fixado em 2026-05-19 → ano corrente = 2026, alinhado com AnoBase dos cenários criados.
/// </summary>
public sealed class CompararCenariosApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_comparar_cenarios_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_comparar_e2e")
        .Build();

    /// <summary>Instante fixo: 2026-05-19 10:00 UTC → ano corrente = 2026.</summary>
    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 10, 0);

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
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>Cria HttpClient autenticado com token de desenvolvimento.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }
}

[CollectionDefinition("CompararCenariosApi")]
#pragma warning disable CA1711
public sealed class CompararCenariosApiGroup : ICollectionFixture<CompararCenariosApiFixture> { }
#pragma warning restore CA1711

// ── Testes ────────────────────────────────────────────────────────────────────

/// <summary>
/// Testes E2E do endpoint <c>POST /api/v1/simulacoes/comparar</c> (Task 4.1, Fase 4).
///
/// O endpoint recebe uma lista de <c>cenarioIds</c> e retorna o
/// <c>ResultadoComparacaoCenariosDto</c> com projeção de cada cenário e
/// deltas mensais/anuais em relação ao primeiro (baseline).
///
/// Padrão de isolamento: cada teste cria os próprios cenários via POST
/// (sem compartilhar estado com outros testes da collection).
/// </summary>
[Collection("CompararCenariosApi")]
[Trait("Category", "Slow")]
public sealed class CompararCenariosApiTests(CompararCenariosApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private const string CenariosBaseUrl = "/api/v1/simulacoes/cenarios";
    private const string CompararUrl = "/api/v1/simulacoes/comparar";

    // ── Helpers de seed ───────────────────────────────────────────────────────

    /// <summary>
    /// Cria um cenário via POST e retorna o Id gerado.
    /// AnoBase = 2026 por padrão, alinhado ao clock fixo da fixture.
    /// </summary>
    private static async Task<Guid> CriarCenarioAsync(
        HttpClient client,
        string nome,
        int anoBase = 2026)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync(CenariosBaseUrl, new
        {
            nome,
            anoBase,
            descricao = $"Criado para teste CompararCenarios: {nome}"
        });

        res.IsSuccessStatusCode.Should().BeTrue(
            $"seed cenário '{nome}' falhou ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    // ── Teste 7: 3 cenários → 200 com estrutura de deltas ────────────────────

    /// <summary>
    /// Cria 3 cenários (sem simulações — projeções idênticas ao cenário base real),
    /// chama o endpoint comparar e verifica:
    /// - Status 200
    /// - Campo <c>cenarios</c> com 3 entradas
    /// - Primeiro cenário é baseline (<c>ehBaseline = true</c>, deltas ausentes)
    /// - Demais cenários têm <c>deltasMensais</c> com 12 meses e <c>deltaAnual</c>
    /// </summary>
    [Fact]
    public async Task Post_Comparar_3Cenarios_Retorna200_ComDeltas()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        Guid idA = await CriarCenarioAsync(client, "Comparar Baseline A");
        Guid idB = await CriarCenarioAsync(client, "Comparar Cenário B");
        Guid idC = await CriarCenarioAsync(client, "Comparar Cenário C");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(CompararUrl, new
        {
            ano = 2026,
            cenarioIds = new[] { idA, idB, idC }
        });

        // Assert — 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"3 cenários válidos devem retornar 200. Body: {await response.Content.ReadAsStringAsync()}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("ano").GetInt32().Should().Be(2026);
        body.TryGetProperty("dataReferencia", out _).Should().BeTrue("campo dataReferencia deve existir");

        JsonElement cenarios = body.GetProperty("cenarios");
        cenarios.GetArrayLength().Should().Be(3, "3 cenários enviados = 3 cenários na resposta");

        // Primeiro: baseline
        JsonElement baseline = cenarios[0];
        baseline.GetProperty("ehBaseline").GetBoolean().Should().BeTrue();
        baseline.TryGetProperty("deltasMensais", out JsonElement deltasMensaisBaseline).Should().BeTrue();
        deltasMensaisBaseline.ValueKind.Should().Be(JsonValueKind.Null,
            "baseline não tem deltas mensais");
        baseline.TryGetProperty("deltaAnual", out JsonElement deltaAnualBaseline).Should().BeTrue();
        deltaAnualBaseline.ValueKind.Should().Be(JsonValueKind.Null,
            "baseline não tem delta anual");

        // Segundo: tem deltas
        JsonElement cenarioB = cenarios[1];
        cenarioB.GetProperty("ehBaseline").GetBoolean().Should().BeFalse();
        cenarioB.GetProperty("deltasMensais").GetArrayLength().Should().Be(12,
            "deltas mensais deve ter 12 entradas");
        cenarioB.TryGetProperty("deltaAnual", out JsonElement deltaAnualB).Should().BeTrue();
        deltaAnualB.ValueKind.Should().NotBe(JsonValueKind.Null, "cenário B deve ter deltaAnual");
    }

    // ── Teste 8: 6 cenários → 400 ────────────────────────────────────────────

    /// <summary>
    /// Mais de 5 cenários deve resultar em 400 Bad Request.
    /// Limite operacional: máximo 5 cenários por chamada.
    /// </summary>
    [Fact]
    public async Task Post_Comparar_6Cenarios_Retorna400()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // 6 UUIDs aleatórios — não precisam existir porque a validação ocorre antes
        Guid[] seisIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(CompararUrl, new
        {
            ano = 2026,
            cenarioIds = seisIds
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "6 cenários excedem o limite de 5 — deve retornar 400");
    }

    // ── Teste 9: cenário inexistente → 404 ───────────────────────────────────

    /// <summary>
    /// Quando um dos <c>cenarioIds</c> não existe no banco, o endpoint deve
    /// retornar 404 Not Found — sem stack trace ou detalhes internos.
    /// </summary>
    [Fact]
    public async Task Post_Comparar_CenarioInexistente_Retorna404()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        Guid idInexistente = Guid.NewGuid(); // UUID aleatório — garantidamente não existe

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(CompararUrl, new
        {
            ano = 2026,
            cenarioIds = new[] { idInexistente }
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cenário inexistente deve retornar 404 Not Found");
    }
}
