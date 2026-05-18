using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Testes de integração para a regra de não sobreposição de vigência (GAP-001).
/// Cada teste cria seu próprio banco para garantir isolamento entre os cenários.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class LimitesBancoSobreposicaoTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um banco exclusivo para o cenário e retorna seu id.
    /// codigoCompe deve ter exatamente 3 caracteres (validação do domínio).
    /// </summary>
    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco Teste {codigoCompe} S.A.",
            apelido = $"BT{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {res.StatusCode} — {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> CriarLimiteAsync(
        HttpClient client,
        Guid bancoId,
        string inicio,
        string? fim = null,
        string modalidade = "Finimp")
    {
        object payload = fim is null
            ? new { bancoId, modalidade, valorLimiteBrl = 10_000_000m, dataVigenciaInicio = inicio }
            : new { bancoId, modalidade, valorLimiteBrl = 10_000_000m, dataVigenciaInicio = inicio, dataVigenciaFim = fim };

        return await client.PostAsJsonAsync("/api/v1/limites-banco", payload);
    }

    // ─── Cenário 1: Período idêntico retorna 409 ──────────────────────────────

    /// <summary>
    /// Criar dois limites com período exatamente igual → segundo deve retornar 409.
    /// Este é o caso do reprodutor reportado pelo time de front-end.
    /// </summary>
    [Fact]
    public async Task CriarLimite_PeriodoIdentico_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "T01");

        HttpResponseMessage primeiro = await CriarLimiteAsync(client, bancoId, "2099-04-01", "2099-09-30");
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created, "primeiro limite deve ser aceito");

        HttpResponseMessage segundo = await CriarLimiteAsync(client, bancoId, "2099-04-01", "2099-09-30");
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict, "período idêntico deve ser rejeitado com 409");

        string errorBody = await segundo.Content.ReadAsStringAsync();
        errorBody.ToLowerInvariant().Should().Contain("sobrepõe",
            "mensagem de erro deve citar sobreposição");
    }

    // ─── Cenário 2: Sobreposição parcial retorna 409 ──────────────────────────

    /// <summary>
    /// Limite A: 2099-01-01 a 2099-06-30.
    /// Limite B: 2099-05-01 a 2099-12-31 — inicia antes do fim de A → sobreposição parcial.
    /// </summary>
    [Fact]
    public async Task CriarLimite_SobreposicaoParcial_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "T02");

        HttpResponseMessage a = await CriarLimiteAsync(client, bancoId, "2099-01-01", "2099-06-30");
        a.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage b = await CriarLimiteAsync(client, bancoId, "2099-05-01", "2099-12-31");
        b.StatusCode.Should().Be(HttpStatusCode.Conflict, "sobreposição parcial deve retornar 409");
    }

    // ─── Cenário 3: Período contíguo retorna 201 ──────────────────────────────

    /// <summary>
    /// Limite A: 2099-01-01 a 2099-06-30.
    /// Limite B: 2099-07-01 em diante — começa no dia seguinte ao fim de A → não sobrepõe.
    /// </summary>
    [Fact]
    public async Task CriarLimite_PeriodoContiguo_Retorna201()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "T03");

        HttpResponseMessage a = await CriarLimiteAsync(client, bancoId, "2099-01-01", "2099-06-30");
        a.StatusCode.Should().Be(HttpStatusCode.Created);

        // B começa em 2099-07-01, imediatamente após o fim de A (2099-06-30). Sem sobreposição.
        HttpResponseMessage b = await CriarLimiteAsync(client, bancoId, "2099-07-01", "2099-12-31");
        b.StatusCode.Should().Be(HttpStatusCode.Created,
            "período imediatamente após o fim do existente não é sobreposição");
    }

    // ─── Cenário 4: Limite aberto (fim = null) retorna 409 ───────────────────

    /// <summary>
    /// Limite A: 2099-01-01 sem data de encerramento (aberto).
    /// Limite B: qualquer data futura → sempre sobrepõe A.
    /// </summary>
    [Fact]
    public async Task CriarLimite_QuandoExisteAberto_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "T04");

        // A é aberto (sem dataVigenciaFim)
        HttpResponseMessage a = await CriarLimiteAsync(client, bancoId, "2099-01-01", fim: null);
        a.StatusCode.Should().Be(HttpStatusCode.Created);

        // B é um período futuro que sempre sobreporá A
        HttpResponseMessage b = await CriarLimiteAsync(client, bancoId, "2100-01-01", "2100-12-31");
        b.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "limite aberto (sem fim) sobrepõe qualquer período futuro para o mesmo par");
    }

    // ─── Cenário 5: Outra modalidade não conflita ─────────────────────────────

    /// <summary>
    /// Limite para Finimp: 2099-04-01 a 2099-09-30.
    /// Criar período idêntico para Acc (modalidade diferente) → 201, pois o par é diferente.
    /// </summary>
    [Fact]
    public async Task CriarLimite_ModalidadeDiferente_MesmoPeriodo_Retorna201()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "T05");

        HttpResponseMessage finimp = await CriarLimiteAsync(
            client, bancoId, "2099-04-01", "2099-09-30", modalidade: "Finimp");
        finimp.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage refinimp = await CriarLimiteAsync(
            client, bancoId, "2099-04-01", "2099-09-30", modalidade: "Refinimp");
        refinimp.StatusCode.Should().Be(HttpStatusCode.Created,
            "sobreposição não se aplica entre modalidades distintas para o mesmo banco");
    }
}
