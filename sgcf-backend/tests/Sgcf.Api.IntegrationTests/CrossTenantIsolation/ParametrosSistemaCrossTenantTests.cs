using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Verifica que cada tenant lê seus próprios parâmetros de sistema
/// e não pode acessar os de outro tenant.
///
/// ParametrosSistema são criados no provisionamento — cada tenant tem exatamente um registro.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class ParametrosSistemaCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: TenantA lê seus próprios parâmetros ─────────────────────────

    [Fact]
    public async Task TenantA_le_seus_proprios_parametros_sistema()
    {
        // Act
        HttpResponseMessage res = await ClientA.GetAsync("/api/v1/parametros-sistema");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantA deve ter ParametrosSistema criados no provisionamento");

        string body = await res.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    // ── Teste 2: TenantB lê seus próprios parâmetros ─────────────────────────

    [Fact]
    public async Task TenantB_le_seus_proprios_parametros_sistema()
    {
        // Act
        HttpResponseMessage res = await ClientB.GetAsync("/api/v1/parametros-sistema");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantB deve ter ParametrosSistema criados no provisionamento");
    }

    // ── Teste 3: atualizar parâmetro de TenantA não afeta TenantB ────────────

    [Fact]
    public async Task Atualizar_tetao_de_TenantA_nao_afeta_TenantB()
    {
        // Act — TenantA define um tetão específico
        HttpResponseMessage patchA = await ClientA.PatchAsJsonAsync(
            "/api/v1/parametros-sistema/tetao-mensal",
            new { valor = 99_999_999m });

        patchA.EnsureSuccessStatusCode();

        // TenantB lê seus próprios parâmetros
        HttpResponseMessage resB = await ClientB.GetAsync("/api/v1/parametros-sistema");
        resB.EnsureSuccessStatusCode();

        System.Text.Json.JsonElement bodyB = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            await resB.Content.ReadAsStringAsync(), JsonOpts);

        // TenantB NÃO deve ter o valor de R$ 99.999.999 — seus parâmetros são independentes
        decimal? tetaoB = bodyB.GetProperty("tetaoMensalCapacidadeBrl").ValueKind
            == System.Text.Json.JsonValueKind.Null
            ? null
            : bodyB.GetProperty("tetaoMensalCapacidadeBrl").GetDecimal();

        tetaoB.Should().NotBe(99_999_999m,
            because: "a atualização do tetão de TenantA não deve vazar para TenantB");
    }
}
