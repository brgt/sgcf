using NodaTime;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Resultado completo do quadro de dívida para um ano civil.
/// Combina o snapshot atual da carteira, a projeção mês a mês e os totais anuais.
/// </summary>
/// <param name="Ano">Ano civil consultado.</param>
/// <param name="DataReferencia">Data em que o snapshot inicial foi calculado (hoje).</param>
/// <param name="SnapshotInicial">Saldo atual por banco — base da projeção (obtido via GetSaldoPorBancoAtualQuery).</param>
/// <param name="Projecao">12 meses projetados com breakdown por banco.</param>
/// <param name="Sumario">Totais anuais agregados (início, fim, amortizações, captações, variação %).</param>
/// <param name="Alertas">Lista de alertas contextuais. Vazio no MVP — populado na Task 3.4.</param>
public sealed record QuadroDividaDto(
    int Ano,
    DateOnly DataReferencia,
    SaldoPorBancoAtualDto SnapshotInicial,
    QuadroDividaProjecaoDto Projecao,
    QuadroDividaSumarioDto Sumario,
    IReadOnlyList<string> Alertas);

/// <summary>
/// Container dos 12 meses projetados.
/// </summary>
/// <param name="Meses">Exatamente 12 entradas, índice 0 = janeiro, índice 11 = dezembro.</param>
public sealed record QuadroDividaProjecaoDto(
    IReadOnlyList<MesProjecaoDto> Meses);

/// <summary>
/// Projeção de um único mês calendário com breakdown por banco.
/// </summary>
/// <param name="Ano">Ano ao qual o mês pertence.</param>
/// <param name="Mes">Número do mês (1–12).</param>
/// <param name="Bancos">Posição de cada banco no mês. Inclui apenas bancos com saldo ou eventos.</param>
/// <param name="SaldoTotalInicio">Soma de SaldoInicio de todos os bancos do mês em BRL.</param>
/// <param name="SaldoTotalFim">Soma de SaldoFim de todos os bancos do mês em BRL.</param>
/// <param name="TotalAmortizacaoMes">Total de amortizações de principal no mês em BRL.</param>
/// <param name="TotalCaptacaoMes">Total de captações no mês em BRL.</param>
public sealed record MesProjecaoDto(
    int Ano,
    int Mes,
    IReadOnlyList<SaldoBancoMesDto> Bancos,
    decimal SaldoTotalInicio,
    decimal SaldoTotalFim,
    decimal TotalAmortizacaoMes,
    decimal TotalCaptacaoMes);

/// <summary>
/// Posição de um banco específico dentro de um mês projetado.
/// </summary>
/// <param name="BancoId">Identificador do banco.</param>
/// <param name="BancoApelido">Apelido do banco (AD-10).</param>
/// <param name="SaldoInicio">Saldo em BRL no início do mês.</param>
/// <param name="SaldoFim">Saldo em BRL no fim do mês após eventos.</param>
/// <param name="TotalAmortizacaoNoMes">Soma das amortizações de principal do banco no mês em BRL.</param>
/// <param name="TotalCaptacaoNoMes">Soma das captações do banco no mês em BRL.</param>
/// <param name="SharePercentual">Percentual do banco no saldo total de fechamento do mês.</param>
public sealed record SaldoBancoMesDto(
    Guid BancoId,
    string BancoApelido,
    decimal SaldoInicio,
    decimal SaldoFim,
    decimal TotalAmortizacaoNoMes,
    decimal TotalCaptacaoNoMes,
    decimal SharePercentual);

/// <summary>
/// Totais anuais agregados da projeção.
/// </summary>
/// <param name="SaldoTotalInicioAno">Saldo total no início do ano (= SaldoTotalInicio do mês 1).</param>
/// <param name="SaldoTotalFimAno">Saldo total no fim do ano (= SaldoTotalFim do mês 12).</param>
/// <param name="TotalAmortizacaoNoAno">Soma de todas as amortizações de principal no ano.</param>
/// <param name="TotalCaptacaoNoAno">Soma de todas as captações no ano.</param>
/// <param name="VariacaoAnualPercentual">
/// (SaldoFimAno − SaldoInicioAno) / SaldoInicioAno × 100.
/// Zero quando SaldoInicioAno for zero.
/// </param>
public sealed record QuadroDividaSumarioDto(
    decimal SaldoTotalInicioAno,
    decimal SaldoTotalFimAno,
    decimal TotalAmortizacaoNoAno,
    decimal TotalCaptacaoNoAno,
    decimal VariacaoAnualPercentual);
