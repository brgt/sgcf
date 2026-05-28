using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.LimitesBanco;

/// <summary>
/// Testes de integração para RV-01: PATCH expõe novaDataVigenciaFim, novaDataVigenciaInicio
/// e motivoEncerramento. Cobre o contrato dual de resposta do endpoint PATCH.
/// </summary>
[Collection("LimitesBancoApi")]
[Trait("Category", "Slow")]
public sealed class LimitesBancoVigenciaTests(LimitesBancoApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client, string codigoCompe)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe,
            razaoSocial = $"Banco V{codigoCompe} S.A.",
            apelido = $"BV{codigoCompe}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> CriarLimiteAsync(
        HttpClient client,
        Guid bancoId,
        string inicio = "2099-01-01",
        string? fim = null,
        string modalidade = "Finimp",
        decimal valor = 10_000_000m)
    {
        object payload = fim is null
            ? new { bancoId, modalidade, valorLimiteBrl = valor, dataVigenciaInicio = inicio }
            : new { bancoId, modalidade, valorLimiteBrl = valor, dataVigenciaInicio = inicio, dataVigenciaFim = fim };

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> PatchLimiteAsync(
        HttpClient client,
        Guid limiteId,
        object patch)
    {
        HttpResponseMessage res = await client.PatchAsJsonAsync($"/api/v1/limites-banco/{limiteId}", patch);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    // ─── V01: PATCH sem novaDataVigenciaFim → contrato anterior (LimiteBancoDto) ─

    [Fact]
    public async Task Patch_SemNovaDataVigenciaFim_Retorna200ComLimiteBancoDto()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "V01");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid limiteId = criado.GetProperty("id").GetGuid();

        (HttpResponseMessage res, string raw, JsonElement body) = await PatchLimiteAsync(client, limiteId,
            new { novoValorLimiteBrl = 12_000_000m });

        res.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        // backward-compat: resposta é LimiteBancoDto direto (sem envelope avisos)
        body.TryGetProperty("avisos", out _).Should().BeFalse(
            "sem novaDataVigenciaFim, resposta deve ser LimiteBancoDto sem campo 'avisos'");
        body.GetProperty("valorLimiteBrl").GetDecimal().Should().Be(12_000_000m);
    }

    // ─── V02: PATCH com novaDataVigenciaFim → envelope AtualizarLimiteBancoResponse ─

    [Fact]
    public async Task Patch_ComNovaDataVigenciaFim_SemUso_Retorna200ComEnvelopeEAvisosVazios()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "V02");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid limiteId = criado.GetProperty("id").GetGuid();

        (HttpResponseMessage res, string raw, JsonElement body) = await PatchLimiteAsync(client, limiteId,
            new { novaDataVigenciaFim = "2099-12-31" });

        res.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        body.TryGetProperty("avisos", out JsonElement avisos).Should().BeTrue(
            "com novaDataVigenciaFim, resposta deve ser AtualizarLimiteBancoResponse com campo 'avisos'");
        avisos.GetArrayLength().Should().Be(0, "sem utilização ativa, lista de avisos deve estar vazia");
        body.GetProperty("limite").GetProperty("dataVigenciaFim").GetString()
            .Should().Be("2099-12-31");
    }

    // ─── V03: PATCH com motivoEncerramento → persistido no limite ─────────────

    [Fact]
    public async Task Patch_ComMotivoEncerramento_EhPersistidoERetornadoNoDto()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "V03");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid limiteId = criado.GetProperty("id").GetGuid();

        const string motivo = "Banco retirou linha de crédito — reavaliação mai/2026";
        (HttpResponseMessage res, string raw, JsonElement body) = await PatchLimiteAsync(client, limiteId,
            new { novaDataVigenciaFim = "2099-06-30", motivoEncerramento = motivo });

        res.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        body.GetProperty("limite").GetProperty("motivoEncerramento").GetString()
            .Should().Be(motivo);
    }

    // ─── V04: PATCH com novaDataVigenciaFim causando sobreposição → 409 ────────

    [Fact]
    public async Task Patch_NovaDataVigenciaFim_ComSobreposicao_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "V04");

        // Limite A: 2099-01-01 (em aberto)
        (_, _, JsonElement limiteA) = await CriarLimiteAsync(client, bancoId, "2099-01-01");
        Guid limiteAId = limiteA.GetProperty("id").GetGuid();

        // Fechar limite A até 2099-06-30 para criar espaço
        await PatchLimiteAsync(client, limiteAId, new { novaDataVigenciaFim = "2099-06-30" });

        // Limite B: 2099-07-01 (em aberto)
        (_, _, JsonElement limiteB) = await CriarLimiteAsync(client, bancoId, "2099-07-01");
        Guid limiteBId = limiteB.GetProperty("id").GetGuid();
        _ = limiteBId;

        // Tentar expandir A para 2099-12-31 — sobrepõe B
        (HttpResponseMessage res, string raw, _) = await PatchLimiteAsync(client, limiteAId,
            new { novaDataVigenciaFim = "2099-12-31" });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict, raw);
    }

    // ─── V05: PATCH limite inexistente → 404 ─────────────────────────────────

    [Fact]
    public async Task Patch_LimiteInexistente_Retorna404()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        (HttpResponseMessage res, _, _) = await PatchLimiteAsync(client, Guid.NewGuid(),
            new { novaDataVigenciaFim = "2099-12-31" });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── V06: PATCH com novaDataVigenciaInicio → atualiza início ─────────────

    [Fact]
    public async Task Patch_ComNovaDataVigenciaInicio_AtualizaInicio()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client, "V06");
        (_, _, JsonElement criado) = await CriarLimiteAsync(client, bancoId, "2099-03-01", "2099-12-31");
        Guid limiteId = criado.GetProperty("id").GetGuid();

        (HttpResponseMessage res, string raw, JsonElement body) = await PatchLimiteAsync(client, limiteId,
            new { novaDataVigenciaInicio = "2099-01-01" });

        res.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        // Sem novaDataVigenciaFim, o controller retorna response.Limite (flat DTO),
        // mesmo quando novaDataVigenciaInicio está presente.
        body.TryGetProperty("avisos", out _).Should().BeFalse(
            "sem novaDataVigenciaFim, resposta deve ser LimiteBancoDto sem campo 'avisos'");
        body.GetProperty("dataVigenciaInicio").GetString().Should().Be("2099-01-01");
    }
}
