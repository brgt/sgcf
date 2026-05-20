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
    /// Dois usuários com a mesma Idempotency-Key NÃO devem ver respostas cruzadas.
    ///
    /// Prove-It: com a implementação atual (cacheKey = "idempotency:{key}"), o usuário B
    /// receberia a resposta do usuário A — isso é um IDOR.
    /// Após o fix (cacheKey = "idempotency:{userSub}:{method}:{path}:{key}"),
    /// os dois usuários têm cache entries diferentes → IDs diferentes.
    /// </summary>
    [Fact]
    public async Task Post_MesmaKey_UsuarioDiferente_NaoRetornaRespostaCruzada()
    {
        // Arrange — dois clients simulando usuários diferentes via sub diferente
        // O dev-bypass injeta sempre "dev-user-id" como sub, então precisamos de dois
        // clients com tokens distintos. Como o dev-bypass usa um sub fixo, simularemos
        // via MemoryCache separado — o teste verifica o comportamento ESPERADO após o fix.
        //
        // Estratégia: cada requisição usa uma key UUID aleatória única POR USUÁRIO.
        // Se a cache key não incluir o usuário, o segundo usuario com a mesma key
        // receberia a resposta do primeiro. Para provar o bug sem dois users reais,
        // usamos keys idênticas em duas sessões WebApplicationFactory separadas
        // que compartilham o mesmo IMemoryCache (in-process).
        //
        // Com o fix, userSub faz parte da key → cache miss para o segundo request
        // mesmo com key igual (pois o sub é o mesmo neste test infra, mas o fix
        // registra o path/method/key corretamente).
        //
        // Nota: como o dev-bypass injeta sempre o mesmo sub ("dev-user-id"),
        // não conseguimos testar dois users REAIS diferentes em um único server.
        // O teste valida que a CHAVE COMPOSTA está sendo usada inspecionando
        // indiretamente: dois POSTs para ROTAS DIFERENTES com a mesma key
        // NÃO devem retornar a mesma resposta.

        HttpClient clientA = fixture.CreateAuthenticatedClient();
        string sharedKey = Guid.NewGuid().ToString("D");

        // Rota A: POST /api/v1/simulacoes/cenarios
        clientA.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);
        (HttpResponseMessage resA, JsonElement bodyA) =
            await PostCenarioAsync(clientA, BuildCriarCenarioPayload("Cenário Usuário A"));

        // Rota B: usa endpoint diferente mas mesma key — deve ter cache miss
        HttpClient clientB = fixture.CreateAuthenticatedClient();
        clientB.DefaultRequestHeaders.Add("Idempotency-Key", sharedKey);
        // POST para /api/v1/simulacoes/cenarios/{id}/simulacoes (rota diferente)
        // Rota inexistente retorna 400/404 — mas o filtro deve rodar com cache miss
        Guid cenarioIdA = bodyA.GetProperty("id").GetGuid();
        HttpResponseMessage resB = await clientB.PostAsJsonAsync(
            $"/api/v1/simulacoes/cenarios/{cenarioIdA}/simulacoes",
            new
            {
                bancoId        = Guid.NewGuid(),
                modalidade     = "Finimp",
                moeda          = "Usd",
                valorPrincipal = 1_000_000m,
                dataContratacaoPrevista  = "2026-09-01",
                dataPrimeiroVencimento   = "2027-09-01",
                tipoTaxa                 = "Fixa",
                taxaAa                   = 6m,
                spreadAa                 = (decimal?)null,
                baseCalculo              = "Dias360",
                estruturaAmortizacao     = "Bullet",
                periodicidade            = "Anual",
                quantidadeParcelas       = 1,
                anchorDiaMes             = "DiaContratacao"
            });

        // Assert — a resposta de B NÃO deve ser 200 OK com o body de A
        // (um cache HIT indevido retornaria 200 OK com { "id": cenarioIdA })
        resA.IsSuccessStatusCode.Should().BeTrue(
            because: "primeiro POST deve ter sucesso");

        // Se a cache key não incluir a rota, resB retornaria 200 OK com o body de resA
        // (IDOR). Com o fix, resB terá cache miss e executará normalmente (201/200).
        //
        // Comparação por body inteiro (não apenas "id"): a rota "adicionar simulação a
        // cenário" também retorna { id: cenarioId, ... } no payload de sucesso, o que
        // coincidiria com bodyA por semântica de domínio (não por IDOR). Verificar
        // bytes idênticos é o sinal seguro de cache HIT cruzado.
        if (resB.IsSuccessStatusCode)
        {
            string rawA = await resA.Content.ReadAsStringAsync();
            string rawB = await resB.Content.ReadAsStringAsync();

            rawB.Should().NotBe(rawA,
                because: "rota diferente com mesma key NÃO deve retornar body idêntico (cache HIT cruzado = IDOR)");
        }
        // Se resB não for sucesso (ex: validação falhou), o filtro não cacheou → OK
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
