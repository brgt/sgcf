using MediatR;

using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Queries;

/// <summary>
/// Retorna a projeção do Quadro da Dívida para múltiplos cenários com deltas em relação ao primeiro (baseline).
///
/// Restrições:
/// - Mínimo 1 cenário, máximo 5 por chamada (limite operacional).
/// - Todos os cenários devem ter o mesmo <c>AnoBase</c> (409 caso contrário).
/// - Cenário inexistente → 404 (via <see cref="KeyNotFoundException"/>).
///
/// Delegação: para cada cenário chama <see cref="GetQuadroDividaQuery"/> via <c>IMediator</c>,
/// reutilizando a lógica de projeção, cache e alertas da Task 3.1.
///
/// SPEC §7 Task 4.1. Fase 4.
/// </summary>
/// <param name="Ano">Ano civil a consultar. Deve estar entre 2020 e 2050 inclusive.</param>
/// <param name="CenarioIds">
/// Lista de identificadores dos cenários a comparar.
/// O primeiro elemento é o baseline — seus deltas serão sempre null.
/// </param>
public sealed record CompararCenariosQuery(
    int Ano,
    IReadOnlyList<Guid> CenarioIds) : IRequest<ResultadoComparacaoCenariosDto>;

/// <summary>
/// Handler do <see cref="CompararCenariosQuery"/>.
///
/// Algoritmo:
///   1. Valida limites (1–5 cenários).
///   2. Para cada cenarioId busca os metadados do cenário no repositório (nome, status, AnoBase).
///   3. Valida que todos os cenários têm o mesmo AnoBase.
///   4. Para cada cenarioId chama <c>GetQuadroDividaQuery(Ano, cenarioId)</c> via mediator.
///      Sequencial no MVP — simples e sem complexidade de Task.WhenAll
///      (o número máximo é 5, tempo não é crítico para uma operação de análise).
///   5. O primeiro resultado é o baseline — sem deltas.
///   6. Para os demais calcula deltas mensais e anuais em relação ao baseline.
/// </summary>
public sealed class CompararCenariosQueryHandler(
    IMediator mediator,
    ICenarioSimulacaoRepository cenarioRepo)
    : IRequestHandler<CompararCenariosQuery, ResultadoComparacaoCenariosDto>
{
    private const int LimiteMaximoCenarios = 5;

    /// <inheritdoc/>
    public async Task<ResultadoComparacaoCenariosDto> Handle(
        CompararCenariosQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Guardar rápida: limites de quantidade
        ValidarQuantidadeCenarios(query.CenarioIds);

        // 2. Carregar metadados dos cenários para validação de AnoBase
        List<CenarioSimulacao> cenarios = await CarregarCenariosAsync(query.CenarioIds, cancellationToken);

        // 3. Validar AnoBase consistente entre todos os cenários
        ValidarAnoBaseConsistente(cenarios);

        // 4. Chamar GetQuadroDividaQuery para cada cenário em série
        List<QuadroDividaDto> quadros = await ProjetarTodosOsCenariosAsync(query, cancellationToken);

        // 5. Montar resultado com deltas
        IReadOnlyList<CenarioComparadoDto> cenariosComparados = MontarComparativo(cenarios, quadros);

        DateOnly dataReferencia = quadros[0].DataReferencia;

        return new ResultadoComparacaoCenariosDto(query.Ano, dataReferencia, cenariosComparados);
    }

    // ── Validações ────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica que a lista tem pelo menos 1 e no máximo 5 cenários.
    /// Lança <see cref="ArgumentException"/> para ser mapeada para 400 pelo controller.
    /// </summary>
    private static void ValidarQuantidadeCenarios(IReadOnlyList<Guid> cenarioIds)
    {
        if (cenarioIds.Count == 0)
        {
            throw new ArgumentException(
                "Pelo menos 1 cenário é obrigatório para comparação.",
                nameof(cenarioIds));
        }

        if (cenarioIds.Count > LimiteMaximoCenarios)
        {
            throw new ArgumentException(
                $"Máximo {LimiteMaximoCenarios} cenários por comparação. " +
                $"Recebidos: {cenarioIds.Count}.",
                nameof(cenarioIds));
        }
    }

    /// <summary>
    /// Valida que todos os cenários têm o mesmo <c>AnoBase</c>.
    /// Lança <see cref="InvalidOperationException"/> para ser mapeada para 409 pelo controller.
    /// A validação deve ocorrer antes de qualquer projeção — evita trabalho desnecessário.
    /// </summary>
    private static void ValidarAnoBaseConsistente(List<CenarioSimulacao> cenarios)
    {
        if (cenarios.Count <= 1)
        {
            return;
        }

        int anoBaseBaseline = cenarios[0].AnoBase;

        foreach (CenarioSimulacao cenario in cenarios)
        {
            if (cenario.AnoBase != anoBaseBaseline)
            {
                throw new InvalidOperationException(
                    $"Todos os cenários devem ter o mesmo AnoBase para comparação. " +
                    $"Baseline '{cenarios[0].Nome}' tem AnoBase={anoBaseBaseline}, " +
                    $"mas '{cenario.Nome}' tem AnoBase={cenario.AnoBase}.");
            }
        }
    }

    // ── Carregamento ──────────────────────────────────────────────────────────

    /// <summary>
    /// Carrega os metadados de cada cenário do repositório, preservando a ordem da entrada.
    /// Lança <see cref="KeyNotFoundException"/> se qualquer cenário não for encontrado.
    /// </summary>
    private async Task<List<CenarioSimulacao>> CarregarCenariosAsync(
        IReadOnlyList<Guid> cenarioIds,
        CancellationToken ct)
    {
        List<CenarioSimulacao> cenarios = new(cenarioIds.Count);

        foreach (Guid cenarioId in cenarioIds)
        {
            CenarioSimulacao cenario = await cenarioRepo.GetByIdAsync(cenarioId, ct)
                ?? throw new KeyNotFoundException(
                    $"Cenário de simulação '{cenarioId}' não encontrado.");

            cenarios.Add(cenario);
        }

        return cenarios;
    }

    /// <summary>
    /// Projeta o Quadro da Dívida para cada cenário via <see cref="GetQuadroDividaQuery"/>.
    /// Executado em série — máximo de 5 projeções, número pequeno que não justifica paralelismo
    /// no MVP (cada projeção já reutiliza o cache Redis quando disponível).
    /// </summary>
    private async Task<List<QuadroDividaDto>> ProjetarTodosOsCenariosAsync(
        CompararCenariosQuery query,
        CancellationToken ct)
    {
        List<QuadroDividaDto> quadros = new(query.CenarioIds.Count);

        foreach (Guid cenarioId in query.CenarioIds)
        {
            QuadroDividaDto quadro = await mediator.Send(
                new GetQuadroDividaQuery(query.Ano, cenarioId), ct);

            quadros.Add(quadro);
        }

        return quadros;
    }

    // ── Montagem do comparativo ───────────────────────────────────────────────

    /// <summary>
    /// Constrói a lista de <see cref="CenarioComparadoDto"/> com deltas calculados.
    /// O primeiro item (baseline) recebe <c>DeltasMensais = null</c> e <c>DeltaAnual = null</c>.
    /// </summary>
    private static System.Collections.ObjectModel.ReadOnlyCollection<CenarioComparadoDto> MontarComparativo(
        List<CenarioSimulacao> cenarios,
        List<QuadroDividaDto> quadros)
    {
        List<CenarioComparadoDto> resultado = new(cenarios.Count);

        QuadroDividaDto baseline = quadros[0];

        for (int i = 0; i < cenarios.Count; i++)
        {
            CenarioSimulacao cenario = cenarios[i];
            QuadroDividaDto quadro = quadros[i];
            bool ehBaseline = i == 0;

            IReadOnlyList<DeltaMensalDto>? deltasMensais = ehBaseline
                ? null
                : CalcularDeltasMensais(baseline.Projecao, quadro.Projecao);

            DeltaAnualDto? deltaAnual = ehBaseline
                ? null
                : CalcularDeltaAnual(baseline.Sumario, quadro.Sumario);

            resultado.Add(new CenarioComparadoDto(
                CenarioId: cenario.Id,
                Nome: cenario.Nome,
                Status: cenario.Status.ToString(),
                AnoBase: cenario.AnoBase,
                EhBaseline: ehBaseline,
                Projecao: quadro.Projecao,
                Sumario: quadro.Sumario,
                DeltasMensais: deltasMensais,
                DeltaAnual: deltaAnual));
        }

        return resultado.AsReadOnly();
    }

    /// <summary>
    /// Calcula os deltas mensais entre o cenário e o baseline.
    ///
    /// Delta = cenário − baseline para cada campo numérico.
    /// Percentual = (SaldoFimDelta / SaldoFimBaseline) × 100; zero quando baseline é zero.
    /// </summary>
    private static System.Collections.ObjectModel.ReadOnlyCollection<DeltaMensalDto> CalcularDeltasMensais(
        QuadroDividaProjecaoDto projecaoBaseline,
        QuadroDividaProjecaoDto projecaoCenario)
    {
        List<DeltaMensalDto> deltas = new(12);

        for (int i = 0; i < 12; i++)
        {
            MesProjecaoDto mesBaseline = projecaoBaseline.Meses[i];
            MesProjecaoDto mesCenario = projecaoCenario.Meses[i];

            decimal saldoFimDelta = mesCenario.SaldoTotalFim - mesBaseline.SaldoTotalFim;
            decimal captacaoDelta = mesCenario.TotalCaptacaoMes - mesBaseline.TotalCaptacaoMes;
            decimal amortizacaoDelta = mesCenario.TotalAmortizacaoMes - mesBaseline.TotalAmortizacaoMes;

            decimal saldoFimDeltaPercentual = mesBaseline.SaldoTotalFim == 0m
                ? 0m
                : Math.Round(
                    saldoFimDelta / mesBaseline.SaldoTotalFim * 100m,
                    4,
                    MidpointRounding.AwayFromZero);

            deltas.Add(new DeltaMensalDto(
                Mes: mesBaseline.Mes,
                SaldoFimDelta: saldoFimDelta,
                TotalCaptacaoDelta: captacaoDelta,
                TotalAmortizacaoDelta: amortizacaoDelta,
                SaldoFimDeltaPercentual: saldoFimDeltaPercentual));
        }

        return deltas.AsReadOnly();
    }

    /// <summary>
    /// Calcula o delta anual entre o cenário e o baseline.
    ///
    /// Usa os campos do <see cref="QuadroDividaSumarioDto"/> — garantindo consistência
    /// com o sumário exibido (não recalcula a partir dos meses individuais).
    /// </summary>
    private static DeltaAnualDto CalcularDeltaAnual(
        QuadroDividaSumarioDto sumarioBaseline,
        QuadroDividaSumarioDto sumarioCenario)
    {
        decimal saldoFimAnoDelta = sumarioCenario.SaldoTotalFimAno - sumarioBaseline.SaldoTotalFimAno;
        decimal captacaoAnoDelta = sumarioCenario.TotalCaptacaoNoAno - sumarioBaseline.TotalCaptacaoNoAno;

        decimal saldoFimAnoDeltaPercentual = sumarioBaseline.SaldoTotalFimAno == 0m
            ? 0m
            : Math.Round(
                saldoFimAnoDelta / sumarioBaseline.SaldoTotalFimAno * 100m,
                4,
                MidpointRounding.AwayFromZero);

        return new DeltaAnualDto(
            SaldoFimAnoDelta: saldoFimAnoDelta,
            TotalCaptacaoAnoDelta: captacaoAnoDelta,
            SaldoFimAnoDeltaPercentual: saldoFimAnoDeltaPercentual);
    }
}
