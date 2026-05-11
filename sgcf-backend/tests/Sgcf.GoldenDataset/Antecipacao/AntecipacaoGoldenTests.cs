using System.IO;
using System.Text.Json;
using FluentAssertions;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Antecipacao.Strategies;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.GoldenDataset.Antecipacao;


/// <summary>
/// Testes de regressão baseados nos dados de referência do Annexo C.
/// Os arquivos JSON são autoritativos: nunca altere saida_esperada.json sem aprovação de negócio.
/// </summary>
[Trait("Category", "Golden")]
public sealed class AntecipacaoGoldenTests
{
    private static readonly string DataDir =
        Path.Combine(AppContext.BaseDirectory, "data");

    // ── Golden 1: Padrão B (Sicredi) — Annexo C §7.2 ──────────────────────────

    [Fact]
    public void PadraoB_Sicredi_MatchesGoldenDataset()
    {
        // Arrange — lê os dados de entrada e saída esperada do JSON
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-b-sicredi", "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-b-sicredi", "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement e = entradaDoc.RootElement;
        JsonElement s = saidaDoc.RootElement;

        decimal principalValor = e.GetProperty("principalAQuitarValor").GetDecimal();
        Moeda moeda = Enum.Parse<Moeda>(e.GetProperty("principalAQuitarMoeda").GetString()!, ignoreCase: true);
        decimal taxaAaPct = e.GetProperty("taxaAaPct").GetDecimal();
        int baseCalculo = e.GetProperty("baseCalculo").GetInt32();
        int prazoTotalOriginalDias = e.GetProperty("prazoTotalOriginalDias").GetInt32();
        int prazoRemanescenteDias = e.GetProperty("prazoRemanescenteDias").GetInt32();
        int prazoRemanescenteMeses = e.GetProperty("prazoRemanescenteMeses").GetInt32();
        bool exigeAnuenciaExpressa = e.GetProperty("exigeAnuenciaExpressa").GetBoolean();

        EntradaSimulacaoAntecipacao entrada = new(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: null,
            PrazoTotalOriginalDias: prazoTotalOriginalDias,
            PrazoRemanescenteDias: prazoRemanescenteDias,
            PrazoRemanescenteMeses: prazoRemanescenteMeses,
            TaxaAa: Percentual.De(taxaAaPct),
            BaseCalculo: baseCalculo,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoBStrategy.Calcular(entrada, exigeAnuenciaExpressa);

        // Assert — golden dataset values
        decimal totalEsperado = s.GetProperty("totalAQuitarValor").GetDecimal();
        decimal c1Esperado = s.GetProperty("componenteC1ValorMoedaOriginal").GetDecimal();
        decimal c3Esperado = s.GetProperty("componenteC3ValorMoedaOriginal").GetDecimal();
        string alertaContains = s.GetProperty("alertaContains").GetString()!;
        bool permitidoEsperado = s.GetProperty("permitido").GetBoolean();
        bool exigeAnuenciaEsperado = s.GetProperty("exigeAnuenciaExpressa").GetBoolean();

        resultado.Permitido.Should().Be(permitidoEsperado);
        resultado.ExigeAnuenciaExpressa.Should().Be(exigeAnuenciaEsperado);
        resultado.TotalAQuitar.Valor.Should().Be(totalEsperado,
            because: "o total calculado deve bater exatamente com o golden dataset §7.2");

        ComponenteCusto c1 = resultado.Componentes.First(c => c.Codigo == "C1");
        c1.Valor.Valor.Should().Be(c1Esperado, because: "C1 (principal) deve bater com o golden");

        ComponenteCusto c3 = resultado.Componentes.First(c => c.Codigo == "C3");
        c3.Valor.Valor.Should().Be(c3Esperado, because: "C3 (juros período total) deve bater com o golden");

        string alertaUnido = string.Concat(resultado.Alertas);
        alertaUnido.Should().Contain(alertaContains,
            because: "o alerta crítico do Sicredi deve estar presente");
    }

    // ── Golden 2: Padrão D (Caixa) — Annexo C §7.4 ───────────────────────────

    [Fact]
    public void PadraoD_Caixa_MatchesGoldenDataset()
    {
        // Arrange
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-d-caixa", "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-d-caixa", "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement e = entradaDoc.RootElement;
        JsonElement s = saidaDoc.RootElement;

        decimal principalValor = e.GetProperty("principalAQuitarValor").GetDecimal();
        Moeda moeda = Enum.Parse<Moeda>(e.GetProperty("principalAQuitarMoeda").GetString()!, ignoreCase: true);
        decimal jurosValor = e.GetProperty("jurosProRataValor").GetDecimal();
        int prazoRemanescenteMeses = e.GetProperty("prazoRemanescenteMeses").GetInt32();
        decimal tlaSaldo = e.GetProperty("tlaPctSobreSaldo").GetDecimal();
        decimal tlaMes = e.GetProperty("tlaPctPorMesRemanescente").GetDecimal();
        bool isRefinanciamento = e.GetProperty("origemRefinanciamentoInterno").GetBoolean();

        EntradaSimulacaoAntecipacao entrada = new(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: new Money(jurosValor, moeda),
            PrazoTotalOriginalDias: 720,
            PrazoRemanescenteDias: prazoRemanescenteMeses * 30,
            PrazoRemanescenteMeses: prazoRemanescenteMeses,
            TaxaAa: Percentual.De(12m),
            BaseCalculo: 360,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: isRefinanciamento);

        Percentual tlaPctSobreSaldo = Percentual.DeFracao(tlaSaldo);
        Percentual tlaPctPorMes = Percentual.DeFracao(tlaMes);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoDStrategy.Calcular(entrada, tlaPctSobreSaldo, tlaPctPorMes);

        // Assert — golden values
        decimal vtdEsperado = s.GetProperty("vtdBrl").GetDecimal();
        decimal tlaEsperado = s.GetProperty("tlaAplicadaBrl").GetDecimal();
        decimal totalEsperado = s.GetProperty("totalAQuitarBrl").GetDecimal();
        bool isencaoEsperada = s.GetProperty("isencaoAplicada").GetBoolean();

        // VTD = principal + juros pro rata
        decimal vtdCalculado = resultado.Componentes
            .Where(c => c.Sinal == "+")
            .Sum(c => c.Valor.Valor);

        vtdCalculado.Should().Be(vtdEsperado + tlaEsperado,
            because: "VTD + TLA deve compor o total a quitar");

        ComponenteCusto tlaComponente = resultado.Componentes.First(c => c.Codigo == "TLA");
        tlaComponente.Valor.Valor.Should().Be(tlaEsperado,
            because: "TLA deve bater com o golden dataset §7.4");

        resultado.TotalAQuitar.Valor.Should().Be(totalEsperado,
            because: "o total calculado deve bater exatamente com o golden dataset §7.4");

        bool isencaoAplicada = resultado.Alertas.Any(a =>
            a.Contains("isenção", StringComparison.OrdinalIgnoreCase)
            || a.Contains("refinanciamento", StringComparison.OrdinalIgnoreCase));
        isencaoAplicada.Should().Be(isencaoEsperada,
            because: "isenção de TLA não deve ser aplicada neste cenário");
    }

    // ── Golden 3: Padrão A (BB FINIMP) — BFF sobre base antecipada ───────────

    [Fact]
    public void PadraoA_BbFinimp_MatchesGoldenDataset()
    {
        // Arrange
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-a-bb-finimp", "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-a-bb-finimp", "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement e = entradaDoc.RootElement;
        JsonElement s = saidaDoc.RootElement;

        decimal principalValor = e.GetProperty("principalAQuitarValor").GetDecimal();
        Moeda moeda = Enum.Parse<Moeda>(e.GetProperty("principalAQuitarMoeda").GetString()!, ignoreCase: true);
        decimal jurosValor = e.GetProperty("jurosProRataValor").GetDecimal();
        decimal breakFundingFeePct = e.GetProperty("breakFundingFeePct").GetDecimal();
        bool exigeAnuenciaExpressa = e.GetProperty("exigeAnuenciaExpressa").GetBoolean();

        EntradaSimulacaoAntecipacao entrada = new(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: new Money(jurosValor, moeda),
            PrazoTotalOriginalDias: 365,
            PrazoRemanescenteDias: 180,
            PrazoRemanescenteMeses: 6,
            TaxaAa: Percentual.De(5m),
            BaseCalculo: 360,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoAStrategy.Calcular(
            entrada,
            Percentual.DeFracao(breakFundingFeePct),
            exigeAnuenciaExpressa);

        // Assert — golden values
        // BFF = (200000 + 4900) × 0.01 = 2049; TOTAL = 206949
        decimal totalEsperado = s.GetProperty("totalAQuitarValor").GetDecimal();
        decimal c1Esperado = s.GetProperty("componenteC1Valor").GetDecimal();
        decimal c2Esperado = s.GetProperty("componenteC2Valor").GetDecimal();
        decimal c6Esperado = s.GetProperty("componenteC6Valor").GetDecimal();
        int alertasCountEsperado = s.GetProperty("alertasCount").GetInt32();

        resultado.TotalAQuitar.Valor.Should().Be(totalEsperado,
            because: "BFF = (C1+C2)×pct; TOTAL = C1+C2+C6 deve ser exatamente 206949");

        ComponenteCusto c1 = resultado.Componentes.First(c => c.Codigo == "C1");
        c1.Valor.Valor.Should().Be(c1Esperado, because: "C1 (principal) deve ser 200000");

        ComponenteCusto c2 = resultado.Componentes.First(c => c.Codigo == "C2");
        c2.Valor.Valor.Should().Be(c2Esperado, because: "C2 (juros pro rata) deve ser 4900");

        ComponenteCusto c6 = resultado.Componentes.First(c => c.Codigo == "C6");
        c6.Valor.Valor.Should().Be(c6Esperado, because: "C6 (BFF) deve ser 2049");

        resultado.Alertas.Should().HaveCount(alertasCountEsperado,
            because: "Padrão A sem anuência expressa não gera alertas de estratégia");
    }

    // ── Golden 4: Padrão C (FGI/BV) — Marcação a mercado ────────────────────

    [Fact]
    public void PadraoC_FgiBv_MatchesGoldenDataset()
    {
        // Arrange
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-c-fgi-bv", "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-c-fgi-bv", "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement e = entradaDoc.RootElement;
        JsonElement s = saidaDoc.RootElement;

        decimal principalValor = e.GetProperty("principalAQuitarValor").GetDecimal();
        Moeda moeda = Enum.Parse<Moeda>(e.GetProperty("principalAQuitarMoeda").GetString()!, ignoreCase: true);
        decimal taxaContratadaAaPct = e.GetProperty("taxaContratadaAaPct").GetDecimal();
        decimal taxaMercadoAtualAaPct = e.GetProperty("taxaMercadoAtualAaPct").GetDecimal();
        int prazoRemanescenteDias = e.GetProperty("prazoRemanescenteDias").GetInt32();
        int baseCalculo = e.GetProperty("baseCalculo").GetInt32();

        EntradaSimulacaoAntecipacao entrada = new(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: null,
            PrazoTotalOriginalDias: prazoRemanescenteDias,
            PrazoRemanescenteDias: prazoRemanescenteDias,
            PrazoRemanescenteMeses: (int)Math.Ceiling(prazoRemanescenteDias / 30.0),
            TaxaAa: Percentual.De(taxaContratadaAaPct),
            BaseCalculo: baseCalculo,
            TaxaMercadoAtualAa: Percentual.De(taxaMercadoAtualAaPct),
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoCStrategy.Calcular(entrada, exigeAnuenciaExpressa: false);

        // Assert — compute expected inline because floating-point exponentiation is involved
        // Fórmula: TOTAL = principal × (1 + taxa_contratada)^(prazo/base) / (1 + taxa_mercado)^(prazo/base)
        decimal tolerancia = s.GetProperty("totalAQuitarTolerancia").GetDecimal();
        decimal taxaContratadaFracao = Percentual.De(taxaContratadaAaPct).AsDecimal;
        decimal taxaMercadoFracao = Percentual.De(taxaMercadoAtualAaPct).AsDecimal;
        double exponent = (double)prazoRemanescenteDias / (double)baseCalculo;
        double fator = Math.Pow(1.0 + (double)taxaContratadaFracao, exponent)
                     / Math.Pow(1.0 + (double)taxaMercadoFracao, exponent);
        decimal totalEsperado = Math.Round(principalValor * (decimal)fator, 6, MidpointRounding.AwayFromZero);

        resultado.TotalAQuitar.Valor.Should().BeApproximately(totalEsperado, tolerancia,
            because: "MTM deve aplicar fator de desconto/prêmio pela relação entre taxa contratada e mercado");

        // Taxa mercado < taxa contratada → antecipação desfavorável → alerta de prêmio
        string alertaUnido = string.Concat(resultado.Alertas);
        alertaUnido.Should().Contain("desfavorável",
            because: "taxa mercado 14% < taxa contratada 16.24% gera antecipação com prêmio");
    }

    // ── Golden 5: Padrão E (Caixa Pré-fixado) — Abatimento de juros embutidos ─

    [Fact]
    public void PadraoE_CaixaPrefixado_MatchesGoldenDataset()
    {
        // Arrange
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-e-caixa-prefixado", "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "antecipacao-padrao-e-caixa-prefixado", "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement e = entradaDoc.RootElement;
        JsonElement s = saidaDoc.RootElement;

        decimal principalValor = e.GetProperty("principalAQuitarValor").GetDecimal();
        Moeda moeda = Enum.Parse<Moeda>(e.GetProperty("principalAQuitarMoeda").GetString()!, ignoreCase: true);
        decimal jurosEmbebidosValor = e.GetProperty("jurosEmbebidosFuturosValor").GetDecimal();
        Moeda jurosEmbebidosMoeda = Enum.Parse<Moeda>(e.GetProperty("jurosEmbebidosFuturosMoeda").GetString()!, ignoreCase: true);

        EntradaSimulacaoAntecipacao entrada = new(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: new Money(principalValor, moeda),
            JurosProRata: null,
            PrazoTotalOriginalDias: 365,
            PrazoRemanescenteDias: 365,
            PrazoRemanescenteMeses: 12,
            TaxaAa: Percentual.De(5m),
            BaseCalculo: 360,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);

        Money jurosEmbebidosFuturos = new(jurosEmbebidosValor, jurosEmbebidosMoeda);

        // Act
        ResultadoSimulacaoAntecipacao resultado = PadraoEStrategy.Calcular(entrada, jurosEmbebidosFuturos);

        // Assert — golden values
        // TOTAL = 1000000 − 50000 = 950000
        decimal totalEsperado = s.GetProperty("totalAQuitarValor").GetDecimal();
        string moedaEsperada = s.GetProperty("totalAQuitarMoeda").GetString()!;

        resultado.TotalAQuitar.Valor.Should().Be(totalEsperado,
            because: "abatimento de 50000 sobre principal de 1000000 = 950000");
        resultado.TotalAQuitar.Moeda.ToString().Should().Be(moedaEsperada,
            because: "moeda deve ser preservada como BRL");

        ComponenteCusto abat = resultado.Componentes.First(c => c.Codigo == "ABAT");
        abat.Valor.Valor.Should().Be(jurosEmbebidosValor,
            because: "abatimento deve refletir exatamente os juros embutidos informados");
        abat.Sinal.Should().Be("-", because: "abatimento é dedução do principal");
    }
}
