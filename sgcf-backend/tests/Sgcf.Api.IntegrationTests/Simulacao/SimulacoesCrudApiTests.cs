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
/// Fixture para testes E2E do CRUD de Cenários e Simulações.
/// PostgreSQL real via Testcontainers — banco isolado dos demais módulos.
/// Marcada [Slow] — excluir do loop rápido com --filter "Category!=Slow".
/// </summary>
public sealed class SimulacoesCrudApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_simulacoes_crud_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_simulacoes_e2e")
        .Build();

    /// <summary>Instante fixo: 2026-05-19.</summary>
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

[CollectionDefinition("SimulacoesCrudApi")]
#pragma warning disable CA1711
public sealed class SimulacoesCrudApiGroup : ICollectionFixture<SimulacoesCrudApiFixture> { }
#pragma warning restore CA1711

// ── Helpers ───────────────────────────────────────────────────────────────────

internal static class SimulacaoTestHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    internal static object BuildCriarCenarioPayload(string nome = "Cenário Teste", int anoBase = 2026, string? descricao = null) =>
        new { nome, anoBase, descricao };

    internal static object BuildAdicionarSimulacaoPayload(Guid? bancoId = null) =>
        new
        {
            bancoId = bancoId ?? Guid.NewGuid(),
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
        };

    internal static async Task<(HttpResponseMessage Res, JsonElement Body)> PostCenarioAsync(
        HttpClient client, object payload)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/simulacoes/cenarios", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, body);
    }

    internal static async Task<(HttpResponseMessage Res, JsonElement Body)> GetCenarioAsync(
        HttpClient client, Guid cenarioId)
    {
        HttpResponseMessage res = await client.GetAsync($"/api/v1/simulacoes/cenarios/{cenarioId}");
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, body);
    }
}

// ── Testes ────────────────────────────────────────────────────────────────────

/// <summary>
/// Testes E2E para o CRUD REST de Cenários de Simulação e Simulações dentro de Cenário.
/// SPEC §7.4 (Cenário) e §7.5 (Simulações).
/// Fase 2 Task 2.5.
/// </summary>
[Collection("SimulacoesCrudApi")]
[Trait("Category", "Slow")]
public sealed class SimulacoesCrudApiTests(SimulacoesCrudApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private const string CenariosUrl = "/api/v1/simulacoes/cenarios";

    // ── Teste 1: POST /cenarios → 201 Rascunho ───────────────────────────────

    /// <summary>
    /// POST /cenarios deve criar cenário em status Rascunho e retornar 201 com Id.
    /// </summary>
    [Fact]
    public async Task Post_Cenarios_CriaRascunho_Retorna201()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        object payload = SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Rascunho", 2026, "descrição teste");

        // Act
        (HttpResponseMessage res, JsonElement body) = await SimulacaoTestHelper.PostCenarioAsync(client, payload);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created,
            because: $"POST válido deve retornar 201. Body: {body}");

        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("nome").GetString().Should().Be("Cenário Rascunho");
        body.GetProperty("status").GetString().Should().Be("Rascunho");
        body.GetProperty("anoBase").GetInt32().Should().Be(2026);
        body.GetProperty("simulacoes").GetArrayLength().Should().Be(0);

        res.Headers.Location.Should().NotBeNull(
            because: "201 deve incluir header Location apontando para o recurso criado");
    }

    // ── Teste 2: GET /cenarios → lista com filtros ───────────────────────────

    /// <summary>
    /// GET /cenarios com filtro por anoBase deve retornar apenas cenários do ano especificado.
    /// </summary>
    [Fact]
    public async Task Get_Cenarios_ListaComFiltros()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Seed: 2 cenários em 2027, 1 em 2028
        await SimulacaoTestHelper.PostCenarioAsync(client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário 2027 A", 2027));
        await SimulacaoTestHelper.PostCenarioAsync(client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário 2027 B", 2027));
        await SimulacaoTestHelper.PostCenarioAsync(client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário 2028", 2028));

        // Act — filtra por 2027
        HttpResponseMessage res = await client.GetAsync($"{CenariosUrl}?anoBase=2027");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(2,
            because: "deve retornar ao menos os 2 cenários criados com anoBase=2027");

        foreach (JsonElement item in body.EnumerateArray())
        {
            item.GetProperty("anoBase").GetInt32().Should().Be(2027);
        }
    }

    // ── Teste 3: GET /cenarios/{id} → retorna com simulações ─────────────────

    /// <summary>
    /// GET /cenarios/{id} deve retornar o cenário completo com lista de simulações.
    /// </summary>
    [Fact]
    public async Task Get_CenarioPorId_RetornaCenarioComSimulacoes()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário GetById", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        // Act
        (HttpResponseMessage res, JsonElement body) = await SimulacaoTestHelper.GetCenarioAsync(client, cenarioId);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"GET por Id existente deve retornar 200. Body: {body}");

        body.GetProperty("id").GetGuid().Should().Be(cenarioId);
        body.GetProperty("nome").GetString().Should().Be("Cenário GetById");
        body.TryGetProperty("simulacoes", out _).Should().BeTrue(
            because: "resposta de detalhe deve incluir campo 'simulacoes'");
    }

    // ── Teste 4: PATCH /cenarios/{id} → atualiza campos ─────────────────────

    /// <summary>
    /// PATCH em Rascunho deve atualizar nome e retornar o cenário atualizado com 200.
    /// </summary>
    [Fact]
    public async Task Patch_Cenario_EmRascunho_AtualizaCampos()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Nome Original", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        object patchPayload = new { cenarioId, nome = "Nome Atualizado", descricao = "nova desc", anoBase = 2027 };

        // Act
        HttpResponseMessage res = await client.PatchAsJsonAsync($"{CenariosUrl}/{cenarioId}", patchPayload);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"PATCH em Rascunho deve retornar 200. Body: {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("nome").GetString().Should().Be("Nome Atualizado");
        body.GetProperty("anoBase").GetInt32().Should().Be(2027);
    }

    // ── Teste 5: POST /cenarios/{id}/ativar → 200 ────────────────────────────

    /// <summary>
    /// Ativar um cenário Rascunho deve retornar 200 com status "Ativo".
    /// </summary>
    [Fact]
    public async Task Post_AtivarCenario_DeRascunho_Retorna200()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Para Ativar", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        // Act
        HttpResponseMessage res = await client.PostAsync($"{CenariosUrl}/{cenarioId}/ativar", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Ativar Rascunho deve retornar 200. Body: {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetString().Should().Be("Ativo");
    }

    // ── Teste 6: POST /cenarios/{id}/arquivar → 200 ───────────────────────────

    /// <summary>
    /// Arquivar um cenário Ativo deve retornar 200 com status "Arquivado".
    /// AD-11: política Gerencial para arquivar.
    /// </summary>
    [Fact]
    public async Task Post_ArquivarCenario_DeAtivo_Retorna200()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Para Arquivar", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        // Ativar primeiro
        HttpResponseMessage ativar = await client.PostAsync($"{CenariosUrl}/{cenarioId}/ativar", null);
        ativar.StatusCode.Should().Be(HttpStatusCode.OK, because: "deve ativar antes de arquivar");

        // Act — arquivar
        HttpResponseMessage res = await client.PostAsync($"{CenariosUrl}/{cenarioId}/arquivar", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Arquivar Ativo deve retornar 200. Body: {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("status").GetString().Should().Be("Arquivado");
    }

    // ── Teste 7: POST /cenarios/{id}/duplicar → 201 cópia em Rascunho ────────

    /// <summary>
    /// Duplicar deve criar nova cópia em Rascunho com nome "{original} (cópia)" e retornar 201.
    /// SPEC D-10 / Q7.
    /// </summary>
    [Fact]
    public async Task Post_DuplicarCenario_CriaCopiaEmRascunho()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Original", 2026));
        Guid cenarioOrigemId = created.GetProperty("id").GetGuid();

        // Act
        HttpResponseMessage res = await client.PostAsync($"{CenariosUrl}/{cenarioOrigemId}/duplicar", null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created,
            because: $"Duplicar deve retornar 201. Body: {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("id").GetGuid().Should().NotBe(cenarioOrigemId,
            because: "cópia deve ter novo Id");
        body.GetProperty("nome").GetString().Should().Contain("cópia",
            because: "nome da cópia deve indicar que é uma duplicata");
        body.GetProperty("status").GetString().Should().Be("Rascunho");
    }

    // ── Teste 8: DELETE /cenarios/{id} → 204 soft delete ─────────────────────

    /// <summary>
    /// DELETE deve executar soft delete e retornar 204.
    /// GET subsequente deve retornar 404.
    /// </summary>
    [Fact]
    public async Task Delete_Cenario_SoftDelete_Retorna204()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Para Deletar", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        // Act
        HttpResponseMessage res = await client.DeleteAsync($"{CenariosUrl}/{cenarioId}");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "DELETE deve retornar 204 No Content");

        // Verifica que o GET retorna 404 após soft delete
        (HttpResponseMessage getRes, _) = await SimulacaoTestHelper.GetCenarioAsync(client, cenarioId);
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "cenário soft-deletado não deve ser acessível via GET");
    }

    // ── Teste 9: POST /cenarios/{id}/simulacoes → adiciona simulação ──────────

    /// <summary>
    /// POST em cenários/{id}/simulacoes deve adicionar uma simulação e retornar 201 com o cenário atualizado.
    /// </summary>
    [Fact]
    public async Task Post_SimulacoesDoCenario_AdicionaSimulacao()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Com Simulação", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        object simPayload = SimulacaoTestHelper.BuildAdicionarSimulacaoPayload();

        // Act
        HttpResponseMessage res = await client.PostAsJsonAsync($"{CenariosUrl}/{cenarioId}/simulacoes", simPayload);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created,
            because: $"Adicionar simulação deve retornar 201. Body: {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("simulacoes").GetArrayLength().Should().Be(1,
            because: "cenário deve ter exatamente 1 simulação após a adição");
    }

    // ── Teste 10: DELETE /cenarios/{id}/simulacoes/{simId} → remove ──────────

    /// <summary>
    /// DELETE em simulacoes/{simId} deve remover a simulação e retornar 204.
    /// </summary>
    [Fact]
    public async Task Delete_SimulacaoDoCenario_Remove()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        (_, JsonElement created) = await SimulacaoTestHelper.PostCenarioAsync(
            client, SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Remove Sim", 2026));
        Guid cenarioId = created.GetProperty("id").GetGuid();

        // Adicionar simulação
        HttpResponseMessage addRes = await client.PostAsJsonAsync(
            $"{CenariosUrl}/{cenarioId}/simulacoes",
            SimulacaoTestHelper.BuildAdicionarSimulacaoPayload());
        addRes.StatusCode.Should().Be(HttpStatusCode.Created, because: "deve adicionar simulação para depois remover");

        JsonElement addBody = await addRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Guid simId = addBody.GetProperty("simulacoes")[0].GetProperty("id").GetGuid();

        // Act
        HttpResponseMessage res = await client.DeleteAsync($"{CenariosUrl}/{cenarioId}/simulacoes/{simId}");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: $"DELETE de simulação deve retornar 204. Body: {await res.Content.ReadAsStringAsync()}");

        // Verificar que a simulação foi removida
        (_, JsonElement cenario) = await SimulacaoTestHelper.GetCenarioAsync(client, cenarioId);
        cenario.GetProperty("simulacoes").GetArrayLength().Should().Be(0,
            because: "cenário deve ter 0 simulações após remoção");
    }

    // ── Teste 11: Idempotência via Idempotency-Key ────────────────────────────

    /// <summary>
    /// Duas requisições POST com o mesmo Idempotency-Key devem retornar o mesmo Id de cenário.
    /// Valida o comportamento do IdempotencyFilter.
    /// </summary>
    [Fact]
    public async Task Post_Cenarios_ComMesmoIdempotencyKey_RetornaMesmoId_Em2xRequest()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        string idempotencyKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        object payload = SimulacaoTestHelper.BuildCriarCenarioPayload("Cenário Idempotente", 2026);

        // Act — primeira requisição
        (HttpResponseMessage res1, JsonElement body1) = await SimulacaoTestHelper.PostCenarioAsync(client, payload);
        Guid id1 = body1.GetProperty("id").GetGuid();

        // Act — segunda requisição com mesmo Idempotency-Key
        (HttpResponseMessage res2, JsonElement body2) = await SimulacaoTestHelper.PostCenarioAsync(client, payload);
        Guid id2 = body2.GetProperty("id").GetGuid();

        // Assert
        res1.IsSuccessStatusCode.Should().BeTrue(because: "primeira requisição deve ter sucesso");
        res2.IsSuccessStatusCode.Should().BeTrue(because: "segunda requisição deve ter sucesso (cache hit)");
        id1.Should().Be(id2,
            because: "mesmo Idempotency-Key deve retornar o mesmo cenário — IdempotencyFilter em ação");
    }

    // ── Teste 12 (bônus): GET inexistente → 404 ───────────────────────────────

    /// <summary>
    /// GET de cenário inexistente deve retornar 404.
    /// </summary>
    [Fact]
    public async Task Get_CenarioInexistente_Retorna404()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        Guid idInexistente = Guid.NewGuid();

        // Act
        (HttpResponseMessage res, _) = await SimulacaoTestHelper.GetCenarioAsync(client, idInexistente);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
