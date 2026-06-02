using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Sgcf.Api.IntegrationTests.Cotacoes;
using Xunit;

namespace Sgcf.Api.IntegrationTests.Contratos;

/// <summary>
/// Teste de round-trip RF-13: ao converter uma cotação cuja política vigente declara um
/// GRUPO de garantias alternativas "OU" (mesmo <c>grupoAlternativaId</c>), o snapshot
/// retornado por <c>GET /api/v1/contratos/{id}</c> deve preservar os campos de grupo
/// (<c>grupoAlternativaId</c> e <c>grupoRotulo</c>) em TODOS os itens do grupo.
/// Garante que a política agrupada foi "congelada" no contrato (SC-05 / RF-13).
/// Usa a fixture CotacoesApi (PostgreSQL real via Testcontainers).
/// </summary>
[Collection("CotacoesApi")]
[Trait("Category", "Slow")]
public sealed class SnapshotGrupoRoundTripHttpTests(CotacoesApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers de seed (espelham ConverterEmContratoEnforcementHttpTests)
    // ──────────────────────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarBancoAsync(HttpClient client)
    {
        string codigo = Random.Shared.Next(600, 699).ToString(CultureInfo.InvariantCulture);
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/bancos", new
        {
            codigoCompe = codigo,
            razaoSocial = $"Banco Snapshot {codigo} S.A.",
            apelido = $"BS{codigo}",
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
    /// Criar → AddBanco → Enviar → Proposta → EncerrarCaptacao → Aceitar.
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
                valorOferecido = 200_000m,      // 200k USD × PTAX 5.20 = 1.040.000 BRL
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
    // RF-13: snapshot do contrato preserva os campos de grupo da revisão vigente
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Garantias Alternativas (RF-13): banco exige "CdbCativo 100% OU BoletoBancario 100%"
    /// (grupo). Principal = 200k USD × 5,20 = 1.040.000 BRL. A conversão cobre o grupo com
    /// CdbCativo 1.040.000 (fração 1,0) → 201. O snapshot retornado por GET /contratos/{id}
    /// deve conter AMBOS os itens do grupo, cada um carregando o mesmo grupoAlternativaId e
    /// grupoRotulo — provando que a política agrupada foi congelada no contrato.
    /// </summary>
    [Fact]
    public async Task Snapshot_ContratoConvertidoComGrupo_PreservaGrupoAlternativaIdERotulo()
    {
        using HttpClient client = fixture.CreateAuthenticatedClient();

        Guid bancoId = await CriarBancoAsync(client);
        await SeedParametrosMercadoAsync(client, bancoId);

        // Grupo "OU": CdbCativo 100% OU BoletoBancario 100%, rótulo "Colateral FINIMP".
        string grupo = Guid.NewGuid().ToString();
        const string rotulo = "Colateral FINIMP";
        await CriarLimiteBancoAsync(client, bancoId,
        [
            new { tipo = "CdbCativo", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = rotulo },
            new { tipo = "BoletoBancario", percentualSobreLimite = 100m, grupoAlternativaId = grupo, grupoRotulo = rotulo }
        ]);

        Guid cotacaoId = await CriarCotacaoProntaParaConversaoAsync(client, bancoId);

        // Cobre o grupo com CdbCativo cobrindo 100% do principal (1.040.000 BRL).
        HttpResponseMessage convertRes = await client.PostAsJsonAsync(
            $"/api/v1/cotacoes/{cotacaoId}/converter-em-contrato",
            new
            {
                cotacaoId,
                numeroExternoContrato = "RF13-SNAP",
                dataContratacao = "2026-05-20",
                dataVencimento = "2027-05-20",
                taxaAa = 6.0m,
                garantiasContrato = new[]
                {
                    new { tipo = "CdbCativo", valorBrl = 1_040_000m, dataConstituicao = "2026-05-20" }
                }
            });

        convertRes.StatusCode.Should().Be(HttpStatusCode.Created,
            $"cobertura total do grupo deve criar contrato. Body: {await convertRes.Content.ReadAsStringAsync()}");

        JsonElement contratoBody = await convertRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        contratoBody.TryGetProperty("id", out JsonElement contratoIdEl).Should().BeTrue("contrato deve ter id");
        Guid contratoId = contratoIdEl.GetGuid();

        // GET /contratos/{id} (endpoint de detalhe inclui o snapshot).
        HttpResponseMessage getRes = await client.GetAsync($"/api/v1/contratos/{contratoId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK,
            "contrato criado deve ser recuperável via GET de detalhe");

        JsonElement detalhe = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        detalhe.TryGetProperty("garantiasExigidasSnapshot", out JsonElement snapshot).Should().BeTrue(
            "o endpoint de detalhe deve projetar garantiasExigidasSnapshot");
        snapshot.ValueKind.Should().Be(JsonValueKind.Array,
            "garantiasExigidasSnapshot deve ser um array no endpoint de detalhe");

        // O grupo tem 2 alternativas → ambas devem aparecer no snapshot congelado.
        snapshot.GetArrayLength().Should().Be(2,
            "o snapshot deve preservar as 2 alternativas do grupo declaradas na política vigente");

        var itens = snapshot.EnumerateArray().ToList();

        // Ambos os itens carregam o mesmo grupoAlternativaId e grupoRotulo da política.
        Guid grupoEsperado = Guid.Parse(grupo);
        foreach (JsonElement item in itens)
        {
            item.GetProperty("grupoAlternativaId").GetGuid().Should().Be(grupoEsperado,
                "cada item do grupo deve preservar o grupoAlternativaId da revisão vigente");
            item.GetProperty("grupoRotulo").GetString().Should().Be(rotulo,
                "cada item do grupo deve preservar o grupoRotulo da revisão vigente");
        }

        // Os tipos congelados são exatamente as duas alternativas do grupo.
        itens.Select(i => i.GetProperty("tipo").GetString())
            .Should().BeEquivalentTo(["CdbCativo", "BoletoBancario"],
                "o snapshot deve congelar ambos os tipos do grupo de alternativas");
    }
}
