using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Gerador de cronograma Price (amortização francesa — parcelas totais iguais).
/// Implementa a fórmula PMT = PV × i / (1 − (1+i)^−n).
/// Funções puras — sem I/O, sem efeitos colaterais.
/// </summary>
public static class PriceStrategy
{
    /// <summary>
    /// Gera a lista de eventos financeiros para um contrato com amortização Price.
    /// </summary>
    /// <remarks>
    /// Regras:
    /// <list type="bullet">
    ///   <item>PMT = PV × i / (1 − (1+i)^−n) — parcela total constante (arredondada 2 dp HalfUp).</item>
    ///   <item>Juros[k] = SaldoAnterior[k] × i (arredondado 2 dp HalfUp).</item>
    ///   <item>Principal[k] = PMT − Juros[k] (exceto na última parcela — ver abaixo).</item>
    ///   <item>Na última parcela: Principal[n] = SaldoAnterior[n] exato (elimina drift de arredondamento).</item>
    ///   <item>IRRF gross-up = valorJuros × aliq / (1 − aliq) quando AliqIrrf informada.</item>
    ///   <item>Datas mensais a partir de DataPrimeiroVencimento (sem ajuste de dia útil).</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<EventoGeradoPrice> Gerar(EntradaPrice entrada)
    {
        ValidarEntrada(entrada);

        decimal pv = entrada.ValorPrincipal.Valor;
        decimal i = entrada.TaxaMensal.AsDecimal;
        int n = entrada.NumeroParcelas;
        Moeda moeda = entrada.ValorPrincipal.Moeda;

        decimal pmt = CalcularPmt(pv, i, n);

        List<EventoGeradoPrice> eventos = new(n * 3);
        decimal saldo = pv;

        for (int k = 1; k <= n; k++)
        {
            LocalDate dataPrevista = entrada.DataPrimeiroVencimento.PlusMonths(k - 1);

            decimal juros = Math.Round(saldo * i, 2, MidpointRounding.AwayFromZero);

            decimal principal;
            decimal saldoApos;

            if (k == n)
            {
                // Absorve o drift acumulado de arredondamento na última parcela.
                principal = saldo;
                saldoApos = 0m;
            }
            else
            {
                principal = pmt - juros;
                saldoApos = saldo - principal;
            }

            eventos.Add(new EventoGeradoPrice(
                NumeroParcela: k,
                Tipo: TipoEventoCronograma.Juros,
                DataPrevista: dataPrevista,
                Valor: new Money(juros, moeda),
                SaldoDevedorApos: null));

            eventos.Add(new EventoGeradoPrice(
                NumeroParcela: k,
                Tipo: TipoEventoCronograma.Principal,
                DataPrevista: dataPrevista,
                Valor: new Money(principal, moeda),
                SaldoDevedorApos: saldoApos));

            if (entrada.AliqIrrf.HasValue)
            {
                decimal aliqIrrf = entrada.AliqIrrf.Value.AsDecimal;
                decimal valorIrrf = Math.Round(
                    juros * aliqIrrf / (1m - aliqIrrf),
                    2,
                    MidpointRounding.AwayFromZero);

                eventos.Add(new EventoGeradoPrice(
                    NumeroParcela: k,
                    Tipo: TipoEventoCronograma.IrrfRetido,
                    DataPrevista: dataPrevista,
                    Valor: new Money(valorIrrf, moeda),
                    SaldoDevedorApos: null));
            }

            saldo = saldoApos;
        }

        return eventos.AsReadOnly();
    }

    /// <summary>
    /// Fórmula PMT = PV × i / (1 − (1+i)^−n).
    /// Quando i = 0 o contrato é sem juros: PMT = PV / n.
    /// </summary>
    private static decimal CalcularPmt(decimal pv, decimal i, int n)
    {
        if (i == 0m)
        {
            return Math.Round(pv / n, 2, MidpointRounding.AwayFromZero);
        }

        // (1+i)^n via double para precisão adequada; o resultado final é arredondado 2 dp.
        double fatorCompostoInverso = Math.Pow((double)(1m + i), -n);
        decimal denominador = 1m - (decimal)fatorCompostoInverso;

        return Math.Round(pv * i / denominador, 2, MidpointRounding.AwayFromZero);
    }

    private static void ValidarEntrada(EntradaPrice entrada)
    {
        if (entrada.NumeroParcelas <= 0)
        {
            throw new ArgumentException(
                "NumeroParcelas deve ser maior que zero.",
                nameof(entrada));
        }

        if (entrada.ValorPrincipal.Valor <= 0m)
        {
            throw new ArgumentException(
                "ValorPrincipal.Valor deve ser maior que zero.",
                nameof(entrada));
        }

        if (entrada.TaxaMensal.AsDecimal < 0m)
        {
            throw new ArgumentException(
                "TaxaMensal não pode ser negativa.",
                nameof(entrada));
        }

        if (entrada.DataPrimeiroVencimento <= entrada.DataDesembolso)
        {
            throw new ArgumentException(
                "DataPrimeiroVencimento deve ser posterior a DataDesembolso.",
                nameof(entrada));
        }
    }
}
