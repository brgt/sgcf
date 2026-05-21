using System.Net;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

using Sgcf.Domain.Alertas;
using Sgcf.Infrastructure.Persistence;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Alertas;

/// <summary>
/// Verifica isolamento cross-tenant para alertas.
/// Usa <see cref="MultiTenantFixture"/> que registra <see cref="CrossTenantTestAuthHandler"/>
/// e provisiona TenantA (Proxys) + TenantB (ACME).
/// </summary>
[Collection("MultiTenantIsolation")]
[Trait("Category", "Slow")]
public sealed class AlertasCrossTenantTests : MultiTenantTestBase
{
    private readonly MultiTenantFixture _fixture;

    public AlertasCrossTenantTests(MultiTenantFixture fixture) : base(fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Semeia um alerta diretamente no banco para o tenant informado,
    /// bypassando o TenantSaveInterceptor via EF property setter.
    /// </summary>
    private async Task<Guid> SeedAlertaAsync(
        Guid tenantId,
        string chaveIdempotencia,
        PerfilCockpit perfil = PerfilCockpit.Tesouraria)
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        SgcfDbContext ctx   = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        IClock clock        = scope.ServiceProvider.GetRequiredService<IClock>();

        Alerta alerta = Alerta.Criar(
            categoria:         CategoriaAlerta.Vencimento,
            severidade:        SeveridadeAlerta.Critico,
            titulo:            $"Alerta cross-tenant {chaveIdempotencia}",
            descricao:         "Teste de isolamento cross-tenant.",
            origemTipo:        "Contrato",
            origemId:          null,
            perfisVisiveis:    [perfil],
            chaveIdempotencia: chaveIdempotencia,
            clock:             clock);

        await ctx.Alertas.AddAsync(alerta);
        // Seta TenantId via EF property — mesmo mecanismo interno do TenantSaveInterceptor.
        ctx.Entry(alerta).Property("TenantId").CurrentValue = tenantId;
        await ctx.SaveChangesAsync();

        return alerta.Id;
    }

    [Fact]
    public async Task Alerta_de_TenantA_nao_aparece_na_lista_de_TenantB()
    {
        // Arrange
        string chave = $"cross-tenant-alerta-{Guid.NewGuid()}";
        await SeedAlertaAsync(TenantTestData.TenantAId, chave);

        // Act
        HttpResponseMessage resA = await ClientA.GetAsync("/api/v1/alertas");
        HttpResponseMessage resB = await ClientB.GetAsync("/api/v1/alertas");

        // Assert — ambos devem responder com sucesso (ambos os tenants estão provisionados)
        resA.StatusCode.Should().Be(HttpStatusCode.OK);
        resB.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement bodyA = JsonSerializer.Deserialize<JsonElement>(
            await resA.Content.ReadAsStringAsync(), JsonOpts);
        JsonElement bodyB = JsonSerializer.Deserialize<JsonElement>(
            await resB.Content.ReadAsStringAsync(), JsonOpts);

        IReadOnlyList<Guid> idsA = bodyA
            .GetProperty("data")
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        IReadOnlyList<Guid> idsB = bodyB
            .GetProperty("data")
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        // TenantA deve ver o alerta semeado; TenantB não deve ver alertas de TenantA.
        idsA.Should().NotBeEmpty(
            because: "TenantA deve ver o alerta semeado com seu TenantId");

        idsB.Should().NotIntersectWith(idsA,
            because: "alertas são tenant-scoped: o EF global filter deve impedir que TenantB veja alertas de TenantA");
    }
}
