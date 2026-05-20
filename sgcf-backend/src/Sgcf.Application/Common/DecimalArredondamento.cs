namespace Sgcf.Application.Common;

/// <summary>
/// Helper de arredondamento para valores de apresentação em DTOs de saída.
///
/// Motivação: cálculos internos de domínio operam com precisão arbitrária (até 6dp via
/// <c>Money</c> e o projetor). A API devolve apenas 2 casas decimais — padrão regulatório
/// BR (arredondamento comercial HalfUp, conforme CLAUDE.md §4).
///
/// Usar exclusivamente nos mapeadores <c>From(entity)</c> / <c>MapXxx()</c> dos DTOs —
/// NUNCA nos cálculos de domínio.
/// </summary>
internal static class DecimalArredondamento
{
    private const int CasasApresentacao = 2;

    /// <summary>
    /// Arredonda um valor monetário ou percentual para 2 casas decimais com HalfUp.
    /// </summary>
    public static decimal Mostrar(decimal v) =>
        Math.Round(v, CasasApresentacao, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Arredonda um valor monetário ou percentual nullable para 2 casas decimais com HalfUp.
    /// Retorna <c>null</c> quando o valor de entrada é <c>null</c>.
    /// </summary>
    public static decimal? Mostrar(decimal? v) =>
        v.HasValue ? Math.Round(v.Value, CasasApresentacao, MidpointRounding.AwayFromZero) : null;
}
