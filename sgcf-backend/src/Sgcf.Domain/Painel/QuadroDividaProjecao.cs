using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Output completo do <see cref="ProjetorSaldoMensal"/>: 12 meses projetados para o ano informado.
/// </summary>
/// <param name="Ano">Ano civil projetado.</param>
/// <param name="Meses">
/// Exatamente 12 entradas, uma por mês (índice 0 = janeiro, índice 11 = dezembro).
/// </param>
/// <param name="SaldoInicialTotalBrl">
/// Soma do saldo inicial por banco no primeiro dia do ano (base da projeção).
/// Igual à soma de <c>SaldoTotalInicio</c> do mês 1.
/// </param>
public sealed record QuadroDividaProjecao(
    int Ano,
    IReadOnlyList<MesProjecao> Meses,
    Money SaldoInicialTotalBrl);
