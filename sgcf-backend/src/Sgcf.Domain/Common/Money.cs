namespace Sgcf.Domain.Common;

/// <summary>
/// Valor monetário tipado. Combina decimal com Moeda — nunca usar decimal cru.
/// Arredondamento HalfUp em 6 casas em todas as operações.
/// </summary>
public readonly record struct Money
{
    public decimal Valor { get; }
    public Moeda Moeda { get; }

    public Money(decimal valor, Moeda moeda)
    {
        Valor = Arredondamento.HalfUp(valor, casas: 6);
        Moeda = moeda;
    }

    public Money Multiplicar(decimal fator) =>
        new(Arredondamento.HalfUp(Valor * fator, casas: 6), Moeda);

    public Money Dividir(decimal divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Divisor não pode ser zero em operação monetária.");
        }

        return new(Arredondamento.HalfUp(Valor / divisor, casas: 6), Moeda);
    }

    public Money Somar(Money outro)
    {
        GuardarMesmaMoeda(outro);
        return new(Arredondamento.HalfUp(Valor + outro.Valor, casas: 6), Moeda);
    }

    public Money Subtrair(Money outro)
    {
        GuardarMesmaMoeda(outro);
        return new(Arredondamento.HalfUp(Valor - outro.Valor, casas: 6), Moeda);
    }

    public bool MaiorQue(Money outro)
    {
        GuardarMesmaMoeda(outro);
        return Valor > outro.Valor;
    }

    public bool MenorQue(Money outro)
    {
        GuardarMesmaMoeda(outro);
        return Valor < outro.Valor;
    }

    public static Money Zero(Moeda moeda) => new(0m, moeda);

    // Formato "BRL 100.000000" — padrão ISO 4217 em maiúsculas, separador decimal invariante
    public override string ToString() =>
        $"{Moeda.ToString().ToUpperInvariant()} {Valor.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";

    private void GuardarMesmaMoeda(Money outro)
    {
        if (Moeda != outro.Moeda)
        {
            throw new InvalidOperationException(
                $"Operação entre moedas diferentes: {Moeda.ToString().ToUpperInvariant()} e {outro.Moeda.ToString().ToUpperInvariant()}. Use FxRate para converter antes.");
        }
    }
}
