using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Gerador de cronograma bullet com juros periódicos.
/// Principal pago no vencimento; juros pagos a cada <c>PeriodoJurosMeses</c> meses.
/// Funções puras — sem I/O, sem efeitos colaterais.
/// </summary>
public static class BulletComJurosPeriodicosStrategy
{
    /// <summary>
    /// Gera a lista de eventos financeiros para um contrato bullet com cupons de juros periódicos.
    /// </summary>
    public static IReadOnlyList<EventoGeradoBulletComJuros> Gerar(EntradaBulletComJuros entrada)
    {
        ValidarEntrada(entrada);

        List<EventoGeradoBulletComJuros> eventos = [];
        decimal baseCalculo = (decimal)entrada.BaseCalculo;
        Moeda moeda = entrada.ValorPrincipal.Moeda;

        // ── Eventos de dia 0 ────────────────────────────────────────────────────

        if (entrada.AliqIofCambio.HasValue)
        {
            decimal valorIof = Math.Round(
                entrada.ValorPrincipal.Valor * entrada.AliqIofCambio.Value.AsDecimal,
                2,
                MidpointRounding.AwayFromZero);

            eventos.Add(new EventoGeradoBulletComJuros(
                NumeroEvento: 0,
                Tipo: TipoEventoCronograma.IofCambio,
                DataPrevista: entrada.DataDesembolso,
                Valor: new Money(valorIof, moeda),
                SaldoDevedorApos: null));
        }

        if (entrada.TarifaRofBrl.HasValue)
        {
            eventos.Add(new EventoGeradoBulletComJuros(
                NumeroEvento: 0,
                Tipo: TipoEventoCronograma.TarifaRof,
                DataPrevista: entrada.DataDesembolso,
                Valor: new Money(entrada.TarifaRofBrl.Value, Moeda.Brl),
                SaldoDevedorApos: null));
        }

        if (entrada.TarifaCadempBrl.HasValue)
        {
            eventos.Add(new EventoGeradoBulletComJuros(
                NumeroEvento: 0,
                Tipo: TipoEventoCronograma.TarifaCademp,
                DataPrevista: entrada.DataDesembolso,
                Valor: new Money(entrada.TarifaCadempBrl.Value, Moeda.Brl),
                SaldoDevedorApos: null));
        }

        // ── Coupons intermediários e evento final ────────────────────────────────

        // Build the list of coupon dates: one per PeriodoJurosMeses, excluding DataVencimento
        List<LocalDate> datasCoupon = [];
        int k = 1;
        while (true)
        {
            LocalDate dataCoupon = entrada.DataDesembolso.PlusMonths(k * entrada.PeriodoJurosMeses);
            if (dataCoupon >= entrada.DataVencimento)
            {
                break;
            }
            datasCoupon.Add(dataCoupon);
            k++;
        }

        int totalCoupons = datasCoupon.Count;
        // Intermediate coupons numbered 1..totalCoupons; final set numbered totalCoupons+1
        int numeroFinal = totalCoupons + 1;

        LocalDate dataInicioAnterior = entrada.DataDesembolso;

        for (int i = 0; i < totalCoupons; i++)
        {
            LocalDate dataCoupon = datasCoupon[i];
            int numeroEvento = i + 1;

            int diasPeriodo = Period.Between(dataInicioAnterior, dataCoupon, PeriodUnits.Days).Days;

            decimal valorJuros = Math.Round(
                entrada.ValorPrincipal.Valor * entrada.TaxaAa.AsDecimal * diasPeriodo / baseCalculo,
                2,
                MidpointRounding.AwayFromZero);

            eventos.Add(new EventoGeradoBulletComJuros(
                NumeroEvento: numeroEvento,
                Tipo: TipoEventoCronograma.Juros,
                DataPrevista: dataCoupon,
                Valor: new Money(valorJuros, moeda),
                SaldoDevedorApos: null));

            if (entrada.AliqIrrf.HasValue)
            {
                decimal aliqIrrf = entrada.AliqIrrf.Value.AsDecimal;
                decimal valorIrrf = Math.Round(
                    valorJuros * aliqIrrf / (1m - aliqIrrf),
                    2,
                    MidpointRounding.AwayFromZero);

                eventos.Add(new EventoGeradoBulletComJuros(
                    NumeroEvento: numeroEvento,
                    Tipo: TipoEventoCronograma.IrrfRetido,
                    DataPrevista: dataCoupon,
                    Valor: new Money(valorIrrf, moeda),
                    SaldoDevedorApos: null));
            }

            dataInicioAnterior = dataCoupon;
        }

        // ── Evento final no vencimento ───────────────────────────────────────────

        int diasUltimoPeriodo = Period.Between(dataInicioAnterior, entrada.DataVencimento, PeriodUnits.Days).Days;

        decimal valorJurosFinal = Math.Round(
            entrada.ValorPrincipal.Valor * entrada.TaxaAa.AsDecimal * diasUltimoPeriodo / baseCalculo,
            2,
            MidpointRounding.AwayFromZero);

        eventos.Add(new EventoGeradoBulletComJuros(
            NumeroEvento: numeroFinal,
            Tipo: TipoEventoCronograma.Juros,
            DataPrevista: entrada.DataVencimento,
            Valor: new Money(valorJurosFinal, moeda),
            SaldoDevedorApos: null));

        eventos.Add(new EventoGeradoBulletComJuros(
            NumeroEvento: numeroFinal,
            Tipo: TipoEventoCronograma.Principal,
            DataPrevista: entrada.DataVencimento,
            Valor: entrada.ValorPrincipal,
            SaldoDevedorApos: 0m));

        if (entrada.AliqIrrf.HasValue)
        {
            decimal aliqIrrfFinal = entrada.AliqIrrf.Value.AsDecimal;
            decimal valorIrrfFinal = Math.Round(
                valorJurosFinal * aliqIrrfFinal / (1m - aliqIrrfFinal),
                2,
                MidpointRounding.AwayFromZero);

            eventos.Add(new EventoGeradoBulletComJuros(
                NumeroEvento: numeroFinal,
                Tipo: TipoEventoCronograma.IrrfRetido,
                DataPrevista: entrada.DataVencimento,
                Valor: new Money(valorIrrfFinal, moeda),
                SaldoDevedorApos: null));
        }

        return eventos.AsReadOnly();
    }

    private static void ValidarEntrada(EntradaBulletComJuros e)
    {
        if (e.DataVencimento <= e.DataDesembolso)
        {
            throw new ArgumentException(
                "DataVencimento deve ser posterior a DataDesembolso.",
                nameof(e));
        }

        if (e.ValorPrincipal.Valor <= 0m)
        {
            throw new ArgumentException(
                "ValorPrincipal.Valor deve ser maior que zero.",
                nameof(e));
        }

        if (e.TaxaAa.AsDecimal < 0m)
        {
            throw new ArgumentException(
                "TaxaAa não pode ser negativa.",
                nameof(e));
        }

        if (e.PeriodoJurosMeses <= 0)
        {
            throw new ArgumentException(
                "PeriodoJurosMeses deve ser maior que zero.",
                nameof(e));
        }
    }
}
