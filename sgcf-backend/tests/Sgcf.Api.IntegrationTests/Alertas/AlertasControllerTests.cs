using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Sgcf.Domain.Alertas;
using Sgcf.Infrastructure.Persistence;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Alertas;

/// <summary>
/// Testes de integração HTTP do módulo de Alertas.
/// Cada teste usa a API HTTP completa via WebApplicationFactory + PostgreSQL real.
/// Task 0.3 — AlertasController endpoints.
/// </summary>
[Collection("AlertasApi")]
[Trait("Category", "Slow")]
public sealed class AlertasControllerTests(AlertasApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Semeia um alerta diretamente no banco para o tenant informado.
    /// Contorna o TenantSaveInterceptor (que exige ITenantContext resolvido)
    /// via EF property para setar o TenantId no registro.
    /// </summary>
    private async Task<Guid> SeedAlertaAsync(
        Guid tenantId,
        string chaveIdempotencia,
        StatusAlerta status = StatusAlerta.Aberto,
        SeveridadeAlerta severidade = SeveridadeAlerta.Critico,
        PerfilCockpit perfil = PerfilCockpit.Tesouraria)
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        SgcfDbContext ctx   = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        IClock clock        = scope.ServiceProvider.GetRequiredService<IClock>();

        Alerta alerta = Alerta.Criar(
            categoria:         CategoriaAlerta.Vencimento,
            severidade:        severidade,
            titulo:            $"Alerta teste {chaveIdempotencia}",
            descricao:         "Descrição de teste para integração.",
            origemTipo:        "Contrato",
            origemId:          null,
            perfisVisiveis:    [perfil],
            chaveIdempotencia: chaveIdempotencia,
            clock:             clock);

        // Bypass do TenantSaveInterceptor: seta TenantId diretamente via EF property,
        // o mesmo mecanismo que o interceptor usa internamente.
        await ctx.Alertas.AddAsync(alerta);
        ctx.Entry(alerta).Property("TenantId").CurrentValue = tenantId;

        if (status == StatusAlerta.Dispensado)
        {
            alerta.Dispensar(clock);
        }
        else if (status == StatusAlerta.Lido)
        {
            alerta.MarcarComoLido(clock);
        }

        await ctx.SaveChangesAsync();
        return alerta.Id;
    }

    // ── GET /api/v1/alertas ───────────────────────────────────────────────────

    [Fact]
    public async Task GET_alertas_retorna_200_com_envelope_data_e_meta()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/alertas");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        body.TryGetProperty("data", out _).Should().BeTrue(
            because: "GET /alertas deve retornar envelope com propriedade 'data'");
        body.TryGetProperty("meta", out _).Should().BeTrue(
            because: "GET /alertas deve retornar envelope com propriedade 'meta'");
    }

    [Fact]
    public async Task GET_alertas_data_contem_items_e_total()
    {
        // Arrange
        Guid tenantId = new("00000000-0000-7000-8000-000000000001"); // ProxysDevTenant
        await SeedAlertaAsync(tenantId, $"list-items-test-{Guid.NewGuid()}");

        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/alertas");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        JsonElement data = body.GetProperty("data");
        data.TryGetProperty("items", out _).Should().BeTrue(
            because: "data deve conter 'items' (lista de alertas)");
        data.TryGetProperty("total", out _).Should().BeTrue(
            because: "data deve conter 'total' para paginação");
        data.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GET_alertas_sem_autenticacao_retorna_401()
    {
        // Arrange
        HttpClient client = fixture.Factory.CreateClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/alertas");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/v1/alertas/contadores ───────────────────────────────────────

    [Fact]
    public async Task GET_alertas_contadores_retorna_200_com_envelope_e_campos_contagem()
    {
        // Arrange
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/alertas/contadores?perfil=Tesouraria");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        body.TryGetProperty("data", out JsonElement data).Should().BeTrue(
            because: "GET contadores deve retornar envelope com propriedade 'data'");
        body.TryGetProperty("meta", out _).Should().BeTrue(
            because: "GET contadores deve retornar envelope com propriedade 'meta'");

        data.TryGetProperty("critico", out _).Should().BeTrue(
            because: "contadores deve ter campo 'critico'");
        data.TryGetProperty("atencao", out _).Should().BeTrue(
            because: "contadores deve ter campo 'atencao'");
        data.TryGetProperty("informativo", out _).Should().BeTrue(
            because: "contadores deve ter campo 'informativo'");
    }

    [Fact]
    public async Task GET_alertas_contadores_reflete_alerta_critico_aberto()
    {
        // Arrange
        Guid tenantId = new("00000000-0000-7000-8000-000000000001");
        string chave  = $"contador-critico-{Guid.NewGuid()}";

        await SeedAlertaAsync(
            tenantId,
            chave,
            status:     StatusAlerta.Aberto,
            severidade: SeveridadeAlerta.Critico,
            perfil:     PerfilCockpit.Tesouraria);

        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.GetAsync("/api/v1/alertas/contadores?perfil=Tesouraria");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        int critico = body.GetProperty("data").GetProperty("critico").GetInt32();
        critico.Should().BeGreaterThanOrEqualTo(1,
            because: "o alerta crítico aberto semeado deve incrementar o contador");
    }

    // ── POST /api/v1/alertas/{id}/dispensar ──────────────────────────────────

    [Fact]
    public async Task POST_dispensar_em_alerta_existente_retorna_204()
    {
        // Arrange
        Guid tenantId = new("00000000-0000-7000-8000-000000000001");
        Guid alertaId = await SeedAlertaAsync(tenantId, $"dispensar-existente-{Guid.NewGuid()}");
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/alertas/{alertaId}/dispensar", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task POST_dispensar_em_id_inexistente_retorna_404()
    {
        // Arrange
        Guid idInexistente = Guid.NewGuid();
        HttpClient client  = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/alertas/{idInexistente}/dispensar", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_dispensar_idempotente_segunda_chamada_retorna_204()
    {
        // Arrange
        Guid tenantId = new("00000000-0000-7000-8000-000000000001");
        Guid alertaId = await SeedAlertaAsync(tenantId, $"dispensar-idem-{Guid.NewGuid()}");
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Primeira chamada
        HttpResponseMessage res1 = await client.PostAsync(
            $"/api/v1/alertas/{alertaId}/dispensar", content: null);
        res1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — segunda chamada (idempotente)
        HttpResponseMessage res2 = await client.PostAsync(
            $"/api/v1/alertas/{alertaId}/dispensar", content: null);

        // Assert
        res2.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "Dispensar é idempotente quando alerta já está dispensado");
    }

    // ── POST /api/v1/alertas/{id}/marcar-como-lido ───────────────────────────

    [Fact]
    public async Task POST_marcar_como_lido_em_alerta_aberto_retorna_204()
    {
        // Arrange
        Guid tenantId = new("00000000-0000-7000-8000-000000000001");
        Guid alertaId = await SeedAlertaAsync(tenantId, $"lido-aberto-{Guid.NewGuid()}");
        HttpClient client = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/alertas/{alertaId}/marcar-como-lido", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task POST_marcar_como_lido_em_id_inexistente_retorna_404()
    {
        // Arrange
        Guid idInexistente = Guid.NewGuid();
        HttpClient client  = fixture.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage res = await client.PostAsync(
            $"/api/v1/alertas/{idInexistente}/marcar-como-lido", content: null);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

}
