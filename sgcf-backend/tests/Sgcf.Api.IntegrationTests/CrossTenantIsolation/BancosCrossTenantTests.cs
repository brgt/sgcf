using System.Net;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Confirma que Bancos é um catálogo GLOBAL: ambos os tenants devem ver
/// os mesmos bancos. Bancos NÃO são tenant-scoped.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class BancosCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: TenantA e TenantB veem os mesmos bancos ────────────────────

    [Fact]
    public async Task TenantA_e_TenantB_veem_mesmos_bancos_no_catalogo_global()
    {
        // Arrange — garante que pelo menos um banco existe
        Guid bancoId = await GarantirBancoAsync("905", "Banco Global");

        // Act — ambos os tenants listam bancos
        HttpResponseMessage resA = await ClientA.GetAsync("/api/v1/bancos");
        HttpResponseMessage resB = await ClientB.GetAsync("/api/v1/bancos");

        // Assert — ambos OK
        resA.StatusCode.Should().Be(HttpStatusCode.OK);
        resB.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement bodyA = JsonSerializer.Deserialize<JsonElement>(
            await resA.Content.ReadAsStringAsync(), JsonOpts);
        JsonElement bodyB = JsonSerializer.Deserialize<JsonElement>(
            await resB.Content.ReadAsStringAsync(), JsonOpts);

        IReadOnlyList<Guid> idsA = bodyA.EnumerateArray()
            .Select(b => b.GetProperty("id").GetGuid())
            .ToList();

        IReadOnlyList<Guid> idsB = bodyB.EnumerateArray()
            .Select(b => b.GetProperty("id").GetGuid())
            .ToList();

        // Catálogo global: ambos devem retornar conjuntos idênticos.
        idsA.Should().BeEquivalentTo(idsB,
            because: "Bancos é catálogo global — todos os tenants veem os mesmos bancos");

        // E o banco que acabamos de criar deve estar em ambas as listas.
        idsA.Should().Contain(bancoId,
            because: "o banco recém-criado deve aparecer para TenantA");
        idsB.Should().Contain(bancoId,
            because: "o banco recém-criado deve aparecer para TenantB");
    }

    // ── Teste 2: get por ID funciona para ambos os tenants ──────────────────

    [Fact]
    public async Task Get_banco_por_id_retorna_mesmo_registro_para_ambos_os_tenants()
    {
        // Arrange
        Guid bancoId = await GarantirBancoAsync("906", "Banco Global 2");

        // Act — cada tenant busca o mesmo banco
        HttpResponseMessage resA = await ClientA.GetAsync($"/api/v1/bancos/{bancoId}");
        HttpResponseMessage resB = await ClientB.GetAsync($"/api/v1/bancos/{bancoId}");

        // Assert
        resA.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantA deve conseguir ler qualquer banco do catálogo global");
        resB.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantB deve conseguir ler qualquer banco do catálogo global");

        JsonElement bodyA = JsonSerializer.Deserialize<JsonElement>(
            await resA.Content.ReadAsStringAsync(), JsonOpts);
        JsonElement bodyB = JsonSerializer.Deserialize<JsonElement>(
            await resB.Content.ReadAsStringAsync(), JsonOpts);

        bodyA.GetProperty("id").GetGuid().Should().Be(bancoId);
        bodyB.GetProperty("id").GetGuid().Should().Be(bancoId);
    }
}
