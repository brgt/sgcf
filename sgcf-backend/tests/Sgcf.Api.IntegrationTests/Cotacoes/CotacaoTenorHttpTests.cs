using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Critérios de aceite do tenor de prazo via HTTP — SPEC S40 §11.
/// Usa modalidade Nce (BRL pura) para exercer o tenor sem dependência de PTAX.
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class CotacaoTenorHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static async Task<JsonElement> PostCotacaoAsync(HttpClient client, object body)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", body);
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
    }

    [Fact]
    public async Task Post_tenor_em_meses_persiste_dias_canonico_e_intencao()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement body = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 60,
            prazoMaximoUnidade = "Meses",
            dataAbertura = "2026-05-16",
        });

        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(1800);
        body.GetProperty("prazoMaximoValor").GetInt32().Should().Be(60);
        body.GetProperty("prazoMaximoUnidade").GetString().Should().Be("Meses");
        body.GetProperty("alertas").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Post_tenor_em_dias_persiste_valor_igual_aos_dias()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement body = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 180,
            prazoMaximoUnidade = "Dias",
            dataAbertura = "2026-05-16",
        });

        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(180);
        body.GetProperty("prazoMaximoUnidade").GetString().Should().Be("Dias");
    }

    [Fact]
    public async Task Post_sem_unidade_usa_default_da_modalidade_meses()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement body = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 12,
            dataAbertura = "2026-05-16",
        });

        body.GetProperty("prazoMaximoUnidade").GetString().Should().Be("Meses");
        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(360);
    }

    [Fact]
    public async Task Post_legado_so_dias_retorna_unidade_dias_e_valor_igual()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement body = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16",
        });

        body.GetProperty("prazoMaximoUnidade").GetString().Should().Be("Dias");
        body.GetProperty("prazoMaximoValor").GetInt32().Should().Be(180);
        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(180);
    }

    [Theory]
    [InlineData(0, "Meses")]
    [InlineData(-5, "Dias")]
    public async Task Post_valor_invalido_retorna_400(int valor, string unidade)
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = valor,
            prazoMaximoUnidade = unidade,
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_unidade_invalida_retorna_400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 12,
            prazoMaximoUnidade = "Semanas",
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_sem_prazo_retorna_400()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            dataAbertura = "2026-05-16",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_inconsistente_recalcula_e_emite_alerta()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement body = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 24,
            prazoMaximoUnidade = "Meses",
            prazoMaximoDias = 999,
            dataAbertura = "2026-05-16",
        });

        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(720);
        JsonElement alertas = body.GetProperty("alertas");
        alertas.GetArrayLength().Should().BeGreaterThan(0);
        alertas[0].GetProperty("codigo").GetString().Should().Be("prazo-recalculado");
    }

    [Fact]
    public async Task Patch_tenor_meses_atualiza_dias_canonico()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement criada = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 12,
            prazoMaximoUnidade = "Meses",
            dataAbertura = "2026-05-16",
        });
        Guid id = criada.GetProperty("id").GetGuid();

        HttpResponseMessage patchRes = await client.PatchAsJsonAsync($"/api/v1/cotacoes/{id}", new
        {
            prazoMaximoValor = 24,
            prazoMaximoUnidade = "Meses",
        });
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK, await patchRes.Content.ReadAsStringAsync());

        JsonElement body = await patchRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(720);
        body.GetProperty("prazoMaximoValor").GetInt32().Should().Be(24);
    }

    [Fact]
    public async Task Patch_sem_prazo_nao_altera_o_prazo()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        JsonElement criada = await PostCotacaoAsync(client, new
        {
            modalidade = "Nce",
            valorAlvoBrl = 5_000_000m,
            prazoMaximoValor = 60,
            prazoMaximoUnidade = "Meses",
            dataAbertura = "2026-05-16",
        });
        Guid id = criada.GetProperty("id").GetGuid();

        HttpResponseMessage patchRes = await client.PatchAsJsonAsync($"/api/v1/cotacoes/{id}", new
        {
            observacoes = "Somente nota",
        });
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement body = await patchRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("prazoMaximoDias").GetInt32().Should().Be(1800);
        body.GetProperty("observacoes").GetString().Should().Be("Somente nota");
    }
}
