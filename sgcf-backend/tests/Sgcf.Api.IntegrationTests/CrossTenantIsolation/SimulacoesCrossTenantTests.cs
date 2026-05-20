using System.Net;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que cenários de simulação ficam isolados por tenant.
/// TenantA não pode ver nem acessar cenários de TenantB e vice-versa.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class SimulacoesCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: listagem isolada ─────────────────────────────────────────────

    [Fact]
    public async Task Lista_TenantA_nao_retorna_cenarios_do_TenantB()
    {
        // Arrange
        string nomeA = $"Cenário A {Guid.NewGuid():N}"[..30];
        string nomeB = $"Cenário B {Guid.NewGuid():N}"[..30];

        await CriarCenarioAsync(ClientA, nomeA);
        await CriarCenarioAsync(ClientB, nomeB);

        // Act
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/simulacoes/cenarios");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        IEnumerable<string> nomesRetornados = body
            .EnumerateArray()
            .Select(c => c.GetProperty("nome").GetString()!);

        nomesRetornados.Should().Contain(nomeA,
            because: "TenantA deve ver seu próprio cenário");
        nomesRetornados.Should().NotContain(nomeB,
            because: "TenantA não deve ver cenários de TenantB");
    }

    // ── Teste 2: get por ID de outro tenant retorna 404 ──────────────────────

    [Fact]
    public async Task Get_cenario_de_outro_tenant_retorna_404()
    {
        // Arrange — cria cenário em TenantB
        string nomeB = $"Cenário B2 {Guid.NewGuid():N}"[..30];
        Guid idTenantB = await CriarCenarioAsync(ClientB, nomeB);

        // Act — TenantA tenta acessar cenário de TenantB
        HttpResponseMessage res = await ClientA.GetAsync($"/api/v1/simulacoes/cenarios/{idTenantB}");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "o global filter EF deve bloquear acesso cross-tenant ao cenário");
    }

    // ── Teste 3: cenário de B não aparece na lista de A ───────────────────────

    [Fact]
    public async Task Cenario_criado_em_TenantB_nao_aparece_na_lista_de_TenantA()
    {
        // Arrange
        string nomeB = $"Cenário B3 {Guid.NewGuid():N}"[..30];
        Guid idB = await CriarCenarioAsync(ClientB, nomeB);

        // Act — TenantA lista seus cenários
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/simulacoes/cenarios");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync(), JsonOpts);

        IEnumerable<string> idsRetornados = body
            .EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid().ToString());

        idsRetornados.Should().NotContain(idB.ToString(),
            because: "TenantA não deve ver cenários de TenantB");
    }
}
