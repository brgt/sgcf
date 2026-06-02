using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Sgcf.Api.IntegrationTests.Cotacoes;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Contratos;

/// <summary>
/// Testes E2E dos indicadores de garantia (GET /contratos/{id}/garantias/indicadores)
/// para um contrato cambial (USD/Finimp) cujo limite usa política de GRUPO
/// ("CdbCativo 100% OU BoletoBancario 100%").
///
/// Guarda de regressão anti-dupla-contagem: os indicadores são dirigidos pela
/// cobertura REALMENTE declarada no contrato (soma dos valores das garantias ativas),
/// e NÃO pela soma dos alvos exigidos pelo grupo. Um grupo com duas alternativas de
/// 100% do principal NÃO deve inflar a cobertura para ~2× o principal — o indicador
/// reflete apenas a garantia efetivamente constituída.
///
/// Usa a fixture CotacoesApi (PostgreSQL real via Testcontainers).
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class IndicadoresGarantiaGrupoHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers de seed (espelham ConverterEmContratoEnforcementHttpTests)
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
    // IND-GRP-01: indicadores refletem a cobertura REAL, não a soma dos alvos do grupo
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Guarda de regressão (anti-dupla-contagem): contrato cambial (USD/Finimp) cujo
    /// limite usa política de grupo "CdbCativo 100% OU BoletoBancario 100%". O principal
    /// é 200k USD × PTAX 5,20 = 1.040.000 BRL, de modo que cada alternativa do grupo
    /// teria um alvo de 1.040.000 BRL (soma ingênua dos dois alvos = ~2.080.000 BRL).
    ///
    /// A conversão declara apenas CdbCativo de 1.040.000 BRL (cobre o grupo: fração 1,0).
    /// Os indicadores devem refletir a cobertura EFETIVAMENTE declarada — 1.040.000 BRL —
    /// e NÃO a soma dos alvos exigidos pelo grupo. O percentual permanece 0 porque o
    /// indicador é gated por moeda: principalBrl só é preenchido para contratos BRL
    /// (GetIndicadoresGarantiaQuery.cs:36-42); contratos cambiais geram 0 por design.
    /// </summary>
    [Fact]
    public async Task Indicadores_ContratoCambialComPoliticaDeGrupo_RefletemCoberturaRealNaoAlvo()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        // LimiteBanco: grupo "CdbCativo 100% OU BoletoBancario 100%".
        string grupo = Guid.NewGuid().ToString();
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = "Colateral FINIMP" },
            new { tipo = "BoletoBancario", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = "Colateral FINIMP" }
        ]);

        // Principal = 200k USD × PTAX 5,20 = 1.040.000 BRL.
        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // Declara CdbCativo de 1.040.000 (fração 1,0 do alvo do grupo) → grupo coberto → 201.
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "IND-GRP-01",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m,
                garantiasContrato = new[]
                {
                    new { tipo = "CdbCativo", valorBrl = 1_040_000m, dataConstituicao = "2026-05-20" }
                }
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"CdbCativo cobrindo o grupo deve liberar a conversão. Body: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.TryGetProperty("id", out JsonElement contratoIdEl).Should().BeTrue("contrato deve ter id");
        Guid contratoId = contratoIdEl.GetGuid();

        // GET /contratos/{id}/garantias/indicadores deve refletir a cobertura real.
        HttpResponseMessage indicadoresRes = await client.GetAsync(
            $"/api/v1/contratos/{contratoId}/garantias/indicadores");
        indicadoresRes.StatusCode.Should().Be(HttpStatusCode.OK,
            $"indicadores devem ser recuperáveis. Body: {await indicadoresRes.Content.ReadAsStringAsync()}");

        JsonElement indicadores = await indicadoresRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // KEY: cobertura total reflete a garantia REALMENTE declarada (1.040.000),
        // NÃO ~2.080.000 que a soma ingênua dos dois alvos de 100% do grupo produziria.
        indicadores.GetProperty("coberturaTotalBrl").GetDecimal().Should().Be(1_040_000m,
            "indicadores refletem a cobertura real declarada, não a soma dos alvos do grupo");

        // Percentual é gated por moeda: contratos cambiais (USD) rendem 0 porque
        // principalBrl só é preenchido para contratos BRL (GetIndicadoresGarantiaQuery.cs:36-42).
        // Comportamento real e intencional — não é uma preocupação de grupo.
        indicadores.GetProperty("percentualCoberturaTotalPct").GetDecimal().Should().Be(0m,
            "percentual é gated por moeda: contratos cambiais rendem 0 (principalBrl só existe para BRL)");

        // Apenas CdbCativo foi declarado → cobertura líquida (sem CdbCativo) é 0.
        indicadores.GetProperty("coberturaLiquidaSemCdbBrl").GetDecimal().Should().Be(0m,
            "apenas CdbCativo declarado → cobertura líquida sem CDB é zero");

        // Alerta de operação em moeda estrangeira deve estar presente.
        indicadores.GetProperty("alertas").EnumerateArray()
            .Select(a => a.GetString())
            .Should().Contain(s => s != null && s.Contains("moeda estrangeira", StringComparison.Ordinal),
                "contrato cambial deve emitir o alerta de moeda estrangeira");
    }
}
