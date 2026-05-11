using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

using FluentAssertions;

using NodaTime;
using NodaTime.Text;

using Sgcf.Domain.Calculo;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.GoldenDataset.Cronograma;

/// <summary>
/// Testes de regressão baseados nos dados de referência do Annex B §6.6.
/// Os arquivos JSON são autoritativos: nunca altere saida_esperada.json sem aprovação de negócio.
/// </summary>
[Trait("Category", "Golden")]
public sealed class Lei4131GoldenTests
{
    private static readonly string DataDir =
        Path.Combine(AppContext.BaseDirectory, "data", "4131-bb-anexo-b-6.6");

    /// <summary>Converte string ISO 8601 (yyyy-MM-dd) para LocalDate sem depender de localidade.</summary>
    private static LocalDate ParseLocalDate(string iso8601) =>
        LocalDatePattern.Iso.Parse(iso8601).GetValueOrThrow();

    // ── Teste 1: cronograma SAC bate exatamente com saida_esperada.json ───────

    [Fact]
    public void SacSchedule_4131BbAnexoB66_MatchesGoldenDataset()
    {
        // Arrange — lê os dados de entrada do JSON
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement entradaRoot = entradaDoc.RootElement;
        JsonElement saidaRoot = saidaDoc.RootElement;

        decimal valorPrincipal = entradaRoot.GetProperty("valor_principal_usd").GetDecimal();
        decimal taxaAaPct = entradaRoot.GetProperty("taxa_aa_pct").GetDecimal();
        string baseCalculoStr = entradaRoot.GetProperty("base_calculo").GetString()!;
        LocalDate dataDesembolso = ParseLocalDate(entradaRoot.GetProperty("data_desembolso").GetString()!);
        LocalDate dataVencimento = ParseLocalDate(entradaRoot.GetProperty("data_vencimento").GetString()!);
        int numeroParcelas = entradaRoot.GetProperty("numero_parcelas").GetInt32();

        BaseCalculo baseCalculo = Enum.Parse<BaseCalculo>(baseCalculoStr, ignoreCase: true);

        EntradaSac entrada = new(
            ValorPrincipal: new Money(valorPrincipal, Moeda.Usd),
            TaxaAa: Percentual.De(taxaAaPct),
            BaseCalculo: baseCalculo,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            NumeroParcelas: numeroParcelas,
            AliqIrrf: null);

        // Act
        IReadOnlyList<EventoGeradoSac> eventos = SacStrategy.Gerar(entrada);

        // Assert — compara com o cronograma esperado no JSON
        JsonElement cronogramaEsperado = saidaRoot.GetProperty("cronograma");
        JsonElement[] eventosEsperados = cronogramaEsperado.EnumerateArray().ToArray();

        eventos.Should().HaveCount(eventosEsperados.Length,
            because: "o cronograma SAC deve ter exatamente o número de eventos do golden dataset");

        for (int idx = 0; idx < eventosEsperados.Length; idx++)
        {
            JsonElement esperado = eventosEsperados[idx];
            EventoGeradoSac gerado = eventos[idx];

            int numeroParcela = esperado.GetProperty("numero_parcela").GetInt32();
            string tipo = esperado.GetProperty("tipo").GetString()!;
            LocalDate dataPrevista = ParseLocalDate(esperado.GetProperty("data_prevista").GetString()!);
            decimal valorUsd = esperado.GetProperty("valor_usd").GetDecimal();
            decimal? saldoApos = esperado.GetProperty("saldo_devedor_apos").ValueKind == JsonValueKind.Null
                ? (decimal?)null
                : esperado.GetProperty("saldo_devedor_apos").GetDecimal();

            gerado.NumeroParcela.Should().Be(numeroParcela, because: $"evento[{idx}] numero_parcela diverge");
            gerado.Tipo.ToString().Should().Be(tipo, because: $"evento[{idx}] tipo diverge");
            gerado.DataPrevista.Should().Be(dataPrevista, because: $"evento[{idx}] data_prevista diverge");
            gerado.Valor.Valor.Should().Be(valorUsd, because: $"evento[{idx}] valor diverge");
            gerado.SaldoDevedorApos.Should().Be(saldoApos, because: $"evento[{idx}] saldo_devedor_apos diverge");
        }
    }

    // ── Teste 2: saldo provisionado 60 dias após o desembolso ─────────────────

    [Fact]
    public void SaldoProvisionado_Em60Dias_MatchesGoldenDataset()
    {
        // Arrange
        string entradaJson = File.ReadAllText(Path.Combine(DataDir, "entrada.json"));
        string saidaJson = File.ReadAllText(Path.Combine(DataDir, "saida_esperada.json"));

        using JsonDocument entradaDoc = JsonDocument.Parse(entradaJson);
        using JsonDocument saidaDoc = JsonDocument.Parse(saidaJson);

        JsonElement entradaRoot = entradaDoc.RootElement;
        JsonElement saidaRoot = saidaDoc.RootElement;

        decimal valorPrincipal = entradaRoot.GetProperty("valor_principal_usd").GetDecimal();
        decimal taxaAaPct = entradaRoot.GetProperty("taxa_aa_pct").GetDecimal();
        string baseCalculoStr = entradaRoot.GetProperty("base_calculo").GetString()!;
        LocalDate dataDesembolso = ParseLocalDate(entradaRoot.GetProperty("data_desembolso").GetString()!);
        LocalDate dataVencimento = ParseLocalDate(entradaRoot.GetProperty("data_vencimento").GetString()!);
        int numeroParcelas = entradaRoot.GetProperty("numero_parcelas").GetInt32();

        BaseCalculo baseCalculo = Enum.Parse<BaseCalculo>(baseCalculoStr, ignoreCase: true);

        JsonElement consultaElem = entradaRoot.GetProperty("consulta");
        LocalDate dataReferencia = ParseLocalDate(consultaElem.GetProperty("data_referencia").GetString()!);

        JsonElement saldoEsperadoElem = saidaRoot.GetProperty("saldo_em_consulta");
        decimal saldoPrincipalEsperado = saldoEsperadoElem.GetProperty("saldo_principal_aberto_usd").GetDecimal();
        decimal jurosProvisionadosEsperado = saldoEsperadoElem.GetProperty("juros_provisionados_usd").GetDecimal();

        // Gera o cronograma para montar os EventoSaldoItem (todos Previsto — nenhum pago ainda)
        EntradaSac entradaSac = new(
            ValorPrincipal: new Money(valorPrincipal, Moeda.Usd),
            TaxaAa: Percentual.De(taxaAaPct),
            BaseCalculo: baseCalculo,
            DataDesembolso: dataDesembolso,
            DataVencimento: dataVencimento,
            NumeroParcelas: numeroParcelas,
            AliqIrrf: null);

        IReadOnlyList<EventoGeradoSac> eventosGerados = SacStrategy.Gerar(entradaSac);

        List<EventoSaldoItem> eventosSaldo = new(eventosGerados.Count);
        foreach (EventoGeradoSac e in eventosGerados)
        {
            eventosSaldo.Add(new EventoSaldoItem(e.Tipo, StatusEventoCronograma.Previsto, e.DataPrevista, e.Valor));
        }

        EntradaCalculoSaldo entradaSaldo = new(
            ValorPrincipalInicial: new Money(valorPrincipal, Moeda.Usd),
            TaxaAa: Percentual.De(taxaAaPct),
            BaseCalculo: baseCalculo,
            DataDesembolso: dataDesembolso,
            DataReferencia: dataReferencia,
            Eventos: eventosSaldo.AsReadOnly());

        // Act
        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entradaSaldo);

        // Assert
        resultado.SaldoPrincipalAberto.Valor.Should().Be(saldoPrincipalEsperado);
        resultado.JurosProvisionados.Valor.Should().Be(jurosProvisionadosEsperado);
    }
}
