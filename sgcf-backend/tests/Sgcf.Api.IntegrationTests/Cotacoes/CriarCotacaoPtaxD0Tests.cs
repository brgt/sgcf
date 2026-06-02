using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sgcf.Api.IntegrationTests.TestAuth;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Prove-It / regressão (AC-4): reproduz o cenário de produção real onde o banco
/// é alimentado <b>apenas</b> por <c>PtaxD0</c> (formato que o ingestor do BCB grava —
/// nunca <c>PtaxD1</c>). Antes da correção, as leituras pediam <c>PtaxD1</c> direto no
/// repositório e recebiam sempre <c>null</c>, quebrando a criação de cotação cambial.
///
/// Após a correção (resolver traduz <c>PtaxD1 → PtaxD0(D-1)</c>), criar uma cotação com
/// abertura R deve resolver o fechamento <c>PtaxD0</c> de <b>R-1</b> — não R-2, não null.
/// Este teste permanece como guarda permanente contra a regressão e contra off-by-one.
/// </summary>
public sealed class CriarCotacaoPtaxD0Fixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sgcf_ptaxd0")
        .WithUsername("sgcf")
        .WithPassword("sgcf_ptaxd0")
        .Build();

    /// <summary>"Agora" fixo: 2026-05-16 12:00 UTC. Abertura R = 2026-05-16; R-1 = 2026-05-15.</summary>
    public static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 16, 12, 0);

    /// <summary>Data de abertura usada nos testes (R).</summary>
    public static readonly DateOnly DataAbertura = new(2026, 5, 16);

    /// <summary>Fechamento PTAX esperado: D-1 da abertura (R-1 = 2026-05-15).</summary>
    public static readonly DateOnly FechamentoEsperado = new(2026, 5, 15);

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
                        npgsql =>
                        {
                            npgsql.UseNodaTime();
                            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        }));

                services.RemoveAll<IClock>();
                services.AddSingleton(clockFake);
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        SgcfDbContext ctx = scope.ServiceProvider.GetRequiredService<SgcfDbContext>();
        await ctx.Database.MigrateAsync();
        await TenantTestSeeder.SeedProxysAsync(Factory.Services);

        // ── PONTO-CHAVE ──────────────────────────────────────────────────────────
        // Semeamos APENAS PtaxD0, exatamente como o PtaxIngestor faz em produção.
        // Nenhuma linha PtaxD1 é gravada. O fechamento é o de R-1 (2026-05-15).
        // Momento 2026-05-15T20:00Z → em BRT (UTC-3) = 2026-05-15 17:00 → data BRT = 2026-05-15.
        ICotacaoFxRepository cotacaoFxRepo =
            scope.ServiceProvider.GetRequiredService<ICotacaoFxRepository>();
        CotacaoFx ptaxD0 = CotacaoFx.Criar(
            Moeda.Usd,
            TipoCotacao.PtaxD0,
            new Money(5.15m, Moeda.Brl),
            new Money(5.20m, Moeda.Brl),
            fonte: "BACEN-ptaxd0-only",
            momento: Instant.FromUtc(2026, 5, 15, 20, 0));
        await cotacaoFxRepo.UpsertAsync(ptaxD0);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer dev-test-token");
        return client;
    }
}

[CollectionDefinition("CriarCotacaoPtaxD0")]
#pragma warning disable CA1711
public sealed class CriarCotacaoPtaxD0Group : ICollectionFixture<CriarCotacaoPtaxD0Fixture> { }
#pragma warning restore CA1711

[Collection("CriarCotacaoPtaxD0")]
[Trait("Category", "Slow")]
public sealed class CriarCotacaoPtaxD0Tests(CriarCotacaoPtaxD0Fixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// AC-4 + AC-1: com o banco contendo SOMENTE PtaxD0 (como o ingestor real), criar uma
    /// cotação FINIMP com abertura R resolve o fechamento PtaxD0 de R-1 — não null, não R-2.
    /// </summary>
    [Fact]
    public async Task CriarCotacaoFinimp_ComBancoSomentePtaxD0_ResolveFechamentoDeRMenos1()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl = 1_500_000m,
            prazoMaximoDias = 180,
            dataAbertura = CriarCotacaoPtaxD0Fixture.DataAbertura.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });

        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"com PtaxD0 semeado em R-1 a criação deve funcionar; corpo: {await criarRes.Content.ReadAsStringAsync()}");

        JsonElement body = await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // A data de PTAX travada deve ser o fechamento de R-1 (2026-05-15), não null nem R-2.
        body.GetProperty("dataPtaxReferencia").GetString().Should().Be(
            CriarCotacaoPtaxD0Fixture.FechamentoEsperado.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "o resolver traduz PtaxD1→PtaxD0(D-1); off-by-one resultaria em R-2 (2026-05-14)");

        // O valor usado deve ser o ValorVenda do fechamento PtaxD0 (5,20).
        body.GetProperty("ptaxUsadaUsdBrl").GetDecimal().Should().Be(5.20m,
            "CriarCotacao usa ValorVenda do PtaxD0 resolvido");
    }
}
