namespace Sgcf.Domain.Common;

/// <summary>
/// Taxa de câmbio tipada. Armazena cotação de MoedaOrigem em relação ao BRL.
/// Ex: FxRate(5.30, Moeda.Usd) = 1 USD = BRL 5,30.
/// </summary>
public readonly record struct FxRate
{
    public decimal Taxa { get; }
    public Moeda Moeda { get; }

    public FxRate(decimal taxa, Moeda moeda)
    {
        if (taxa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taxa), "Taxa de câmbio deve ser positiva.");
        }

        if (moeda == Moeda.Brl)
        {
            throw new ArgumentException("FxRate não se aplica ao BRL (moeda base).", nameof(moeda));
        }

        Taxa = Arredondamento.HalfUp(taxa, casas: 6);
        Moeda = moeda;
    }

    /// <summary>Converte valor em moeda estrangeira para BRL.</summary>
    public Money ConverterParaBrl(Money valorEstrangeiro)
    {
        if (valorEstrangeiro.Moeda != Moeda)
        {
            throw new InvalidOperationException(
                $"FxRate é de {Moeda.ToString().ToUpperInvariant()} mas o valor é de {valorEstrangeiro.Moeda.ToString().ToUpperInvariant()}.");
        }

        return new Money(Arredondamento.HalfUp(valorEstrangeiro.Valor * Taxa, casas: 6), Moeda.Brl);
    }

    /// <summary>Converte valor em BRL para moeda estrangeira.</summary>
    public Money ConverterDoBrl(Money valorBrl)
    {
        if (valorBrl.Moeda != Moeda.Brl)
        {
            throw new InvalidOperationException("Apenas valores em BRL podem ser convertidos pelo método ConverterDoBrl.");
        }

        return new Money(Arredondamento.HalfUp(valorBrl.Valor / Taxa, casas: 6), Moeda);
    }

    public override string ToString() =>
        $"{Moeda.ToString().ToUpperInvariant()}/BRL {Taxa.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
}
