namespace Sgcf.Application.Common;

/// <summary>
/// Aplica arredondamento de apresentação a valores monetários e percentuais de saída.
///
/// Motivação: valores calculados internamente (via <c>Money</c>) podem ter até 6 casas decimais.
/// A API expõe no máximo 2 casas — regulação financeira BR exige HalfUp (arredondamento comercial).
///
/// Regra: use este helper APENAS nos mapeadores DTO, nunca em cálculos de domínio.
/// O domínio armazena e opera com precisão total; somente a camada de apresentação arredonda.
/// </summary>
internal static class DecimalArredondamento
{
    private const int CasasApresentacao = 2;

    /// <summary>
    /// Arredonda <paramref name="v"/> para 2 casas decimais usando HalfUp
    /// (<see cref="MidpointRounding.AwayFromZero"/>).
    /// </summary>
    public static decimal Mostrar(decimal v) =>
        Math.Round(v, CasasApresentacao, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Arredonda <paramref name="v"/> para 2 casas decimais usando HalfUp, ou retorna
    /// <c>null</c> quando o valor é <c>null</c>.
    /// </summary>
    public static decimal? Mostrar(decimal? v) =>
        v.HasValue
            ? Math.Round(v.Value, CasasApresentacao, MidpointRounding.AwayFromZero)
            : null;
}
