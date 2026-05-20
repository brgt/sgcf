using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Xunit;

namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation;

/// <summary>
/// Confirma que snapshots de CDI são um catálogo GLOBAL: qualquer tenant pode
/// consultar os mesmos dados. CDI snapshots NÃO são tenant-scoped.
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "CrossTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class CdiSnapshotsCrossTenantTests(MultiTenantFixture fixture)
    : MultiTenantTestBase(fixture)
{
    // ── Teste 1: snapshot criado é visível para ambos os tenants ─────────────

    [Fact]
    public async Task Snapshot_criado_e_visivel_para_ambos_os_tenants()
    {
        // Arrange — cria snapshot via admin (admin pode criar CDI snapshots)
        // Usa data futura distante para evitar conflito com outros testes
        string dataSnapshot = "2099-01-15";

        HttpResponseMessage createRes = await AdminClient.PostAsJsonAsync(
            "/api/v1/cdi-snapshots",
            new { data = dataSnapshot, cdiAaPercentual = 13.75m });

        // 201 = criado, 409 = já existe (idempotência entre runs)
        createRes.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
            because: "a criação do snapshot deve ser idempotente");

        // Act — ambos os tenants buscam pelo snapshot
        HttpResponseMessage resA = await ClientA.GetAsync(
            $"/api/v1/cdi-snapshots?desde={dataSnapshot}&ate={dataSnapshot}");
        HttpResponseMessage resB = await ClientB.GetAsync(
            $"/api/v1/cdi-snapshots?desde={dataSnapshot}&ate={dataSnapshot}");

        // Assert — ambos OK (catálogo global)
        resA.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantA deve ver snapshots de CDI do catálogo global");
        resB.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantB deve ver snapshots de CDI do catálogo global");
    }

    // ── Teste 2: TenantA e TenantB obtêm os mesmos snapshots ─────────────────

    [Fact]
    public async Task TenantA_e_TenantB_obtem_mesmos_snapshots_de_cdi()
    {
        // Act — ambos listam snapshots de um período
        HttpResponseMessage resA = await ClientA.GetAsync(
            "/api/v1/cdi-snapshots?desde=2026-01-01&ate=2026-12-31");
        HttpResponseMessage resB = await ClientB.GetAsync(
            "/api/v1/cdi-snapshots?desde=2026-01-01&ate=2026-12-31");

        resA.EnsureSuccessStatusCode();
        resB.EnsureSuccessStatusCode();

        string bodyStrA = await resA.Content.ReadAsStringAsync();
        string bodyStrB = await resB.Content.ReadAsStringAsync();

        JsonElement bodyA = JsonSerializer.Deserialize<JsonElement>(bodyStrA, JsonOpts);
        JsonElement bodyB = JsonSerializer.Deserialize<JsonElement>(bodyStrB, JsonOpts);

        int countA = bodyA.GetArrayLength();
        int countB = bodyB.GetArrayLength();

        countA.Should().Be(countB,
            because: "CDI snapshots são catálogo global — ambos os tenants devem ver a mesma quantidade");
    }
}
