using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Resultado da projeção para um único mês calendário.
/// Contém o breakdown por banco e os totais consolidados.
/// </summary>
/// <param name="AnoCalendar">Ano ao qual o mês pertence.</param>
/// <param name="Mes">Número do mês (1 = janeiro … 12 = dezembro).</param>
/// <param name="SaldosPorBanco">
/// Posição de cada banco no mês. Bancos sem saldo e sem eventos não aparecem.
/// </param>
/// <param name="SaldoTotalInicio">Soma de SaldoInicio de todos os bancos do mês.</param>
/// <param name="SaldoTotalFim">Soma de SaldoFim de todos os bancos do mês.</param>
public sealed record MesProjecao(
    int AnoCalendar,
    int Mes,
    IReadOnlyList<SaldoBancoMes> SaldosPorBanco,
    Money SaldoTotalInicio,
    Money SaldoTotalFim);
