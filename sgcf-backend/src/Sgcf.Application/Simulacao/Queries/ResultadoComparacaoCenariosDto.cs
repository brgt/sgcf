using Sgcf.Application.Painel.Queries;

namespace Sgcf.Application.Simulacao.Queries;

/// <summary>
/// Resultado do comparativo entre múltiplos cenários de simulação.
///
/// Retorna cada cenário projetado com seus deltas mensais e anuais em relação ao
/// primeiro cenário (baseline). O baseline nunca tem deltas — seus campos
/// <see cref="CenarioComparadoDto.DeltasMensais"/> e <see cref="CenarioComparadoDto.DeltaAnual"/>
/// são sempre <c>null</c>.
///
/// SPEC §7 Task 4.1. Fase 4.
/// </summary>
/// <param name="Ano">Ano civil consultado.</param>
/// <param name="DataReferencia">Data de referência da projeção (retornada pelo <c>GetQuadroDividaQuery</c>).</param>
/// <param name="Cenarios">
/// Lista ordenada de cenários comparados. Exatamente a mesma ordem de entrada.
/// O primeiro item é sempre o baseline.
/// </param>
public sealed record ResultadoComparacaoCenariosDto(
    int Ano,
    DateOnly DataReferencia,
    IReadOnlyList<CenarioComparadoDto> Cenarios);

/// <summary>
/// Projeção completa de um cenário individual dentro do comparativo.
///
/// Para o baseline, <see cref="DeltasMensais"/> e <see cref="DeltaAnual"/> são <c>null</c>.
/// Para os demais cenários, os deltas são calculados em relação ao baseline mês a mês.
/// </summary>
/// <param name="CenarioId">Identificador do cenário de simulação.</param>
/// <param name="Nome">Nome legível do cenário.</param>
/// <param name="Status">Status do ciclo de vida: "Rascunho", "Ativo" ou "Arquivado".</param>
/// <param name="AnoBase">Ano-calendário de referência do cenário.</param>
/// <param name="EhBaseline">
/// <c>true</c> para o primeiro cenário da lista — usado como referência para os deltas.
/// </param>
/// <param name="Projecao">Os 12 meses projetados com breakdown por banco.</param>
/// <param name="Sumario">Totais anuais agregados.</param>
/// <param name="DeltasMensais">
/// Deltas mensais em relação ao baseline. <c>null</c> quando <see cref="EhBaseline"/> for <c>true</c>.
/// </param>
/// <param name="DeltaAnual">
/// Delta anual agregado em relação ao baseline. <c>null</c> quando <see cref="EhBaseline"/> for <c>true</c>.
/// </param>
public sealed record CenarioComparadoDto(
    Guid CenarioId,
    string Nome,
    string Status,
    int AnoBase,
    bool EhBaseline,
    QuadroDividaProjecaoDto Projecao,
    QuadroDividaSumarioDto Sumario,
    IReadOnlyList<DeltaMensalDto>? DeltasMensais,
    DeltaAnualDto? DeltaAnual);

/// <summary>
/// Delta de um único mês calendário em relação ao baseline.
///
/// Todos os campos "Delta" são calculados como: <c>cenário − baseline</c>.
/// Valores positivos indicam que o cenário tem mais dívida/captação que o baseline.
/// </summary>
/// <param name="Mes">Número do mês (1–12).</param>
/// <param name="SaldoFimDelta">
/// <c>SaldoTotalFim</c> do cenário menos o do baseline, em BRL.
/// </param>
/// <param name="TotalCaptacaoDelta">
/// <c>TotalCaptacaoMes</c> do cenário menos o do baseline, em BRL.
/// </param>
/// <param name="TotalAmortizacaoDelta">
/// <c>TotalAmortizacaoMes</c> do cenário menos o do baseline, em BRL.
/// </param>
/// <param name="SaldoFimDeltaPercentual">
/// <c>(SaldoFimDelta / SaldoFimBaseline) × 100</c>.
/// Zero quando <c>SaldoFimBaseline == 0</c> para evitar divisão por zero.
/// </param>
public sealed record DeltaMensalDto(
    int Mes,
    decimal SaldoFimDelta,
    decimal TotalCaptacaoDelta,
    decimal TotalAmortizacaoDelta,
    decimal SaldoFimDeltaPercentual);

/// <summary>
/// Delta anual agregado em relação ao baseline.
///
/// Derivado dos campos do <see cref="QuadroDividaSumarioDto"/> de cada cenário
/// (não dos 12 meses individuais) para garantir consistência com o sumário exibido.
/// </summary>
/// <param name="SaldoFimAnoDelta">
/// <c>SaldoTotalFimAno</c> do cenário menos o do baseline, em BRL.
/// </param>
/// <param name="TotalCaptacaoAnoDelta">
/// <c>TotalCaptacaoNoAno</c> do cenário menos o do baseline, em BRL.
/// </param>
/// <param name="SaldoFimAnoDeltaPercentual">
/// <c>(SaldoFimAnoDelta / SaldoFimAnoBaseline) × 100</c>.
/// Zero quando <c>SaldoFimAnoBaseline == 0</c>.
/// </param>
public sealed record DeltaAnualDto(
    decimal SaldoFimAnoDelta,
    decimal TotalCaptacaoAnoDelta,
    decimal SaldoFimAnoDeltaPercentual);
