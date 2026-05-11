using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

public sealed class Parcela : Entity
{
    public Guid ContratoId { get; private set; }
    public short Numero { get; private set; }
    public LocalDate DataVencimento { get; private set; }

    internal decimal ValorPrincipalDecimal { get; private set; }
    internal decimal ValorJurosDecimal { get; private set; }
    internal decimal? ValorPagoDecimal { get; private set; }

    public Moeda Moeda { get; private set; }
    public StatusParcela Status { get; private set; }
    public LocalDate? DataPagamento { get; private set; }

    public Money ValorPrincipal => new(ValorPrincipalDecimal, Moeda);
    public Money ValorJuros => new(ValorJurosDecimal, Moeda);
    public Money? ValorPago => ValorPagoDecimal.HasValue ? new(ValorPagoDecimal.Value, Moeda) : null;

    private Parcela() { }

    internal static Parcela Criar(
        Guid contratoId,
        short numero,
        LocalDate dataVencimento,
        Money valorPrincipal,
        Money valorJuros)
    {
        if (valorPrincipal.Moeda != valorJuros.Moeda)
        {
            throw new ArgumentException("ValorPrincipal e ValorJuros devem ter a mesma moeda.");
        }

        return new Parcela
        {
            ContratoId = contratoId,
            Numero = numero,
            DataVencimento = dataVencimento,
            ValorPrincipalDecimal = valorPrincipal.Valor,
            ValorJurosDecimal = valorJuros.Valor,
            Moeda = valorPrincipal.Moeda,
            Status = StatusParcela.Pendente
        };
    }

    public void RegistrarPagamento(Money valorPago, LocalDate dataPagamento)
    {
        if (valorPago.Moeda != Moeda)
        {
            throw new ArgumentException("Moeda do pagamento não corresponde à moeda da parcela.", nameof(valorPago));
        }

        if (valorPago.Valor <= 0)
        {
            throw new ArgumentException("Valor pago deve ser positivo.", nameof(valorPago));
        }

        ValorPagoDecimal = valorPago.Valor;
        DataPagamento = dataPagamento;

        var totalDevido = ValorPrincipal.Somar(ValorJuros);
        Status = valorPago.MaiorQue(totalDevido) || valorPago.Valor == totalDevido.Valor
            ? StatusParcela.Paga
            : StatusParcela.Parcial;
    }
}
