using System.Text.Json;

using FluentAssertions;

using NodaTime;
using NodaTime.Text;

using Sgcf.Domain.Common;
using Sgcf.Domain.Painel;

using Xunit;

namespace Sgcf.GoldenDataset.QuadroDivida;

/// <summary>
/// Testa golden do módulo Quadro da Dívida — 3 bancos, 4 amortizações, ano 2026.
/// Dados autoritativos em data/quadro-divida-2026/. Não altere output_esperado.json
/// sem aprovação de negócio. Referência: tasks/quadro-divida-simulacao/plan.md §7 Task 4.2.
/// Tolerância: R$ 1,00 (arredondamento de centavos acumulado — irrelevante com valores inteiros).
/// </summary>
[Trait("Category", "Golden")]
public sealed class QuadroDivida2026GoldenTest
{
    private const decimal Tolerancia = 1.00m;

    private static readonly string DataDir =
        Path.Combine(AppContext.BaseDirectory, "data", "quadro-divida-2026");

    // ── Helper de leitura ──────────────────────────────────────────────────

    private static JsonElement LerJson(string arquivo)
    {
        string caminho = Path.Combine(DataDir, arquivo);
        string conteudo = File.ReadAllText(caminho);
        return JsonDocument.Parse(conteudo).RootElement.Clone();
    }

    private static LocalDate ParseLocalDate(string iso8601) =>
        LocalDatePattern.Iso.Parse(iso8601).GetValueOrThrow();

    // ── Teste principal ────────────────────────────────────────────────────

    /// <summary>
    /// Carrega input.json e output_esperado.json, executa ProjetorSaldoMensal.Projetar()
    /// e compara saldo final do ano e saldo de fechamento de cada um dos 12 meses
    /// contra os valores autoritativos do golden dataset.
    /// </summary>
    [Fact]
    public void Projetar_QuadroDivida2026_BateComGoldenDataset()
    {
        // Arrange — leitura dos datasets
        JsonElement input   = LerJson("input.json");
        JsonElement esperado = LerJson("output_esperado.json");

        Dictionary<Guid, Money> saldoInicial = ConstruirSaldoInicial(input);
        List<EventoProjecao>    eventos       = ConstruirEventos(input);
        int ano = input.GetProperty("ano").GetInt32();

        // Act
        QuadroDividaProjecao projecao = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, ano);

        // Assert — saldo final do ano (dezembro)
        decimal saldoFimAnoEsperado = esperado
            .GetProperty("sumarioAnual")
            .GetProperty("saldoFimAno")
            .GetDecimal();

        projecao.Meses[11].SaldoTotalFim.Valor
            .Should().BeApproximately(saldoFimAnoEsperado, Tolerancia,
                because: "saldo de fechamento de dezembro/2026 deve bater com o golden");

        // Assert — saldo total de fechamento mês a mês (12 asserções independentes)
        JsonElement mesesEsperados = esperado.GetProperty("meses");

        for (int m = 0; m < 12; m++)
        {
            decimal saldoFimMesEsperado = mesesEsperados[m]
                .GetProperty("saldoTotalFim")
                .GetDecimal();

            projecao.Meses[m].SaldoTotalFim.Valor
                .Should().BeApproximately(saldoFimMesEsperado, Tolerancia,
                    because: $"saldo total de fechamento do mês {m + 1}/2026 deve bater com o golden");
        }
    }

    /// <summary>
    /// Valida invariante estrutural: SaldoTotalFim[m] == SaldoTotalInicio[m+1]
    /// para todos os meses exceto dezembro (P-1 do ProjetorSaldoMensal).
    /// </summary>
    [Fact]
    public void Projetar_QuadroDivida2026_InvarianteCarryForwardEntreM_Meses()
    {
        // Arrange
        JsonElement input = LerJson("input.json");

        Dictionary<Guid, Money> saldoInicial = ConstruirSaldoInicial(input);
        List<EventoProjecao>    eventos       = ConstruirEventos(input);
        int ano = input.GetProperty("ano").GetInt32();

        // Act
        QuadroDividaProjecao projecao = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, ano);

        // Assert — P-1: SaldoFim[m] == SaldoInicio[m+1]
        for (int m = 0; m < 11; m++)
        {
            decimal saldoFimMesAnterior  = projecao.Meses[m].SaldoTotalFim.Valor;
            decimal saldoInicioMesSeguinte = projecao.Meses[m + 1].SaldoTotalInicio.Valor;

            saldoFimMesAnterior.Should().Be(saldoInicioMesSeguinte,
                because: $"SaldoFim do mês {m + 1} deve ser exatamente SaldoInicio do mês {m + 2} (invariante P-1)");
        }
    }

    // ── Construtores de dados de entrada ──────────────────────────────────

    private static Dictionary<Guid, Money> ConstruirSaldoInicial(JsonElement input)
    {
        var resultado = new Dictionary<Guid, Money>();

        foreach (JsonElement banco in input.GetProperty("saldoInicialPorBanco").EnumerateArray())
        {
            Guid   id    = banco.GetProperty("bancoId").GetGuid();
            decimal valor = banco.GetProperty("saldoBrl").GetDecimal();
            resultado[id] = new Money(valor, Moeda.Brl);
        }

        return resultado;
    }

    private static List<EventoProjecao> ConstruirEventos(JsonElement input)
    {
        var resultado = new List<EventoProjecao>();

        foreach (JsonElement e in input.GetProperty("eventos").EnumerateArray())
        {
            resultado.Add(new EventoProjecao(
                BancoId:   e.GetProperty("bancoId").GetGuid(),
                Data:      ParseLocalDate(e.GetProperty("data").GetString()!),
                Tipo:      Enum.Parse<TipoEventoProjecao>(e.GetProperty("tipo").GetString()!),
                ValorBrl:  new Money(e.GetProperty("valorBrl").GetDecimal(), Moeda.Brl)));
        }

        return resultado;
    }
}
