namespace Sgcf.Domain.Common;

/// <summary>
/// Funções de arredondamento financeiro segundo a legislação brasileira.
/// </summary>
public static class Arredondamento
{
    /// <summary>
    /// Arredondamento comercial (HalfUp / AwayFromZero).
    /// Ponto médio sempre arredonda para longe do zero — exigido pela regulação financeira brasileira.
    /// Diferente do padrão C# (ToEven / banker's rounding) que arredonda 2.5 → 2.
    /// </summary>
    public static decimal HalfUp(decimal valor, int casas) =>
        Math.Round(valor, casas, MidpointRounding.AwayFromZero);
}
