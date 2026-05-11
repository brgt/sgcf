using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Domain.Calculo;

/// <summary>
/// Cálculo de saldo devedor em três componentes conforme Annex B §6.4.
/// Funções puras — sem I/O, sem efeitos colaterais.
/// </summary>
public static class CalculadorSaldo
{
    private static readonly HashSet<TipoEventoCronograma> TiposComissao =
        new HashSet<TipoEventoCronograma>
        {
            TipoEventoCronograma.IofCambio,
            TipoEventoCronograma.ComissaoSblc,
            TipoEventoCronograma.ComissaoCpg,
            TipoEventoCronograma.ComissaoGarantiaFgi,
            TipoEventoCronograma.TarifaRof,
            TipoEventoCronograma.TarifaCademp,
            TipoEventoCronograma.TarifaCartorio,
            TipoEventoCronograma.BreakFundingFee,
        };

    /// <summary>
    /// Calcula o saldo devedor na data de referência informada.
    /// </summary>
    public static ResultadoSaldo Calcular(EntradaCalculoSaldo entrada)
    {
        if (entrada.DataReferencia < entrada.DataDesembolso)
        {
            throw new ArgumentException(
                "DataReferencia não pode ser anterior a DataDesembolso.",
                nameof(entrada));
        }

        Moeda moeda = entrada.ValorPrincipalInicial.Moeda;

        // ── A) Saldo principal aberto ─────────────────────────────────────────
        decimal totalPrincipalPago = entrada.Eventos
            .Where(e =>
                e.Tipo == TipoEventoCronograma.Principal &&
                e.Status == StatusEventoCronograma.Pago &&
                e.DataPrevista <= entrada.DataReferencia &&
                e.Valor.Moeda == moeda)
            .Sum(e => e.Valor.Valor);

        decimal saldoPrincipal = entrada.ValorPrincipalInicial.Valor - totalPrincipalPago;
        Money saldoPrincipalAberto = new(saldoPrincipal, moeda);

        // ── B) Juros provisionados pro rata desde o último pagamento de juros ─
        LocalDate dataBaseJuros = entrada.DataDesembolso;

        LocalDate? ultimoPagamentoJuros = entrada.Eventos
            .Where(e =>
                e.Tipo == TipoEventoCronograma.Juros &&
                e.Status == StatusEventoCronograma.Pago &&
                e.DataPrevista <= entrada.DataReferencia)
            .Select(e => (LocalDate?)e.DataPrevista)
            .Max();

        if (ultimoPagamentoJuros.HasValue)
        {
            dataBaseJuros = ultimoPagamentoJuros.Value;
        }

        int diasJuros = Period.Between(dataBaseJuros, entrada.DataReferencia, PeriodUnits.Days).Days;
        decimal baseCalculo = (decimal)entrada.BaseCalculo;

        decimal valorJurosProvisionados = Math.Round(
            saldoPrincipal * entrada.TaxaAa.AsDecimal * diasJuros / baseCalculo,
            2,
            MidpointRounding.AwayFromZero);

        Money jurosProvisionados = new(valorJurosProvisionados, moeda);

        // ── C) Comissões a pagar (mesma moeda, status Previsto ou Atrasado) ───
        decimal totalComissoes = entrada.Eventos
            .Where(e =>
                TiposComissao.Contains(e.Tipo) &&
                (e.Status == StatusEventoCronograma.Previsto || e.Status == StatusEventoCronograma.Atrasado) &&
                e.Valor.Moeda == moeda)
            .Sum(e => e.Valor.Valor);

        Money comissoesAPagar = new(totalComissoes, moeda);

        // ── Saldo total ───────────────────────────────────────────────────────
        Money saldoTotal = saldoPrincipalAberto
            .Somar(jurosProvisionados)
            .Somar(comissoesAPagar);

        // ── BRL equivalents (optional, computed when TaxaCambio is provided) ──
        Money? saldoPrincipalAbertoBrl = null;
        Money? jurosProvisionadosBrl = null;
        Money? comissoesAPagarBrl = null;
        Money? saldoTotalBrl = null;

        if (entrada.TaxaCambio.HasValue)
        {
            decimal taxa = entrada.TaxaCambio.Value;
            saldoPrincipalAbertoBrl = new Money(saldoPrincipal * taxa, Moeda.Brl);
            jurosProvisionadosBrl = new Money(valorJurosProvisionados * taxa, Moeda.Brl);
            comissoesAPagarBrl = new Money(totalComissoes * taxa, Moeda.Brl);
            saldoTotalBrl = new Money((saldoPrincipal + valorJurosProvisionados + totalComissoes) * taxa, Moeda.Brl);
        }

        return new ResultadoSaldo(
            SaldoPrincipalAberto: saldoPrincipalAberto,
            JurosProvisionados: jurosProvisionados,
            ComissoesAPagar: comissoesAPagar,
            SaldoTotal: saldoTotal,
            SaldoPrincipalAbertoBrl: saldoPrincipalAbertoBrl,
            JurosProvisionadosBrl: jurosProvisionadosBrl,
            ComissoesAPagarBrl: comissoesAPagarBrl,
            SaldoTotalBrl: saldoTotalBrl);
    }
}
