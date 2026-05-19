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
/// Fixture para testes E2E do endpoint de conveniência
/// <c>GET /api/v1/simulacoes/cenarios/{id}/quadro-divida</c>.
///
/// PostgreSQL real via Testcontainers — banco isolado do <c>SimulacoesCrudApiFixture</c>.
/// Clock fixado em 2026-05-19 → ano corrente = 2026, alinhado com o cenário AnoBase = 2026.
/// </summary>
public sealed class CenarioQuadroDividaAtalhoApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_cenario_quadro_atalho_e2e")
        .WithUsername("sgcf")
        .WithPassword("sgcf_cenario_atalho_e2e")
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

[CollectionDefinition("CenarioQuadroDividaAtalhoApi")]
#pragma warning disable CA1711
public sealed class CenarioQuadroDividaAtalhoApiGroup : ICollectionFixture<CenarioQuadroDividaAtalhoApiFixture> { }
#pragma warning restore CA1711

// ── Testes ────────────────────────────────────────────────────────────────────

/// <summary>
/// Testes E2E do endpoint de conveniência
/// <c>GET /api/v1/simulacoes/cenarios/{id}/quadro-divida</c> (Task 3.3, Fase 3).
///
/// Este endpoint é um atalho que:
///   1. Busca o cenário para obter <c>AnoBase</c>.
///   2. Delega para <c>GetQuadroDividaQuery</c> passando o <c>cenarioId</c> (Task 3.1).
///
/// NOTA DE COORDENAÇÃO (Task 3.1 paralela):
///   Task 3.1 ainda pode estar em desenvolvimento. O endpoint implementado nesta task
///   chama <c>GetQuadroDividaQuery(anoEfetivo, id)</c> (com cenarioId). Se 3.1 ainda
///   não aceitou o parâmetro, o overload sem cenarioId é usado como fallback e os testes
///   de integração do cenário aplicado falharão até 3.1 mergear — comportamento esperado.
///   Os testes de rota e de cenário inexistente são independentes de 3.1 e devem passar
///   desde o GREEN desta task.
/// </summary>
[Collection("CenarioQuadroDividaAtalhoApi")]
[Trait("Category", "Slow")]
public sealed class CenarioQuadroDividaAtalhoApiTests(CenarioQuadroDividaAtalhoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private const string CenariosBaseUrl = "/api/v1/simulacoes/cenarios";

    // ── Helpers de seed ───────────────────────────────────────────────────────

    /// <summary>
    /// Cria cenário via POST e retorna o Id gerado.
    /// AnoBase = 2026 por padrão, alinhado ao clock fixo da fixture (2026).
    /// </summary>
    private static async Task<Guid> CriarCenarioAsync(
        HttpClient client,
        string nome = "Cenário Quadro Atalho",
        int anoBase = 2026)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync(CenariosBaseUrl, new
        {
            nome,
            anoBase,
            descricao = "Criado para teste de conveniência quadro-divida"
        });

        res.IsSuccessStatusCode.Should().BeTrue(
            $"seed cenário falhou ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");

        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    // ── Teste 1: AnoBase do cenário é usado como padrão ──────────────────────

    /// <summary>
    /// Chama o endpoint sem query <c>?ano=</c> e verifica que o campo <c>ano</c>
    /// na resposta é igual ao <c>AnoBase</c> do cenário (2026).
    ///
    /// O endpoint deve usar <c>cenario.AnoBase</c> automaticamente quando o override
    /// <c>?ano=</c> não é fornecido (SPEC §7.3, critério "Usa AnoBase como default").
    /// </summary>
    [Fact]
    public async Task Get_QuadroDividaPorCenario_UsaCenarioAnoBaseComoDefault()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        Guid cenarioId = await CriarCenarioAsync(client, "Cenário Default AnoBase", anoBase: 2026);

        // Act — sem parâmetro ?ano, deve usar AnoBase=2026 do cenário
        HttpResponseMessage response = await client.GetAsync(
            $"{CenariosBaseUrl}/{cenarioId}/quadro-divida");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"endpoint de conveniência deve retornar 200 para cenário existente. " +
            $"Body: {await response.Content.ReadAsStringAsync()}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        body.GetProperty("ano").GetInt32().Should().Be(2026,
            "sem ?ano= o endpoint deve usar cenario.AnoBase (2026) como ano efetivo");

        // Estrutura básica do QuadroDividaDto
        body.GetProperty("projecao").GetProperty("meses").GetArrayLength().Should().Be(12,
            "a projeção sempre tem 12 meses");

        body.GetProperty("alertas").GetArrayLength().Should().BeGreaterThanOrEqualTo(0,
            "campo alertas deve existir no DTO");
    }

    // ── Teste 2: Override de ano aceito quando igual ao AnoBase ──────────────

    /// <summary>
    /// Fornece <c>?ano=2026</c> explicitamente (igual ao AnoBase do cenário) e
    /// verifica que o endpoint retorna 200 com o ano correto.
    ///
    /// O override por query string é permitido para fins de teste e consistência
    /// com o endpoint direto <c>/painel/quadro-divida?ano=YYYY&amp;cenarioId=...</c>.
    /// Quando o ano override == AnoBase, o resultado deve ser idêntico ao sem override.
    /// </summary>
    [Fact]
    public async Task Get_QuadroDividaPorCenario_AceitaAnoOverride_QuandoIgualAoCenarioAnoBase()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        Guid cenarioId = await CriarCenarioAsync(client, "Cenário Override Ano", anoBase: 2026);

        // Act — passa ?ano=2026 explicitamente (override = AnoBase)
        HttpResponseMessage response = await client.GetAsync(
            $"{CenariosBaseUrl}/{cenarioId}/quadro-divida?ano=2026");

        // Assert — deve retornar 200 com ano=2026
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"override ?ano=2026 == AnoBase deve ser aceito. " +
            $"Body: {await response.Content.ReadAsStringAsync()}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("ano").GetInt32().Should().Be(2026);

        // 12 meses na projeção
        body.GetProperty("projecao").GetProperty("meses").GetArrayLength().Should().Be(12);
    }

    // ── Teste 3: Cenário inexistente → 404 ───────────────────────────────────

    /// <summary>
    /// Chama o endpoint com um Id de cenário que não existe e verifica que retorna 404.
    ///
    /// O endpoint deve rejeitar silenciosamente (sem stack trace) qualquer Id sem
    /// correspondência no banco — comportamento consistente com
    /// <c>GET /api/v1/simulacoes/cenarios/{id}</c>.
    /// </summary>
    [Fact]
    public async Task Get_QuadroDividaPorCenario_CenarioInexistente_Retorna404()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        Guid idInexistente = Guid.NewGuid(); // UUID aleatório — garantidamente não existe

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"{CenariosBaseUrl}/{idInexistente}/quadro-divida");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cenário inexistente deve retornar 404 Not Found");
    }

    // ── Teste 4: Rota de atalho retorna mesma estrutura que endpoint direto ───

    /// <summary>
    /// Cria um cenário, chama o endpoint de conveniência e verifica que a estrutura
    /// retornada é compatível com o <c>QuadroDividaDto</c> do endpoint direto.
    ///
    /// Este teste valida que o atalho é de fato um atalho — não um endpoint com
    /// schema diferente. Os campos obrigatórios <c>ano</c>, <c>projecao</c>,
    /// <c>sumario</c>, <c>snapshotInicial</c> e <c>alertas</c> devem estar presentes.
    ///
    /// NOTA: A igualdade numérica entre o atalho e a chamada direta ao
    /// <c>/painel/quadro-divida?cenarioId=...</c> só pode ser validada após Task 3.1
    /// ser mergeada. Este teste verifica apenas que o schema é idêntico.
    /// </summary>
    [Fact]
    public async Task Get_QuadroDividaPorCenario_RotaDeAtalho_RetornaSchemaCompativel()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();
        Guid cenarioId = await CriarCenarioAsync(client, "Cenário Schema Atalho", anoBase: 2026);

        // Act — endpoint de conveniência (atalho)
        HttpResponseMessage responseAtalho = await client.GetAsync(
            $"{CenariosBaseUrl}/{cenarioId}/quadro-divida");

        // Assert — deve retornar 200
        responseAtalho.StatusCode.Should().Be(HttpStatusCode.OK,
            $"atalho deve retornar 200. Body: {await responseAtalho.Content.ReadAsStringAsync()}");

        JsonElement bodyAtalho = await responseAtalho.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // Campos obrigatórios do QuadroDividaDto devem estar presentes no atalho
        bodyAtalho.TryGetProperty("ano", out _).Should().BeTrue("campo 'ano' deve existir");
        bodyAtalho.TryGetProperty("dataReferencia", out _).Should().BeTrue("campo 'dataReferencia' deve existir");
        bodyAtalho.TryGetProperty("snapshotInicial", out _).Should().BeTrue("campo 'snapshotInicial' deve existir");
        bodyAtalho.TryGetProperty("projecao", out _).Should().BeTrue("campo 'projecao' deve existir");
        bodyAtalho.TryGetProperty("sumario", out _).Should().BeTrue("campo 'sumario' deve existir");
        bodyAtalho.TryGetProperty("alertas", out _).Should().BeTrue("campo 'alertas' deve existir");

        // O campo ano do atalho deve corresponder ao AnoBase do cenário
        bodyAtalho.GetProperty("ano").GetInt32().Should().Be(2026,
            "o atalho deve usar AnoBase do cenário como ano da projeção");

        // A projeção deve ter exatamente 12 meses
        bodyAtalho.GetProperty("projecao").GetProperty("meses").GetArrayLength()
            .Should().Be(12, "projeção sempre tem 12 meses");

        // O sumário deve ter os campos esperados
        JsonElement sumario = bodyAtalho.GetProperty("sumario");
        sumario.TryGetProperty("saldoTotalInicioAno", out _).Should().BeTrue("sumario.saldoTotalInicioAno deve existir");
        sumario.TryGetProperty("saldoTotalFimAno", out _).Should().BeTrue("sumario.saldoTotalFimAno deve existir");
        sumario.TryGetProperty("totalAmortizacaoNoAno", out _).Should().BeTrue("sumario.totalAmortizacaoNoAno deve existir");
        sumario.TryGetProperty("totalCaptacaoNoAno", out _).Should().BeTrue("sumario.totalCaptacaoNoAno deve existir");
    }
}
