using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

public sealed class EventoCronograma : Entity
{
    public Guid ContratoId { get; private set; }
    public short NumeroEvento { get; private set; }
    public TipoEventoCronograma Tipo { get; private set; }
    public LocalDate DataPrevista { get; private set; }

    internal decimal ValorMoedaOriginalDecimal { get; private set; }
    public Money ValorMoedaOriginal => new(ValorMoedaOriginalDecimal, Moeda);

    internal decimal? ValorBrlEstimadoDecimal { get; private set; }
    public Money? ValorBrlEstimado => ValorBrlEstimadoDecimal.HasValue ? new(ValorBrlEstimadoDecimal.Value, Moeda.Brl) : null;

    internal decimal? SaldoDevedorAposDecimal { get; private set; }
    public Money? SaldoDevedorApos => SaldoDevedorAposDecimal.HasValue ? new(SaldoDevedorAposDecimal.Value, Moeda) : null;

    public Moeda Moeda { get; private set; }
    public StatusEventoCronograma Status { get; private set; }
    public LocalDate? DataPagamentoEfetivo { get; private set; }

    internal decimal? ValorPagamentoEfetivoDecimal { get; private set; }
    public Money? ValorPagamentoEfetivo => ValorPagamentoEfetivoDecimal.HasValue ? new(ValorPagamentoEfetivoDecimal.Value, Moeda) : null;

    internal decimal? ValorPagamentoEfetivoBrlDecimal { get; private set; }
    public Money? ValorPagamentoEfetivoBrl => ValorPagamentoEfetivoBrlDecimal.HasValue ? new(ValorPagamentoEfetivoBrlDecimal.Value, Moeda.Brl) : null;

    internal decimal? TaxaCambioPagamentoDecimal { get; private set; }
    public decimal? TaxaCambioPagamento => TaxaCambioPagamentoDecimal;

    public string? Observacoes { get; private set; }
    public string? ComprovanteUrl { get; private set; }

    private EventoCronograma() { }

    public static EventoCronograma Criar(
        Guid contratoId,
        short numeroEvento,
        TipoEventoCronograma tipo,
        LocalDate dataPrevista,
        Money valorMoedaOriginal,
        decimal? saldoDevedorApos = null)
    {
        if (valorMoedaOriginal.Valor < 0)
        {
            throw new ArgumentException("ValorMoedaOriginal não pode ser negativo.", nameof(valorMoedaOriginal));
        }

        return new EventoCronograma
        {
            ContratoId = contratoId,
            NumeroEvento = numeroEvento,
            Tipo = tipo,
            DataPrevista = dataPrevista,
            ValorMoedaOriginalDecimal = valorMoedaOriginal.Valor,
            Moeda = valorMoedaOriginal.Moeda,
            SaldoDevedorAposDecimal = saldoDevedorApos,
            Status = StatusEventoCronograma.Previsto,
        };
    }

    public void RegistrarPagamento(Money valorEfetivo, Money valorBrl, decimal taxaCambio, LocalDate dataPagamento)
    {
        ValorPagamentoEfetivoDecimal = valorEfetivo.Valor;
        ValorPagamentoEfetivoBrlDecimal = valorBrl.Valor;
        TaxaCambioPagamentoDecimal = taxaCambio;
        DataPagamentoEfetivo = dataPagamento;
        Status = StatusEventoCronograma.Pago;
    }

    public void AtualizarBrlEstimado(Money valorBrl)
    {
        ValorBrlEstimadoDecimal = valorBrl.Valor;
    }

    public void MarcarAtrasado()
    {
        Status = StatusEventoCronograma.Atrasado;
    }
}
