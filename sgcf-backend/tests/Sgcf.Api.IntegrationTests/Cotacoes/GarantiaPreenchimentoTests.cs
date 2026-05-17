using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Cotacoes;

/// <summary>
/// Testes E2E para o pré-preenchimento automático de garantia ao adicionar banco na cotação.
/// Task 4.1 (pré-preenchimento) e Task 4.2 (alertas de coerência).
/// SPEC §3.3 (regra CDB cativo → rendimento obrigatório).
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class GarantiaPreenchimentoTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client)
    {
        // Gera codigoCompe único de 3 dígitos para evitar conflito entre testes paralelos
        string codigo = Random.Shared.Next(100, 999).ToString(CultureInfo.InvariantCulture);
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco Garantia {codigo} S.A.",
            apelido = $"BG{codigo}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarLimiteComGarantiasAsync(
        HttpClient client,
        Guid bancoId,
        object[] garantiasExigidas,
        decimal valorLimiteBrl = 50_000_000m)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl,
            dataVigenciaInicio = "2026-01-01",
            garantiasExigidas
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed limite falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Garante CDI + PTAX no banco de dados para que CriarCotacao funcione.
    /// Ignora 409 (já existe de outro teste na mesma fixture).
    /// </summary>
    private static async Task SeedParametrosMercadoAsync(HttpClient client, Guid bancoId)
    {
        await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-16",
            cdiAaPercentual = 10.50m
        });

        await client.PostAsJsonAsync("/api/v1/parametros-cotacao", new
        {
            bancoId,
            modalidade = "Finimp",
            moeda = "Usd",
            tipoCotacao = "PtaxD1",
            valorCompra = 5.15m,
            valorVenda = 5.20m,
            dataReferencia = "2026-05-15"
        });
    }

    private static async Task<Guid> CriarCotacaoAsync(HttpClient client, decimal valorAlvoBrl = 1_000_000m)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl,
            prazoMaximoDias = 180,
            dataAbertura = "2026-05-16"
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar cotação falhou: {await res.Content.ReadAsStringAsync()}");
        JsonElement body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpResponseMessage Res, string Raw, JsonElement Body)> AdicionarBancoAsync(
        HttpClient client,
        Guid cotacaoId,
        object payload)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/bancos", payload);
        string raw = await res.Content.ReadAsStringAsync();
        JsonElement body = string.IsNullOrWhiteSpace(raw)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);
        return (res, raw, body);
    }

    // ─── G01: CDB cativo 20% com rendimento → 200 + proposta preenchida ──────

    /// <summary>
    /// Cenário principal Task 4.1: limite com CDB cativo 20% + cotação de 1M BRL.
    /// Deve retornar 200 com proposta.garantiaEhCdbCativo=true,
    /// valorGarantiaExigidaBrl=200.000 (20% de 1M) e string formatada.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_LimiteComCdbCativo20Pct_RetornaGarantiaPreenchida()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        await CriarLimiteComGarantiasAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 20m, obrigatoria = true }
        ]);

        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 1_000_000m);

        (HttpResponseMessage res, _, JsonElement body) = await AdicionarBancoAsync(
            client,
            cotacaoId,
            new
            {
                bancoId,
                preencherGarantiaAutomaticamente = true,
                rendimentoCdbAaPercentual = 12.5m
            });

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            $"esperado 200 com body de garantia: {body}");

        body.TryGetProperty("proposta", out JsonElement proposta).Should().BeTrue("resposta deve ter campo 'proposta'");

        proposta.GetProperty("garantiaEhCdbCativo").GetBoolean().Should().BeTrue();
        proposta.GetProperty("valorGarantiaExigidaBrl").GetDecimal().Should().Be(200_000m);
        proposta.GetProperty("garantiaExigida").GetString().Should().Be("CDB cativo 20% (obrigatório)");

        body.TryGetProperty("alertas", out JsonElement alertas).Should().BeTrue();
        alertas.GetArrayLength().Should().Be(0, "sem valores manuais divergentes");
    }

    // ─── G02: CDB cativo SEM rendimento → 409 (SPEC §3.3) ───────────────────

    /// <summary>
    /// Regra SPEC §3.3: CDB cativo exige rendimento. Sem ele → 409.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_LimiteComCdbCativoSemRendimento_Retorna409()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        await CriarLimiteComGarantiasAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 20m, obrigatoria = true }
        ]);

        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 500_000m);

        (HttpResponseMessage res, string raw, _) = await AdicionarBancoAsync(
            client,
            cotacaoId,
            new
            {
                bancoId,
                preencherGarantiaAutomaticamente = true
                // rendimentoCdbAaPercentual ausente — deve falhar
            });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "SPEC §3.3: CDB cativo sem rendimento deve ser rejeitado");
        raw.ToLowerInvariant().Should().Contain("cdb cativo");
    }

    // ─── G03: Limite sem garantias → 204 sem body ─────────────────────────────

    /// <summary>
    /// Limite sem garantias exigidas: comportamento original preservado (204 NoContent).
    /// GarantiaEhCdbCativo e string ficam com defaults — preenchimento fica para o caller.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_LimiteSemGarantias_Retorna204SemBody()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        // Limite sem garantiasExigidas
        HttpResponseMessage limRes = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 50_000_000m,
            dataVigenciaInicio = "2026-01-01"
        });
        limRes.IsSuccessStatusCode.Should().BeTrue();

        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 500_000m);

        (HttpResponseMessage res, _, _) = await AdicionarBancoAsync(
            client,
            cotacaoId,
            new { bancoId });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "limite sem garantias → comportamento original 204");
    }

    // ─── G04: Limite com Aval → garantiaEhCdbCativo=false, valor=0 ───────────

    /// <summary>
    /// Aval não contribui com valor monetário. GarantiaEhCdbCativo = false.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_LimiteComApenasAval_RetornaValorZeroECdbFalso()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        await CriarLimiteComGarantiasAsync(client, bancoId,
        [
            new { tipo = "Aval", obrigatoria = true }
        ]);

        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 1_000_000m);

        (HttpResponseMessage res, _, JsonElement body) = await AdicionarBancoAsync(
            client,
            cotacaoId,
            new { bancoId, preencherGarantiaAutomaticamente = true });

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement proposta = body.GetProperty("proposta");
        proposta.GetProperty("garantiaEhCdbCativo").GetBoolean().Should().BeFalse();
        proposta.GetProperty("valorGarantiaExigidaBrl").GetDecimal().Should().Be(0m);
        proposta.GetProperty("garantiaExigida").GetString().Should().Be("Aval (obrigatório)");
    }

    // ─── G05: preencherGarantiaAutomaticamente=false → 204 (manual) ──────────

    /// <summary>
    /// Caller opt-out do pré-preenchimento: mesmo com garantias no limite, retorna 204.
    /// O caller informa os dados manualmente ao registrar a Proposta.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_OptOutDoPreenchimento_Retorna204()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        await CriarLimiteComGarantiasAsync(client, bancoId,
        [
            new { tipo = "Aval", obrigatoria = true }
        ]);

        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 500_000m);

        (HttpResponseMessage res, _, _) = await AdicionarBancoAsync(
            client,
            cotacaoId,
            new { bancoId, preencherGarantiaAutomaticamente = false });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "opt-out do pré-preenchimento preserva comportamento 204");
    }

    // ─── G06: Task 4.2 — alerta quando valor manual diverge ──────────────────

    /// <summary>
    /// Task 4.2: quando caller informa valor manual que diverge do calculado,
    /// a resposta inclui alerta informativo. Não bloqueia a operação.
    /// </summary>
    [Fact]
    public async Task AdicionarBanco_ValorManualDivergente_RetornaAlertaInformativo()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();
        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        await CriarLimiteComGarantiasAsync(client, bancoId,
        [
            new { tipo = "Aval", obrigatoria = true }
        ]);

        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 1_000_000m);

        // Passa garantiaEhCdbCativoManual=true, mas o calculado é false (Aval)
        (HttpResponseMessage res, _, JsonElement body) = await AdicionarBancoAsync(
            client,
            cotacaoId,
            new
            {
                bancoId,
                preencherGarantiaAutomaticamente = true,
                garantiaEhCdbCativoManual = true,  // diverge do calculado (false)
                garantiaExigidaManual = "Algo diferente", // diverge de "Aval (obrigatório)"
                valorGarantiaExigidaBrlManual = 500_000m   // diverge de 0
            });

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            "alertas não bloqueiam — operação deve ter sucesso");

        JsonElement alertas = body.GetProperty("alertas");
        alertas.GetArrayLength().Should().Be(3,
            "três campos manuais divergem do pré-preenchimento calculado");
    }

    // ─── G07: Regressão — fluxo completo com banco sem garantias ─────────────

    /// <summary>
    /// Regressão: fluxo existente sem garantias continua funcionando com 204 no AddBanco.
    /// Garante backward-compatibility com callers existentes que passam apenas { bancoId }.
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_SemGarantias_BackwardCompatibility()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        // Seed: banco, limite sem garantias, CDI, PTAX
        string codigoReg = Random.Shared.Next(100, 999).ToString(CultureInfo.InvariantCulture);
        HttpResponseMessage bancRes = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigoReg,
            razaoSocial = $"Banco Regressão {codigoReg} S.A.",
            apelido = $"BR{codigoReg}",
            padraoAntecipacao = "A"
        });
        bancRes.IsSuccessStatusCode.Should().BeTrue();
        Guid bancoId = (await bancRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        HttpResponseMessage limRes = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 50_000_000m,
            dataVigenciaInicio = "2026-01-01"
        });
        limRes.IsSuccessStatusCode.Should().BeTrue();

        await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-16",
            cdiAaPercentual = 10.50m
        });

        await client.PostAsJsonAsync("/api/v1/parametros-cotacao", new
        {
            bancoId,
            modalidade = "Finimp",
            moeda = "Usd",
            tipoCotacao = "PtaxD1",
            valorCompra = 5.15m,
            valorVenda = 5.20m,
            dataReferencia = "2026-05-15"
        });

        // Criar cotação + adicionar banco (payload mínimo, sem novos campos)
        Guid cotacaoId = await CriarCotacaoAsync(client, valorAlvoBrl: 1_000_000m);

        (HttpResponseMessage addRes, _, _) = await AdicionarBancoAsync(
            client, cotacaoId, new { bancoId });

        // Backward-compat: 204 quando sem garantias
        addRes.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "payload mínimo com apenas bancoId deve retornar 204 sem quebrar callers existentes");

        // Cotação deve estar em estado consistente para prosseguir o fluxo
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/cotacoes/{cotacaoId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement cotacao = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        cotacao.GetProperty("status").GetString().Should().Be("Rascunho");
    }
}
