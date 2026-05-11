using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cotacoes;

public sealed class CotacaoFx : Entity
{
    public Moeda MoedaBase { get; private set; }
    public Moeda MoedaQuote { get; private set; }
    public Instant Momento { get; private set; }
    public TipoCotacao Tipo { get; private set; }

    internal decimal ValorCompraDecimal { get; private set; }
    internal decimal ValorVendaDecimal { get; private set; }

    public Money ValorCompra => new(ValorCompraDecimal, MoedaQuote);
    public Money ValorVenda => new(ValorVendaDecimal, MoedaQuote);

    public string Fonte { get; private set; } = default!;

    private CotacaoFx() { }

    public static CotacaoFx Criar(
        Moeda moedaBase,
        TipoCotacao tipo,
        Money valorCompra,
        Money valorVenda,
        string fonte,
        Instant momento)
    {
        if (moedaBase == Moeda.Brl)
        {
            throw new ArgumentException("MoedaBase não pode ser BRL.", nameof(moedaBase));
        }

        if (valorCompra.Moeda != valorVenda.Moeda)
        {
            throw new ArgumentException("ValorCompra e ValorVenda devem ter a mesma moeda.");
        }

        if (valorCompra.Valor <= 0)
        {
            throw new ArgumentException("ValorCompra deve ser positivo.", nameof(valorCompra));
        }

        if (valorVenda.Valor <= 0)
        {
            throw new ArgumentException("ValorVenda deve ser positivo.", nameof(valorVenda));
        }

        if (string.IsNullOrWhiteSpace(fonte))
        {
            throw new ArgumentException("Fonte não pode ser vazia.", nameof(fonte));
        }

        return new CotacaoFx
        {
            MoedaBase = moedaBase,
            MoedaQuote = valorCompra.Moeda,
            Momento = momento,
            Tipo = tipo,
            ValorCompraDecimal = valorCompra.Valor,
            ValorVendaDecimal = valorVenda.Valor,
            Fonte = fonte
        };
    }
}
