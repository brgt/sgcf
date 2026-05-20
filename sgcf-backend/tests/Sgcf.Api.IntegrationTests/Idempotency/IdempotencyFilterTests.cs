using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

namespace Sgcf.Api.IntegrationTests.Idempotency;

// ── Fixture ───────────────────────────────────────────────────────────────────

/// <summary>
/// Fixture para testes do IdempotencyFilter.
/// PostgreSQL real via Testcontainers — banco isolado dos demais módulos.
/// </summary>
[Trait("Category", "Slow")]
public sealed class IdempotencyFilterFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_idempotency_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_idempotency_e2e")
        .Build();

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

                // Replace the dev JwtBearer with TestAuthHandler so tests can inject
                // arbitrary sub values via X-Test-User-Sub header. This allows true
                // multi-user cache-isolation testing without modifying production code.
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
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>Cria HttpClient autenticado com sub padrão (<c>dev-user-id</c>).</summary>
    public HttpClient CreateAuthenticatedClient() =>
        CreateAuthenticatedClient("dev-user-id");

    /// <summary>
    /// Cria HttpClient autenticado injetando <paramref name="subOverride"/> como
    /// <c>sub</c> do usuário via header <c>X-Test-User-Sub</c>.
    /// Permite simular múltiplos usuários distintos no mesmo servidor de teste.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string subOverride)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, subOverride);
        return client;
    }
}

[CollectionDefinition("IdempotencyFilter")]
#pragma warning disable CA1711
public sealed class IdempotencyFilterGroup : ICollectionFixture<IdempotencyFilterFixture> { }
#pragma warning restore CA1711

// ── Testes ────────────────────────────────────────────────────────────────────

/// <summary>
/// Testes de segurança e comportamento do <see cref="Sgcf.Api.Filters.IdempotencyFilter"/>.
///
/// Cenários críticos de segurança (Prove-It pattern):
///   - Mesma key, mesmo usuário, mesma rota → cache HIT (comportamento original preservado)
///   - Mesma key, usuário diferente → cache MISS (escopo de usuário impede IDOR)
///   - Mesma key, rota diferente → cache MISS (escopo de rota impede cross-resource hit)
///   - Key inválida (não-UUID) → 400 Bad Request
///   - Key com caracteres especiais → 400 Bad Request
///
/// Estes testes foram escritos no estado RED — falham enquanto IdempotencyFilter
/// não compõe a cache key com userSub+method+path (security fix #3).
/// </summary>
[Collection("IdempotencyFilter")]
[Trait("Category", "Slow")]
[Trait("Category", "Security")]
public sealed class IdempotencyFilterTests(IdempotencyFilterFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object BuildCriarCenarioPayload(string nome = "Cenário Idem", int anoBase = 2026) =>
        new { nome, anoBase };

    private static async Task<(HttpResponseMessage Res, JsonElement Body)> PostCenarioAsync(
        HttpClient client, object payload)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/simulacoes/cenarios", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, body);
    }

    // ── Teste 1: comportamento original preservado ────────────────────────────

    /// <summary>
    /// Mesmo usuário, mesma key, mesma rota → segunda requisição deve retornar o mesmo ID.
    /// Garante que o fix não quebrou o comportamento de deduplicação.
    /// </summary>
    [Fact]
    public async Task Post_MesmaKey_MesmoUsuario_MesmaRota_RetornaMesmaResposta()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        string key = Guid.NewGuid().ToString("D");
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);

        object payload = BuildCriarCenarioPayload("Cenário Idempotente Preservado");

        // Act
        (HttpResponseMessage res1, JsonElement body1) = await PostCenarioAsync(client, payload);
        (HttpResponseMessage res2, JsonElement body2) = await PostCenarioAsync(client, payload);

        // Assert — mesma key + mesmo usuário + mesma rota deve retornar mesmo cenário
        res1.IsSuccessStatusCode.Should().BeTrue(because: "primeira requisição deve ter sucesso");
        res2.IsSuccessStatusCode.Should().BeTrue(because: "segunda requisição deve ter sucesso (cache hit)");

        Guid id1 = body1.GetProperty("id").GetGuid();
        Guid id2 = body2.GetProperty("id").GetGuid();
        id1.Should().Be(id2,
            because: "mesmo usuário + mesma key + mesma rota → cache HIT, mesmo ID retornado");
    }

    // ── Teste 2 (Prove-It): IDOR via key sem escopo de usuário ───────────────

    /// <summary>
    /// Dois usuários distintos com a mesma Idempotency-Key devem receber IDs diferentes.
    ///
    /// Prove-It direto: sem o escopo de userSub na cache key, a resposta do usuário A
    /// seria retornada para o usuário B (IDOR). Com o fix, cada userSub gera uma
    /// cache entry separada, e o segundo POST cria um novo cenário com ID diferente.
    ///
    /// Usa <see cref="TestAuthHandler"/> para injetar sub distintos via
    /// <c>X-Test-User-Sub</c> header — sem depender do dev-bypass de sub fixo.
    /// </summary>
    [Fact]
    public async Task Post_MesmaKey_UsuarioDiferente_NaoRetornaRespostaCruzada()
    {
        // Arrange — dois clients com subs REAIS diferentes na mesma rota + mesma key
        string sharedKey = Guid.NewGuid().ToString("D");

        HttpClient clientA = fixture.CreateAuthenticatedClient("user-alice");
        clientA.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);

        HttpClient clientB = fixture.CreateAuthenticatedClient("user-bob");
        clientB.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);

        // Act — mesma rota, mesma key, mas usuários diferentes
        (HttpResponseMessage resA, JsonElement bodyA) =
            await PostCenarioAsync(clientA, BuildCriarCenarioPayload("Cenário Usuário A"));

        (HttpResponseMessage resB, JsonElement bodyB) =
            await PostCenarioAsync(clientB, BuildCriarCenarioPayload("Cenário Usuário B"));

        // Assert
        resA.IsSuccessStatusCode.Should().BeTrue(
            because: "POST do usuário A deve ter sucesso");

        resB.IsSuccessStatusCode.Should().BeTrue(
            because: "POST do usuário B deve ter sucesso (cache miss, não HIT cruzado)");

        Guid idA = bodyA.GetProperty("id").GetGuid();
        Guid idB = bodyB.GetProperty("id").GetGuid();

        // Sem o escopo de userSub, idB == idA (IDOR: B recebe resposta de A).
        // Com o fix, a cache key inclui o sub → cache miss para B → novo recurso criado.
        idA.Should().NotBe(idB,
            because: "usuários diferentes com a mesma Idempotency-Key devem criar " +
                     "recursos independentes — cache scoped por userSub previne IDOR");
    }

    // ── Teste 3 (Prove-It): key sem escopo de rota ───────────────────────────

    /// <summary>
    /// Mesma key, rota diferente → cache MISS obrigatório.
    ///
    /// Prove-It direto: POST /cenarios com key K → ID-1.
    /// Caso a cache não inclua a rota, um GET /cenarios com key K retornaria
    /// a resposta do POST (tipo de resposta errado para o verbo errado).
    /// </summary>
    [Fact]
    public async Task Post_MesmaKey_RotaDiferente_NaoRetornaRespostaCruzada()
    {
        // Arrange — POST cria um cenário com key K
        HttpClient clientPost = fixture.CreateAuthenticatedClient();
        string sharedKey = Guid.NewGuid().ToString("D");
        clientPost.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);

        (HttpResponseMessage resPost, JsonElement bodyPost) =
            await PostCenarioAsync(clientPost, BuildCriarCenarioPayload("Cenário Rota Cruzada"));
        resPost.IsSuccessStatusCode.Should().BeTrue();
        Guid cenarioId = bodyPost.GetProperty("id").GetGuid();

        // Act — GET na mesma rota com a mesma key
        // Com cache sem escopo de método, o GET retornaria o body do POST (200 OK + body)
        // em vez de executar a query real.
        HttpClient clientGet = fixture.CreateAuthenticatedClient();
        clientGet.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);
        HttpResponseMessage resGet = await clientGet.GetAsync("/api/v1/simulacoes/cenarios");

        // Assert — o GET deve executar normalmente (retornar a lista, não o body do POST)
        resGet.IsSuccessStatusCode.Should().BeTrue(
            because: "GET /cenarios deve funcionar independente do cache do POST");

        string rawGet = await resGet.Content.ReadAsStringAsync();
        JsonElement bodyGet = JsonSerializer.Deserialize<JsonElement>(rawGet, JsonOpts);

        // Um cache HIT indevido (sem escopo de método) retornaria um objeto com "id"
        // em vez de um array de cenários
        bodyGet.ValueKind.Should().Be(JsonValueKind.Array,
            because: "GET /cenarios deve retornar array, não o objeto do POST (cache cruzado por método/rota)");
    }

    // ── Teste 4: key inválida → 400 ───────────────────────────────────────────

    /// <summary>
    /// Idempotency-Key com formato inválido (não UUID e não alfanumérico restrito)
    /// deve retornar 400 Bad Request sem processar a requisição.
    /// </summary>
    [Fact]
    public async Task Post_KeyInvalida_NaoUuid_Retorna400()
    {
        // Arrange — key inválida: muito curta, sem formato UUID e não alfanumérica
        HttpClient client = fixture.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", "nao-e-uuid!!!");

        // Act
        HttpResponseMessage res = await client.PostAsJsonAsync(
            "/api/v1/simulacoes/cenarios",
            BuildCriarCenarioPayload("Cenário Key Inválida"));

        // Assert — filtro deve rejeitar antes de chegar no controller
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "Idempotency-Key com formato inválido deve retornar 400");
    }

    // ── Teste 5: key com caracteres especiais → 400 ───────────────────────────

    /// <summary>
    /// Idempotency-Key com script injection / caracteres proibidos deve ser rejeitada.
    /// Previne cache poisoning via key crafted.
    /// </summary>
    [Fact]
    public async Task Post_KeyComCaracteresEspeciais_Retorna400()
    {
        // Arrange — key com injeção de caracteres especiais
        HttpClient client = fixture.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", "../../../etc/passwd");

        // Act
        HttpResponseMessage res = await client.PostAsJsonAsync(
            "/api/v1/simulacoes/cenarios",
            BuildCriarCenarioPayload("Cenário Key Perigosa"));

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "Idempotency-Key com path traversal deve ser rejeitada com 400");
    }
}
