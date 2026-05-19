using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Posição de um banco específico dentro de um mês projetado.
/// SharePercentual soma 100 em todos os bancos do mesmo mês quando o saldo total é positivo (P-3).
/// </summary>
/// <param name="BancoId">Identificador do banco.</param>
/// <param name="SaldoInicio">Saldo no primeiro dia do mês (igual ao SaldoFim do mês anterior).</param>
/// <param name="SaldoFim">SaldoInicio − TotalAmortizacaoNoMes + TotalCaptacaoNoMes.</param>
/// <param name="TotalAmortizacaoNoMes">Soma de todas as amortizações de principal do banco no mês.</param>
/// <param name="TotalCaptacaoNoMes">Soma de todas as captações do banco no mês.</param>
/// <param name="SharePercentual">
/// Percentual do banco no saldo total de fechamento do mês.
/// Arredondado HalfUp a 4 casas decimais. Zero quando o saldo total for zero.
/// </param>
public sealed record SaldoBancoMes(
    Guid BancoId,
    Money SaldoInicio,
    Money SaldoFim,
    Money TotalAmortizacaoNoMes,
    Money TotalCaptacaoNoMes,
    decimal SharePercentual);
