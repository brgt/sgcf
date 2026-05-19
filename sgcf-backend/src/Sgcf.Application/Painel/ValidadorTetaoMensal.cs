using Sgcf.Application.Painel.Queries;

namespace Sgcf.Application.Painel;

/// <summary>
/// Valida se algum mês da projeção excede o tetão mensal de movimentação (D-11).
///
/// Pure function — zero I/O, zero estado, zero dependências externas.
/// Pode ser testada em isolamento sem nenhum mock.
///
/// Regra: movimentação_mensal = TotalAmortizacaoMes + TotalCaptacaoMes.
/// Quando movimentação > tetaoBrl → adiciona um alerta por mês.
/// Não bloqueia — apenas informa.
/// </summary>
public static class ValidadorTetaoMensal
{
    /// <summary>
    /// Retorna alertas textuais para cada mês que excede o tetão configurado.
    /// </summary>
    /// <param name="projecao">Projeção dos 12 meses do quadro de dívida.</param>
    /// <param name="tetaoBrl">
    /// Limite mensal em BRL. Quando <c>null</c>, retorna lista vazia imediatamente.
    /// </param>
    /// <returns>Lista imutável de strings — uma por mês excedido, ordenada por mês.</returns>
    public static IReadOnlyList<string> Validar(
        QuadroDividaProjecaoDto projecao,
        decimal? tetaoBrl)
    {
        if (!tetaoBrl.HasValue)
        {
            return [];
        }

        List<string>? alertas = null;

        foreach (MesProjecaoDto mes in projecao.Meses)
        {
            decimal movimentacao = mes.TotalAmortizacaoMes + mes.TotalCaptacaoMes;

            if (movimentacao > tetaoBrl.Value)
            {
                alertas ??= [];
                alertas.Add(
                    $"Mês {mes.Mes:D2}/{mes.Ano}: movimentação " +
                    $"R$ {movimentacao:N2} excede tetão configurado " +
                    $"R$ {tetaoBrl.Value:N2}.");
            }
        }

        return alertas is null ? [] : alertas.AsReadOnly();
    }
}
