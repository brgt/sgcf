using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Gerador de cronograma bullet (amortização única no vencimento).
/// Implementa Annex B §6.1 — "bullet único": 1 amortização no vencimento.
/// Funções puras — sem I/O, sem efeitos colaterais.
/// </summary>
public static class BulletStrategy
{
    /// <summary>
    /// Gera a lista de eventos financeiros para um contrato bullet.
    /// </summary>
    public static IReadOnlyList<EventoGeradoBullet> Gerar(EntradaBullet entrada)
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

        List<EventoGeradoBullet> eventos = [];

        // ── Eventos de dia 0 (data de desembolso) ───────────────────────────

        if (entrada.AliqIofCambio.HasValue)
        {
            decimal valorIof = Math.Round(
                entrada.ValorPrincipal.Valor * entrada.AliqIofCambio.Value.AsDecimal,
                2,
                MidpointRounding.AwayFromZero);

            eventos.Add(new EventoGeradoBullet(
                NumeroEvento: 0,
                Tipo: TipoEventoCronograma.IofCambio,
                DataPrevista: entrada.DataDesembolso,
                Valor: new Money(valorIof, entrada.ValorPrincipal.Moeda),
                SaldoDevedorApos: null));
        }

        if (entrada.TarifaRofBrl.HasValue)
        {
            eventos.Add(new EventoGeradoBullet(
                NumeroEvento: 0,
                Tipo: TipoEventoCronograma.TarifaRof,
                DataPrevista: entrada.DataDesembolso,
                Valor: new Money(entrada.TarifaRofBrl.Value, Moeda.Brl),
                SaldoDevedorApos: null));
        }

        if (entrada.TarifaCadempBrl.HasValue)
        {
            eventos.Add(new EventoGeradoBullet(
                NumeroEvento: 0,
                Tipo: TipoEventoCronograma.TarifaCademp,
                DataPrevista: entrada.DataDesembolso,
                Valor: new Money(entrada.TarifaCadempBrl.Value, Moeda.Brl),
                SaldoDevedorApos: null));
        }

        // ── Eventos no vencimento (evento 1) ─────────────────────────────────

        int prazo = Period.Between(entrada.DataDesembolso, entrada.DataVencimento, PeriodUnits.Days).Days;
        decimal baseCalculo = (decimal)entrada.BaseCalculo;

        decimal valorJuros = Math.Round(
            entrada.ValorPrincipal.Valor * entrada.TaxaAa.AsDecimal * prazo / baseCalculo,
            2,
            MidpointRounding.AwayFromZero);

        Money juros = new(valorJuros, entrada.ValorPrincipal.Moeda);

        eventos.Add(new EventoGeradoBullet(
            NumeroEvento: 1,
            Tipo: TipoEventoCronograma.Juros,
            DataPrevista: entrada.DataVencimento,
            Valor: juros,
            SaldoDevedorApos: null));

        eventos.Add(new EventoGeradoBullet(
            NumeroEvento: 1,
            Tipo: TipoEventoCronograma.Principal,
            DataPrevista: entrada.DataVencimento,
            Valor: entrada.ValorPrincipal,
            SaldoDevedorApos: 0m));

        if (entrada.AliqIrrf.HasValue)
        {
            decimal aliqIrrf = entrada.AliqIrrf.Value.AsDecimal;
            decimal valorIrrf = Math.Round(
                valorJuros * aliqIrrf / (1m - aliqIrrf),
                2,
                MidpointRounding.AwayFromZero);

            eventos.Add(new EventoGeradoBullet(
                NumeroEvento: 1,
                Tipo: TipoEventoCronograma.IrrfRetido,
                DataPrevista: entrada.DataVencimento,
                Valor: new Money(valorIrrf, entrada.ValorPrincipal.Moeda),
                SaldoDevedorApos: null));
        }

        return eventos.AsReadOnly();
    }
}
