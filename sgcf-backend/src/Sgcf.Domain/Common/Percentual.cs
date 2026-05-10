namespace Sgcf.Domain.Common;

/// <summary>
/// Percentual como valor tipado. Armazenado internamente como 0.06 para 6%.
/// Use Percentual.De(6m) para criar um percentual de 6% a.a.
/// </summary>
public readonly record struct Percentual
{
    // Stored as fraction (0.06 for 6%)

    private Percentual(decimal valorFracional) => AsDecimal = valorFracional;

    /// <summary>Cria um percentual a partir de um valor "humano". Ex: De(6m) = 6% = 0,06.</summary>
    public static Percentual De(decimal percentualHumano)
    {
        if (percentualHumano < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentualHumano), "Percentual não pode ser negativo.");
        }

        return new Percentual(percentualHumano / 100m);
    }

    /// <summary>Cria um percentual a partir de fração decimal. Ex: DeFracao(0.06m) = 6%.</summary>
    public static Percentual DeFracao(decimal fracao)
    {
        if (fracao < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fracao), "Percentual não pode ser negativo.");
        }

        return new Percentual(fracao);
    }

    /// <summary>Valor fracional para uso em fórmulas. Ex: 0,06 para 6%.</summary>
    public decimal AsDecimal { get; }

    /// <summary>Valor "humano" para exibição. Ex: 6,0 para 6%.</summary>
    public decimal AsHumano => AsDecimal * 100m;

    public override string ToString() => $"{AsHumano:F4}%";
}
