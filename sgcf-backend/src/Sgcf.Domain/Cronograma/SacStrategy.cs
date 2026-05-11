using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Gerador de cronograma SAC (Sistema de Amortização Constante).
/// Implementa Annex B §6.6 — parcelas com amortização constante e juros decrescentes.
/// Funções puras — sem I/O, sem efeitos colaterais.
/// </summary>
public static class SacStrategy
{
    private const int DefaultNumeroParcelas = 4;

    /// <summary>
    /// Gera a lista de eventos financeiros para um contrato com amortização SAC.
    /// </summary>
    /// <remarks>
    /// Regras:
    /// <list type="bullet">
    ///   <item>AmortizacaoFixa = ValorPrincipal / NumeroParcelas (constante em todas as parcelas).</item>
    ///   <item>SaldoAbertura[i] = ValorPrincipal - (i-1) × AmortizacaoFixa.</item>
    ///   <item>Juros[i] = SaldoAbertura[i] × TaxaAa × DiasPeriodo[i] / BaseCalculo.</item>
    ///   <item>Períodos distribuídos uniformemente entre DataDesembolso e DataVencimento.</item>
    ///   <item>Sem IOF câmbio (Lei 4.131 é empréstimo direto do exterior).</item>
    ///   <item>IRRF aplicado se AliqIrrf informado (gross-up: juros × aliq / (1 - aliq)).</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<EventoGeradoSac> Gerar(EntradaSac entrada)
    {
        ValidarEntrada(entrada);

        int totalDias = Period.Between(entrada.DataDesembolso, entrada.DataVencimento, PeriodUnits.Days).Days;
        decimal baseCalculo = (decimal)entrada.BaseCalculo;
        int n = entrada.NumeroParcelas;

        // AmortizacaoFixa = principal / n, arredondado para 2 casas HalfUp
        decimal amortizacaoFixa = Math.Round(
            entrada.ValorPrincipal.Valor / n,
            2,
            MidpointRounding.AwayFromZero);

        List<EventoGeradoSac> eventos = new(n * 3);

        for (int i = 1; i <= n; i++)
        {
            // Datas de início e fim do período i usando divisão inteira para espaçamento uniforme
            LocalDate dataInicioPeriodo = entrada.DataDesembolso.PlusDays(totalDias * (i - 1) / n);
            LocalDate dataFimPeriodo = entrada.DataDesembolso.PlusDays(totalDias * i / n);

            int diasPeriodo = Period.Between(dataInicioPeriodo, dataFimPeriodo, PeriodUnits.Days).Days;

            // SaldoAbertura[i] = ValorPrincipal - (i-1) × AmortizacaoFixa
            decimal saldoAbertura = entrada.ValorPrincipal.Valor - ((i - 1) * amortizacaoFixa);

            decimal valorJuros = Math.Round(
                saldoAbertura * entrada.TaxaAa.AsDecimal * diasPeriodo / baseCalculo,
                2,
                MidpointRounding.AwayFromZero);

            // SaldoDevedorApos = saldoAbertura - amortizacaoFixa (zero na última parcela)
            decimal saldoApos = i == n ? 0m : saldoAbertura - amortizacaoFixa;

            Money juros = new(valorJuros, entrada.ValorPrincipal.Moeda);
            Money amortizacao = new(amortizacaoFixa, entrada.ValorPrincipal.Moeda);

            eventos.Add(new EventoGeradoSac(
                NumeroParcela: i,
                Tipo: TipoEventoCronograma.Juros,
                DataPrevista: dataFimPeriodo,
                Valor: juros,
                SaldoDevedorApos: null));

            eventos.Add(new EventoGeradoSac(
                NumeroParcela: i,
                Tipo: TipoEventoCronograma.Principal,
                DataPrevista: dataFimPeriodo,
                Valor: amortizacao,
                SaldoDevedorApos: saldoApos));

            if (entrada.AliqIrrf.HasValue)
            {
                decimal aliqIrrf = entrada.AliqIrrf.Value.AsDecimal;
                decimal valorIrrf = Math.Round(
                    valorJuros * aliqIrrf / (1m - aliqIrrf),
                    2,
                    MidpointRounding.AwayFromZero);

                eventos.Add(new EventoGeradoSac(
                    NumeroParcela: i,
                    Tipo: TipoEventoCronograma.IrrfRetido,
                    DataPrevista: dataFimPeriodo,
                    Valor: new Money(valorIrrf, entrada.ValorPrincipal.Moeda),
                    SaldoDevedorApos: null));
            }
        }

        return eventos.AsReadOnly();
    }

    private static void ValidarEntrada(EntradaSac entrada)
    {
        if (entrada.DataVencimento <= entrada.DataDesembolso)
        {
            throw new ArgumentException(
                "DataVencimento deve ser posterior a DataDesembolso.",
                nameof(entrada));
        }

        if (entrada.ValorPrincipal.Valor <= 0m)
        {
            throw new ArgumentException(
                "ValorPrincipal.Valor deve ser maior que zero.",
                nameof(entrada));
        }

        if (entrada.TaxaAa.AsDecimal < 0m)
        {
            throw new ArgumentException(
                "TaxaAa não pode ser negativa.",
                nameof(entrada));
        }

        if (entrada.NumeroParcelas <= 0)
        {
            throw new ArgumentException(
                "NumeroParcelas deve ser maior que zero.",
                nameof(entrada));
        }
    }
}
