using System.IO;
using System.Text.Json;

using FluentAssertions;

using NodaTime;
using NodaTime.Text;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.GoldenDataset.Cotacoes;

/// <summary>
/// Testes golden do módulo de Cotações — 5 cenários obrigatórios do MVP.
/// Dados autoritativos em data/cotacoes/*/. Não altere saida_esperada.json sem aprovação de negócio.
/// Referência: docs/specs/cotacoes/SPEC.md §10.2.
/// Tolerâncias: CET ±0,01 p.p.; Valores monetários ±0,01 BRL.
/// </summary>
[Trait("Category", "Golden")]
public sealed class CotacoesGoldenTests
{
    private static readonly string DataDir =
        Path.Combine(AppContext.BaseDirectory, "data", "cotacoes");

    private static readonly LocalDate DataDesembolso = new(2026, 5, 15);

    private const decimal ToleranciaValorBrl = 1.00m;   // ±R$ 1,00
    private const decimal ToleranciaCetPp = 0.01m;       // ±0,01 p.p.

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static LocalDate ParseLocalDate(string iso8601) =>
        LocalDatePattern.Iso.Parse(iso8601).GetValueOrThrow();

    private static JsonElement LerJson(string cenario, string arquivo)
    {
        string path = Path.Combine(DataDir, cenario, arquivo);
        string json = File.ReadAllText(path);
        // Mantém o documento na memória via campo auxiliar (pattern do projeto)
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Cria Proposta usando o construtor internal (visível via InternalsVisibleTo).
    /// Replica o que RegistrarPropostaCommandHandler faria em produção.
    /// </summary>
    private static Proposta CriarProposta(
        decimal valorOferecidoUsd,
        decimal taxaAaPercentual,
        decimal spreadAaPercentual,
        decimal iofPercentual,
        int prazoDias,
        bool exigeNdf = false,
        decimal? custoNdfAaPercentual = null,
        bool garantiaEhCdbCativo = false,
        decimal? rendimentoCdbAaPercentual = null,
        decimal valorGarantiaExigidaBrl = 0m,
        Moeda moedaOriginal = Moeda.Usd)
    {
        return new Proposta(
            cotacaoId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            moedaOriginal: moedaOriginal,
            valorOferecidoMoedaOriginal: new Money(valorOferecidoUsd, moedaOriginal),
            taxaAaPercentual: taxaAaPercentual,
            iofPercentual: iofPercentual,
            spreadAaPercentual: spreadAaPercentual,
            prazoDias: prazoDias,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: exigeNdf,
            custoNdfAaPercentual: custoNdfAaPercentual,
            garantiaExigida: garantiaEhCdbCativo ? "CDB Cativo" : "Aval",
            valorGarantiaExigidaBrl: new Money(valorGarantiaExigidaBrl, Moeda.Brl),
            garantiaEhCdbCativo: garantiaEhCdbCativo,
            rendimentoCdbAaPercentual: rendimentoCdbAaPercentual,
            dataCaptura: DataDesembolso);
    }

    // ── Cenário 1: FINIMP USD 3 bancos ─────────────────────────────────────

    /// <summary>
    /// Golden 1: três propostas USD, ranking por CET (Bradesco menor, BB intermediário, Santander maior).
    /// Valida também economia ao fechar com taxa -0,2 p.p. em relação à proposta vencedora.
    /// SPEC §10.2 cenário 1.
    /// </summary>
    [Fact]
    public void FinimpUsd3Bancos_RankingPorCet_MatchesGoldenDataset()
    {
        // Arrange
        JsonElement e = LerJson("finimp-usd-3bancos", "entrada.json");
        JsonElement s = LerJson("finimp-usd-3bancos", "saida_esperada.json");

        decimal ptax = e.GetProperty("cotacao").GetProperty("ptaxUsadaUsdBrl").GetDecimal();
        decimal taxaCdi = e.GetProperty("taxaCdiAaPercentual").GetDecimal();

        JsonElement[] propostas = e.GetProperty("propostas").EnumerateArray().ToArray();
        JsonElement[] cetsEsperados = s.GetProperty("cets").EnumerateArray().ToArray();
        JsonElement[] rankingEsperado = s.GetProperty("rankingPorCet").EnumerateArray().ToArray();
        JsonElement economiaSaida = s.GetProperty("economia");

        // Act: calcular CET de cada proposta
        var cetsCalculados = new List<(string Banco, decimal Cet)>();

        foreach (JsonElement propostaJson in propostas)
        {
            Proposta proposta = CriarProposta(
                valorOferecidoUsd: propostaJson.GetProperty("valorOferecidoUsd").GetDecimal(),
                taxaAaPercentual: propostaJson.GetProperty("taxaAaPercentual").GetDecimal(),
                spreadAaPercentual: propostaJson.GetProperty("spreadAaPercentual").GetDecimal(),
                iofPercentual: propostaJson.GetProperty("iofPercentual").GetDecimal(),
                prazoDias: propostaJson.GetProperty("prazoDias").GetInt32(),
                valorGarantiaExigidaBrl: propostaJson.GetProperty("valorGarantiaExigidaBrl").GetDecimal());

            decimal cet = CalculadoraCet.CalcularCet(proposta, ptax, DataDesembolso);
            cetsCalculados.Add((propostaJson.GetProperty("banco").GetString()!, cet));
        }

        // Assert — CET de cada banco dentro da tolerância
        for (int i = 0; i < cetsEsperados.Length; i++)
        {
            string banco = cetsEsperados[i].GetProperty("banco").GetString()!;
            decimal cetEsperado = cetsEsperados[i].GetProperty("cetAaPercentual").GetDecimal();
            decimal cetCalculado = cetsCalculados.First(c => c.Banco == banco).Cet;

            cetCalculado.Should().BeApproximately(cetEsperado, ToleranciaCetPp,
                because: $"CET do {banco} deve bater com o golden (tolerância ±{ToleranciaCetPp} p.p.)");
        }

        // Assert — ranking correto (menor CET primeiro)
        List<string> rankingCalculado = cetsCalculados
            .OrderBy(c => c.Cet)
            .Select(c => c.Banco)
            .ToList();

        List<string> rankingGolden = rankingEsperado
            .Select(r => r.GetString()!)
            .ToList();

        rankingCalculado.Should().Equal(rankingGolden,
            because: "ranking por CET deve ser Bradesco < BB < Santander");

        // Assert — economia da proposta vencedora (Bradesco) ao fechar com taxa -0,2 p.p.
        decimal cetPropostaVencedora = economiaSaida.GetProperty("cetContratoAaPercentual").GetDecimal();
        decimal cetBradesco = cetsCalculados.First(c => c.Banco == "Bradesco").Cet;

        // Proposta aceita = CET do cenário entrada (6.279086%)
        decimal cetPropostaAceita = e.GetProperty("cenarioEconomia")
            .GetProperty("propostaCetAaPercentual").GetDecimal();

        decimal valorAlvoBrl = e.GetProperty("cotacao").GetProperty("valorAlvoBrl").GetDecimal();
        int prazoProposta = propostas[0].GetProperty("prazoDias").GetInt32(); // 180

        var (economiaBruta, economiaAjustada, _) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: cetPropostaAceita,
            cetContratoAaPercentual: cetPropostaVencedora,
            valorPrincipalBrl: new Money(valorAlvoBrl, Moeda.Brl),
            prazoProposta: prazoProposta,
            prazoContrato: prazoProposta,
            taxaCdiAaPercentual: taxaCdi,
            dataReferenciaCdi: DataDesembolso);

        decimal economiaBrutaEsperada = economiaSaida.GetProperty("economiaBrutaBrl").GetDecimal();
        decimal economiaAjustadaEsperada = economiaSaida.GetProperty("economiaAjustadaCdiBrl").GetDecimal();

        economiaBruta.Valor.Should().BeApproximately(economiaBrutaEsperada, ToleranciaValorBrl,
            because: "economia bruta deve bater com o golden (tolerância ±R$1,00)");
        economiaAjustada.Valor.Should().BeApproximately(economiaAjustadaEsperada, ToleranciaValorBrl,
            because: "economia ajustada CDI deve bater com o golden (tolerância ±R$1,00)");
    }

    // ── Cenário 2: FINIMP CNY NDF obrigatório ─────────────────────────────

    /// <summary>
    /// Golden 2: proposta CNY com NDF obrigatório — custo NDF eleva o CET.
    /// Valida que cetComNdf > cetSemNdf e que o adicional respeita o limite mínimo.
    /// SPEC §10.2 cenário 2.
    /// </summary>
    [Fact]
    public void FinimpCnyNdfObrigatorio_CetComNdfMaiorQueSeemNdf_MatchesGoldenDataset()
    {
        // Arrange
        JsonElement e = LerJson("finimp-cny-ndf-obrigatorio", "entrada.json");
        JsonElement s = LerJson("finimp-cny-ndf-obrigatorio", "saida_esperada.json");

        decimal ptax = e.GetProperty("cotacao").GetProperty("ptaxUsadaUsdBrl").GetDecimal();
        JsonElement propostaJson = e.GetProperty("proposta");

        decimal valorOferecido = propostaJson.GetProperty("valorOferecidoUsd").GetDecimal();
        decimal taxaAa = propostaJson.GetProperty("taxaAaPercentual").GetDecimal();
        decimal spreadAa = propostaJson.GetProperty("spreadAaPercentual").GetDecimal();
        decimal iof = propostaJson.GetProperty("iofPercentual").GetDecimal();
        int prazo = propostaJson.GetProperty("prazoDias").GetInt32();
        decimal custoNdf = propostaJson.GetProperty("custoNdfAaPercentual").GetDecimal();
        decimal valGarantia = propostaJson.GetProperty("valorGarantiaExigidaBrl").GetDecimal();

        // Act: calcular CET sem NDF e com NDF
        Proposta propostaSemNdf = CriarProposta(
            valorOferecidoUsd: valorOferecido,
            taxaAaPercentual: taxaAa,
            spreadAaPercentual: spreadAa,
            iofPercentual: iof,
            prazoDias: prazo,
            exigeNdf: false,
            valorGarantiaExigidaBrl: valGarantia,
            moedaOriginal: Moeda.Cny);

        Proposta propostaComNdf = CriarProposta(
            valorOferecidoUsd: valorOferecido,
            taxaAaPercentual: taxaAa,
            spreadAaPercentual: spreadAa,
            iofPercentual: iof,
            prazoDias: prazo,
            exigeNdf: true,
            custoNdfAaPercentual: custoNdf,
            valorGarantiaExigidaBrl: valGarantia,
            moedaOriginal: Moeda.Cny);

        decimal cetSemNdf = CalculadoraCet.CalcularCet(propostaSemNdf, ptax, DataDesembolso);
        decimal cetComNdf = CalculadoraCet.CalcularCet(propostaComNdf, ptax, DataDesembolso);

        // Assert — valores absolutos dentro da tolerância
        decimal cetSemNdfEsperado = s.GetProperty("cetSemNdfAaPercentual").GetDecimal();
        decimal cetComNdfEsperado = s.GetProperty("cetComNdfAaPercentual").GetDecimal();
        decimal adicionalEsperado = s.GetProperty("adicionalNdfPercentualPontos").GetDecimal();

        cetSemNdf.Should().BeApproximately(cetSemNdfEsperado, ToleranciaCetPp,
            because: "CET sem NDF deve bater com o golden");
        cetComNdf.Should().BeApproximately(cetComNdfEsperado, ToleranciaCetPp,
            because: "CET com NDF deve bater com o golden");

        // Assert — invariante: NDF sempre eleva o CET
        cetComNdf.Should().BeGreaterThan(cetSemNdf,
            because: "custo NDF obrigatório DEVE elevar o CET (SPEC §5.1 e §10.2)");

        decimal adicionalCalculado = cetComNdf - cetSemNdf;
        adicionalCalculado.Should().BeApproximately(adicionalEsperado, ToleranciaCetPp,
            because: "adicional do NDF deve bater com o golden dentro da tolerância");
    }

    // ── Cenário 3: FINIMP USD Garantia CDB Cativo ─────────────────────────

    /// <summary>
    /// Golden 3: proposta USD com garantia CDB cativo — rendimento reduz o CET efetivo.
    /// Garante que cetComCdb &#60; cetSemCdb e que o CET resultante permanece positivo.
    /// SPEC §10.2 cenário 3.
    /// </summary>
    [Fact]
    public void FinimpUsdGarantiaCdb_RendimentoReduzCet_MatchesGoldenDataset()
    {
        // Arrange
        JsonElement e = LerJson("finimp-usd-garantia-cdb", "entrada.json");
        JsonElement s = LerJson("finimp-usd-garantia-cdb", "saida_esperada.json");

        decimal ptax = e.GetProperty("cotacao").GetProperty("ptaxUsadaUsdBrl").GetDecimal();

        JsonElement semCdbJson = e.GetProperty("propostaSemCdb");
        JsonElement comCdbJson = e.GetProperty("propostaComCdb");

        // Act
        Proposta propostaSemCdb = CriarProposta(
            valorOferecidoUsd: semCdbJson.GetProperty("valorOferecidoUsd").GetDecimal(),
            taxaAaPercentual: semCdbJson.GetProperty("taxaAaPercentual").GetDecimal(),
            spreadAaPercentual: semCdbJson.GetProperty("spreadAaPercentual").GetDecimal(),
            iofPercentual: semCdbJson.GetProperty("iofPercentual").GetDecimal(),
            prazoDias: semCdbJson.GetProperty("prazoDias").GetInt32(),
            garantiaEhCdbCativo: false,
            valorGarantiaExigidaBrl: semCdbJson.GetProperty("valorGarantiaExigidaBrl").GetDecimal());

        Proposta propostaComCdb = CriarProposta(
            valorOferecidoUsd: comCdbJson.GetProperty("valorOferecidoUsd").GetDecimal(),
            taxaAaPercentual: comCdbJson.GetProperty("taxaAaPercentual").GetDecimal(),
            spreadAaPercentual: comCdbJson.GetProperty("spreadAaPercentual").GetDecimal(),
            iofPercentual: comCdbJson.GetProperty("iofPercentual").GetDecimal(),
            prazoDias: comCdbJson.GetProperty("prazoDias").GetInt32(),
            garantiaEhCdbCativo: true,
            rendimentoCdbAaPercentual: comCdbJson.GetProperty("rendimentoCdbAaPercentual").GetDecimal(),
            valorGarantiaExigidaBrl: comCdbJson.GetProperty("valorGarantiaExigidaBrl").GetDecimal());

        decimal cetSemCdb = CalculadoraCet.CalcularCet(propostaSemCdb, ptax, DataDesembolso);
        decimal cetComCdb = CalculadoraCet.CalcularCet(propostaComCdb, ptax, DataDesembolso);

        // Assert — valores absolutos
        decimal cetSemCdbEsperado = s.GetProperty("cetSemCdbAaPercentual").GetDecimal();
        decimal cetComCdbEsperado = s.GetProperty("cetComCdbAaPercentual").GetDecimal();
        decimal reducaoEsperada = s.GetProperty("reducaoCetPercentualPontos").GetDecimal();

        cetSemCdb.Should().BeApproximately(cetSemCdbEsperado, ToleranciaCetPp,
            because: "CET sem CDB deve bater com o golden");
        cetComCdb.Should().BeApproximately(cetComCdbEsperado, ToleranciaCetPp,
            because: "CET com CDB deve bater com o golden");

        // Assert — invariantes
        cetComCdb.Should().BeLessThan(cetSemCdb,
            because: "rendimento CDB cativo DEVE reduzir o CET (SPEC §5.1 e §10.2)");
        cetComCdb.Should().BePositive(
            because: "CDB de 30% do principal não deve superar o custo total (CET deve permanecer positivo)");

        decimal reducaoCalculada = cetSemCdb - cetComCdb;
        reducaoCalculada.Should().BeApproximately(reducaoEsperada, ToleranciaCetPp,
            because: "redução do CET deve bater com o golden dentro da tolerância");
    }

    // ── Cenário 4: Comparação com prazos diferentes ────────────────────────

    /// <summary>
    /// Golden 4: banco A (180d) vs banco B (270d) — três métricas lado a lado.
    /// Taxa nominal, CET e custo total equivalente equalizado por CDI para o prazo da cotação.
    /// SPEC §5.3 e §10.2 cenário 4.
    /// </summary>
    [Fact]
    public void ComparacaoPrazosDiferentes_TresMetricas_MatchesGoldenDataset()
    {
        // Arrange
        JsonElement e = LerJson("comparacao-prazos-diferentes", "entrada.json");
        JsonElement s = LerJson("comparacao-prazos-diferentes", "saida_esperada.json");

        decimal ptax = e.GetProperty("cotacao").GetProperty("ptaxUsadaUsdBrl").GetDecimal();
        decimal taxaCdi = e.GetProperty("taxaCdiAaPercentual").GetDecimal();
        decimal valorAlvoBrl = e.GetProperty("cotacao").GetProperty("valorAlvoBrl").GetDecimal();

        JsonElement[] propostasJson = e.GetProperty("propostas").EnumerateArray().ToArray();
        JsonElement[] propostasSaidaJson = s.GetProperty("propostas").EnumerateArray().ToArray();

        // Act + Assert por proposta
        for (int i = 0; i < propostasJson.Length; i++)
        {
            JsonElement pJson = propostasJson[i];
            JsonElement pSaida = propostasSaidaJson[i];

            string banco = pJson.GetProperty("banco").GetString()!;
            decimal taxaAa = pJson.GetProperty("taxaAaPercentual").GetDecimal();
            decimal spreadAa = pJson.GetProperty("spreadAaPercentual").GetDecimal();
            int prazo = pJson.GetProperty("prazoDias").GetInt32();

            Proposta proposta = CriarProposta(
                valorOferecidoUsd: pJson.GetProperty("valorOferecidoUsd").GetDecimal(),
                taxaAaPercentual: taxaAa,
                spreadAaPercentual: spreadAa,
                iofPercentual: pJson.GetProperty("iofPercentual").GetDecimal(),
                prazoDias: prazo,
                valorGarantiaExigidaBrl: pJson.GetProperty("valorGarantiaExigidaBrl").GetDecimal());

            // Métrica 1: taxa nominal
            decimal taxaNominalCalculada = taxaAa + spreadAa;
            decimal taxaNominalEsperada = pSaida.GetProperty("taxaNominalAaPercentual").GetDecimal();
            taxaNominalCalculada.Should().Be(taxaNominalEsperada,
                because: $"{banco}: taxa nominal deve ser soma exata de taxa + spread");

            // Métrica 2: CET
            decimal cet = CalculadoraCet.CalcularCet(proposta, ptax, DataDesembolso);
            decimal cetEsperado = pSaida.GetProperty("cetAaPercentual").GetDecimal();
            cet.Should().BeApproximately(cetEsperado, ToleranciaCetPp,
                because: $"{banco}: CET deve bater com o golden (tolerância ±{ToleranciaCetPp} p.p.)");

            // Métrica 3: custo total equivalente em BRL (VPL via CDI)
            // Fórmula: custo_total = principal × CET × prazo/360; VPL = custo / (1 + CDI × prazo/360)
            decimal principalBrl = valorAlvoBrl;
            decimal cdiDecimal = taxaCdi / 100m;
            decimal custoTotal = principalBrl * cet / 100m * prazo / 360m;
            decimal fatorDesconto = 1m / (1m + cdiDecimal * prazo / 360m);
            decimal vpl = Math.Round(custoTotal * fatorDesconto, 2, MidpointRounding.AwayFromZero);

            decimal vplEsperado = pSaida.GetProperty("custoTotalEquivalenteBrl").GetDecimal();
            vpl.Should().BeApproximately(vplEsperado, ToleranciaValorBrl,
                because: $"{banco}: custo total equivalente BRL deve bater com o golden (tolerância ±R${ToleranciaValorBrl})");
        }

        // Assert — ordenamento: banco A (180d) tem VPL menor que banco B (270d)
        decimal vplA = CalcularVplCustoTotal(propostasJson[0], ptax, valorAlvoBrl, taxaCdi);
        decimal vplB = CalcularVplCustoTotal(propostasJson[1], ptax, valorAlvoBrl, taxaCdi);

        vplA.Should().BeLessThan(vplB,
            because: "BancoA (180d, taxa nominal maior) tem custo total equivalente MENOR que BancoB (270d) — prazo mais longo eleva o VPL absoluto");
    }

    // ── Cenário 5: Economia mensal agregada ───────────────────────────────

    /// <summary>
    /// Golden 5: 3 cotações convertidas em abril/2026 — relatório agregado por mês e por banco.
    /// Valida totais e subtotais usando CalculadoraEconomia diretamente (sem I/O).
    /// SPEC §10.2 cenário 5.
    /// </summary>
    [Fact]
    public void EconomiaMensalAgregada_TresCotacoes_MatchesGoldenDataset()
    {
        // Arrange
        JsonElement e = LerJson("economia-mensal-agregada", "entrada.json");
        JsonElement s = LerJson("economia-mensal-agregada", "saida_esperada.json");

        decimal taxaCdi = e.GetProperty("taxaCdiAaPercentual").GetDecimal();
        JsonElement[] economiasJson = e.GetProperty("economias").EnumerateArray().ToArray();

        // Act: calcular economia de cada cotação
        var economiasCalculadas = new List<(string Banco, decimal EconomiaBruta, decimal EconomiaAjustada)>();

        foreach (JsonElement ecJson in economiasJson)
        {
            string banco = ecJson.GetProperty("banco").GetString()!;
            decimal cetProposta = ecJson.GetProperty("cetPropostaAaPercentual").GetDecimal();
            decimal cetContrato = ecJson.GetProperty("cetContratoAaPercentual").GetDecimal();
            decimal principalBrl = ecJson.GetProperty("valorPrincipalBrl").GetDecimal();
            int prazoProposta = ecJson.GetProperty("prazoProposta").GetInt32();
            int prazoContrato = ecJson.GetProperty("prazoContrato").GetInt32();
            LocalDate dataRef = ParseLocalDate(ecJson.GetProperty("dataReferenciaCdi").GetString()!);

            var (bruta, ajustada, _) = CalculadoraEconomia.Calcular(
                cetPropostaAaPercentual: cetProposta,
                cetContratoAaPercentual: cetContrato,
                valorPrincipalBrl: new Money(principalBrl, Moeda.Brl),
                prazoProposta: prazoProposta,
                prazoContrato: prazoContrato,
                taxaCdiAaPercentual: taxaCdi,
                dataReferenciaCdi: dataRef);

            economiasCalculadas.Add((banco, bruta.Valor, ajustada.Valor));
        }

        // Assert — totais do período
        JsonElement totaisEsperados = s.GetProperty("totais");
        int totalOpsEsperado = totaisEsperados.GetProperty("totalOperacoes").GetInt32();
        decimal totalBrutaEsperada = totaisEsperados.GetProperty("totalEconomiaBrutaBrl").GetDecimal();
        decimal totalAjustadaEsperada = totaisEsperados.GetProperty("totalEconomiaAjustadaCdiBrl").GetDecimal();

        decimal totalBrutaCalculada = Math.Round(
            economiasCalculadas.Sum(e => e.EconomiaBruta), 2, MidpointRounding.AwayFromZero);
        decimal totalAjustadaCalculada = Math.Round(
            economiasCalculadas.Sum(e => e.EconomiaAjustada), 2, MidpointRounding.AwayFromZero);

        economiasCalculadas.Should().HaveCount(totalOpsEsperado,
            because: "deve haver exatamente 3 operações no período");
        totalBrutaCalculada.Should().BeApproximately(totalBrutaEsperada, ToleranciaValorBrl,
            because: "total economia bruta deve bater com o golden");
        totalAjustadaCalculada.Should().BeApproximately(totalAjustadaEsperada, ToleranciaValorBrl,
            because: "total economia ajustada CDI deve bater com o golden");

        // Assert — subtotais por banco
        JsonElement[] bancosEsperados = s.GetProperty("porBanco").EnumerateArray().ToArray();

        foreach (JsonElement bancoEsperado in bancosEsperados)
        {
            string banco = bancoEsperado.GetProperty("banco").GetString()!;
            decimal brutaEsperada = bancoEsperado.GetProperty("economiaBrutaBrl").GetDecimal();

            decimal brutaCalculada = Math.Round(
                economiasCalculadas.Where(e => e.Banco == banco).Sum(e => e.EconomiaBruta),
                2,
                MidpointRounding.AwayFromZero);

            brutaCalculada.Should().BeApproximately(brutaEsperada, ToleranciaValorBrl,
                because: $"economia bruta do {banco} deve bater com o golden");
        }

        // Assert — por mês (único mês: 2026-04)
        JsonElement mesEsperado = s.GetProperty("porMes").EnumerateArray().First();
        int anoEsperado = mesEsperado.GetProperty("ano").GetInt32();
        int mesNumEsperado = mesEsperado.GetProperty("mes").GetInt32();
        int opsEsperado = mesEsperado.GetProperty("quantidadeOperacoes").GetInt32();

        anoEsperado.Should().Be(2026, because: "todas as cotações são de 2026");
        mesNumEsperado.Should().Be(4, because: "todas as cotações são de abril");
        opsEsperado.Should().Be(3, because: "há 3 cotações convertidas no mês");
    }

    // ─── Helper interno ────────────────────────────────────────────────────

    private static decimal CalcularVplCustoTotal(
        JsonElement propostaJson,
        decimal ptax,
        decimal principalBrl,
        decimal taxaCdi)
    {
        decimal taxaAa = propostaJson.GetProperty("taxaAaPercentual").GetDecimal();
        decimal spreadAa = propostaJson.GetProperty("spreadAaPercentual").GetDecimal();
        decimal iof = propostaJson.GetProperty("iofPercentual").GetDecimal();
        int prazo = propostaJson.GetProperty("prazoDias").GetInt32();

        Proposta proposta = CriarProposta(
            valorOferecidoUsd: propostaJson.GetProperty("valorOferecidoUsd").GetDecimal(),
            taxaAaPercentual: taxaAa,
            spreadAaPercentual: spreadAa,
            iofPercentual: iof,
            prazoDias: prazo,
            valorGarantiaExigidaBrl: propostaJson.GetProperty("valorGarantiaExigidaBrl").GetDecimal());

        decimal cet = CalculadoraCet.CalcularCet(proposta, ptax, DataDesembolso);
        decimal cdiDecimal = taxaCdi / 100m;
        decimal custoTotal = principalBrl * cet / 100m * prazo / 360m;
        decimal fator = 1m / (1m + cdiDecimal * prazo / 360m);
        return Math.Round(custoTotal * fator, 2, MidpointRounding.AwayFromZero);
    }
}
