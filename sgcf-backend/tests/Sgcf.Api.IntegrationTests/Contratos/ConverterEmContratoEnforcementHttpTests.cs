using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Sgcf.Api.IntegrationTests.Cotacoes;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Contratos;

/// <summary>
/// Testes E2E do enforcement SC-04 na conversão cotação→contrato.
/// Verifica o caminho HTTP completo: GlobalExceptionHandler mapeia
/// <c>GarantiaExigidaNaoCobertaException</c> para 409 com ProblemDetails estruturado.
/// Usa a fixture CotacoesApi (PostgreSQL real via Testcontainers).
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class ConverterEmContratoEnforcementHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers de seed
    // ──────────────────────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client)
    {
        string codigo = TestBancoCodigo.Next();
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco Enforcement {codigo} S.A.",
            apelido = $"BE{codigo}",
            padraoAntecipacao = "A"
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed banco falhou: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    private static async Task SeedParametrosMercadoAsync(HttpClient client, Guid bancoId)
    {
        // CDI — ignora 409 (pode já ter sido criado por outro teste na mesma fixture).
        await client.PostAsJsonAsync("/api/v1/cdi-snapshots", new
        {
            data = "2026-05-16",
            cdiAaPercentual = 10.50m
        });

        // PTAX D-1 para dataAbertura 2026-05-16.
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

    /// <summary>
    /// Cria um LimiteBanco com os itens de garantia informados e retorna o id criado.
    /// </summary>
    private static async Task<Guid> CriarLimiteBancoAsync(
        HttpClient client,
        Guid bancoId,
        object[] garantiasExigidas)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/limites-banco", new
        {
            bancoId,
            modalidade = "Finimp",
            valorLimiteBrl = 50_000_000m,
            dataVigenciaInicio = "2026-01-01",
            garantiasExigidas
        });
        res.IsSuccessStatusCode.Should().BeTrue($"seed limite falhou: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Executa o fluxo completo até o passo imediatamente antes da conversão:
    /// Criar → AddBanco → Enviar → Proposta → EncerrarCaptacao → Comparativo → Aceitar.
    /// Retorna o cotacaoId pronto para conversão.
    /// </summary>
    private static async Task<Guid> CriarCotacaoProntaParaConversaoAsync(
        HttpClient client,
        Guid bancoId,
        decimal valorAlvoBrl = 1_000_000m)
    {
        // 1. Criar cotação
        HttpResponseMessage criarRes = await client.PostAsJsonAsync("/api/v1/cotacoes", new
        {
            modalidade = "Finimp",
            valorAlvoBrl,
            prazoMaximoDias = 365,
            dataAbertura = "2026-05-16"
        });
        criarRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"criar cotação falhou: {await criarRes.Content.ReadAsStringAsync()}");
        Guid cotacaoId = (await criarRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        // 2. Adicionar banco — desabilita pré-preenchimento para evitar requisito de
        //    rendimentoCdbAaPercentual quando o limite tem CdbCativo como garantia exigida.
        //    O enforcement de garantias é testado na conversão, não no add-banco.
        HttpResponseMessage addRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/bancos",
            new { bancoId, preencherGarantiaAutomaticamente = false });
        addRes.IsSuccessStatusCode.Should().BeTrue(
            $"adicionar banco falhou: {await addRes.Content.ReadAsStringAsync()}");

        // 3. Enviar
        await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/enviar", null);

        // 4. Registrar proposta USD
        HttpResponseMessage propostaRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas",
            new
            {
                bancoId,
                moedaOriginal = "Usd",
                valorOferecido = 200_000m,      // 200k USD × PTAX 5.20 ≈ 1.040.000 BRL
                taxaAa = 6.0m,
                iofPct = 0.38m,
                spreadAa = 0.50m,
                prazoDias = 365,
                estruturaAmortizacao = "Bullet",
                periodicidadeJuros = "Bullet",
                exigeNdf = false,
                custoNdfAa = (decimal?)null,
                garantiaExigida = "Aval",
                valorGarantiaBrl = 1_100_000m,
                garantiaEhCdbCativo = false,
                rendimentoCdbAa = (decimal?)null
            });
        propostaRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"proposta falhou: {await propostaRes.Content.ReadAsStringAsync()}");
        Guid propostaId = (await propostaRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        // 5. Encerrar captação
        await client.PostAsync($"/api/v1/cotacoes/{cotacaoId}/encerrar-captacao", null);

        // 6. Aceitar proposta (comparativo calculado automaticamente pelo encerramento)
        HttpResponseMessage aceitarRes = await client.PostAsync(
            $"/api/v1/cotacoes/{cotacaoId}/propostas/{propostaId}/aceitar", null);
        aceitarRes.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"aceitar proposta falhou: {await aceitarRes.Content.ReadAsStringAsync()}");

        return cotacaoId;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // EH-01: 409 + ProblemDetails quando garantia obrigatória não está coberta
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cenário SC-04 / EH-01: banco exige CdbCativo 80% sobre o principal.
    /// Conversão sem garantias declaradas deve retornar HTTP 409 com ProblemDetails
    /// contendo: type, title, limiteBancoId, garantiasExigidasRevisaoId e lacunas.
    /// </summary>
    [Fact]
    public async Task Converter_GarantiaObrigatoriaAusente_Retorna409ComProblemDetails()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        // LimiteBanco: CdbCativo 80% obrigatório.
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 80m, obrigatoria = true }
        ]);

        // ValorAlvo 1.000.000 BRL → proposta 200k USD × 5.20 = 1.040.000 BRL → esperado 832.000 BRL.
        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // Converter sem declarar garantias.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "SC04-EH01",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m
                // garantiasContrato ausente → nenhuma garantia declarada
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"SC-04: conversão sem garantia obrigatória deve retornar 409. Body: {await convertRes.Content.ReadAsStringAsync()}");

        string raw = await convertRes.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        // Verifica estrutura do ProblemDetails (GlobalExceptionHandler §4.5)
        body.GetProperty("type").GetString().Should()
            .Be("https://sgcf.io/errors/garantia-exigida-nao-coberta",
               "type deve seguir a URI definida na SPEC §4.5");

        body.TryGetProperty("limiteBancoId", out _).Should().BeTrue("limiteBancoId deve estar nas extensions");
        body.TryGetProperty("garantiasExigidasRevisaoId", out _).Should().BeTrue("revisaoId deve estar nas extensions");

        body.GetProperty("lacunas").GetArrayLength().Should().BeGreaterThan(0,
            "pelo menos uma lacuna deve ser reportada");

        // Valida estrutura do primeiro elemento de lacunas
        JsonElement primeiraLacuna = body.GetProperty("lacunas")[0];
        primeiraLacuna.TryGetProperty("tipo", out _).Should().BeTrue("lacuna deve ter campo 'tipo'");
        primeiraLacuna.TryGetProperty("obrigatoria", out _).Should().BeTrue("lacuna deve ter campo 'obrigatoria'");
        primeiraLacuna.TryGetProperty("valorEsperadoBrl", out _).Should().BeTrue("lacuna deve ter campo 'valorEsperadoBrl'");
        primeiraLacuna.TryGetProperty("valorCobertoBrl", out _).Should().BeTrue("lacuna deve ter campo 'valorCobertoBrl'");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // EH-02: 201 quando garantia obrigatória está suficientemente coberta
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cenário SC-04 / EH-02: mesmo banco com CdbCativo 80% obrigatório, mas desta vez
    /// o operador declara garantia que cobre o valor exigido → retorna 201 Created.
    /// Verifica também que GET no contrato criado retorna o contrato.
    /// </summary>
    [Fact]
    public async Task Converter_GarantiaObrigatoriaComCoberturaSuficiente_Retorna201()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        // LimiteBanco: CdbCativo 80% obrigatório.
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 80m, obrigatoria = true }
        ]);

        // Cotação com 200k USD × PTAX 5.20 = 1.040.000 BRL → esperado = 832.000 BRL.
        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // Declarar CdbCativo acima do mínimo (900.000 > 832.000).
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "SC04-EH02",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m,
                garantiasContrato = new[]
                {
                    new
                    {
                        tipo = "CdbCativo",
                        valorBrl = 900_000m,
                        dataConstituicao = "2026-05-20"
                    }
                }
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"cobertura suficiente deve criar contrato: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.TryGetProperty("id", out JsonElement contratoIdEl).Should().BeTrue("contrato deve ter id");
        Guid contratoId = contratoIdEl.GetGuid();

        // GET /contratos/{id} deve retornar o contrato recém criado.
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/contratos/{contratoId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK,
            "contrato criado deve ser recuperável via GET");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // EH-03: 409 com Aval puro sem nenhum Aval declarado
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cenário SC-04 / EH-03: banco exige Aval puro (sem percentual/valor fixo).
    /// Conversão sem nenhum Aval declarado → 409 com lacuna cujos ValorEsperadoBrl
    /// e ValorCobertoBrl são null (caso especial do Aval puro documentado na SPEC §4.4).
    /// </summary>
    [Fact]
    public async Task Converter_AvalPuroObrigatorioAusente_Retorna409ComNullsNaLacuna()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        // LimiteBanco: Aval puro — sem percentual, sem valor fixo.
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "Aval", obrigatoria = true }
        ]);

        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // Declarar CdbCativo mas não Aval — o enforcement deve detectar a lacuna Aval.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "SC04-EH03",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m,
                garantiasContrato = new[]
                {
                    new
                    {
                        tipo = "CdbCativo",
                        valorBrl = 999_999m,
                        dataConstituicao = "2026-05-20"
                    }
                }
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"Aval puro sem cobertura deve retornar 409. Body: {await convertRes.Content.ReadAsStringAsync()}");

        string raw = await convertRes.Content.ReadAsStringAsync();
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(raw, JsonOpts);

        JsonElement lacunas = body.GetProperty("lacunas");
        lacunas.GetArrayLength().Should().Be(1, "apenas Aval puro está sem cobertura");

        JsonElement lacunaAval = lacunas[0];
        lacunaAval.GetProperty("tipo").GetString().Should().Be("Aval");

        // Aval puro: valorEsperadoBrl e valorCobertoBrl devem ser null no JSON.
        JsonValueKind expectedKind = JsonValueKind.Null;
        lacunaAval.GetProperty("valorEsperadoBrl").ValueKind.Should().Be(expectedKind,
            "Aval puro não tem valor monetário esperado — deve ser null");
        lacunaAval.GetProperty("valorCobertoBrl").ValueKind.Should().Be(expectedKind,
            "Aval puro sem cobertura — valorCobertoBrl deve ser null");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // EH-GRP-01: grupo "OU" com fração combinada < 1,0 → 409 com lacuna de grupo
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Garantias Alternativas (RV-GA): banco exige "CdbCativo 100% OU BoletoBancario 100%"
    /// (grupo). Principal = 200k USD × 5,20 = 1.040.000 BRL → alvo de cada alternativa 1.040.000.
    /// Conversão declarando 520k Cdb (0,5) + 416k Boleto (0,4) = fração 0,9 &lt; 1,0 →
    /// 409 com UMA lacuna de grupo (grupoAlternativaId, alternativasAceitas, fracaoCoberta 0,9).
    /// </summary>
    [Fact]
    public async Task Converter_GrupoOuFracaoInsuficiente_Retorna409ComLacunaDeGrupo()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        string grupo = Guid.NewGuid().ToString();
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = "Colateral FINIMP" },
            new { tipo = "BoletoBancario", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = "Colateral FINIMP" }
        ]);

        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // 520k/1.04M = 0,5 ; 416k/1.04M = 0,4 ; soma = 0,9 < 1,0 → bloqueado.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "GRP-01",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m,
                garantiasContrato = new[]
                {
                    new { tipo = "CdbCativo", valorBrl = 520_000m, dataConstituicao = "2026-05-20" },
                    new { tipo = "BoletoBancario", valorBrl = 416_000m, dataConstituicao = "2026-05-20" }
                }
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"fração de grupo < 1,0 deve bloquear. Body: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement body = JsonSerializer.Deserialize<JsonElement>(
            await convertRes.Content.ReadAsStringAsync(), JsonOpts);
        JsonElement lacunas = body.GetProperty("lacunas");
        lacunas.GetArrayLength().Should().Be(1, "uma única lacuna por grupo não coberto");

        JsonElement lacuna = lacunas[0];
        lacuna.GetProperty("grupoAlternativaId").GetGuid().Should().Be(Guid.Parse(grupo));
        lacuna.GetProperty("grupoRotulo").GetString().Should().Be("Colateral FINIMP");
        lacuna.GetProperty("fracaoCoberta").GetDecimal().Should().Be(0.9m);
        lacuna.GetProperty("alternativasAceitas").EnumerateArray()
            .Select(a => a.GetString())
            .Should().BeEquivalentTo(["CdbCativo", "BoletoBancario"]);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // EH-GRP-02: grupo "OU" coberto por combinação (Σ fração ≥ 1,0) → 201
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mesmo grupo "CdbCativo 100% OU BoletoBancario 100%". Conversão declarando
    /// 624k Cdb (0,6) + 416k Boleto (0,4) = 1,0 → liberada (201 Created).
    /// </summary>
    [Fact]
    public async Task Converter_GrupoOuCobertoPorCombinacao_Retorna201()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        string grupo = Guid.NewGuid().ToString();
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = "Colateral FINIMP" },
            new { tipo = "BoletoBancario", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = "Colateral FINIMP" }
        ]);

        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // 624k/1.04M = 0,6 ; 416k/1.04M = 0,4 ; soma = 1,0 → liberado.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "GRP-02",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m,
                garantiasContrato = new[]
                {
                    new { tipo = "CdbCativo", valorBrl = 624_000m, dataConstituicao = "2026-05-20" },
                    new { tipo = "BoletoBancario", valorBrl = 416_000m, dataConstituicao = "2026-05-20" }
                }
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"combinação cobrindo o grupo deve liberar. Body: {await convertRes.Content.ReadAsStringAsync()}");
    }
}
