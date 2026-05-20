using MediatR;

using NodaTime;

using Sgcf.Application.Common;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Queries;

// Os DTOs CronogramaHipoteticoDto e EventoCronogramaItemDto são definidos em
// Sgcf.Application.Simulacao.Dtos.CronogramaHipoteticoDto.cs (criado pela Task 2.4b como stub).
// Este arquivo contém apenas a Query e o Handler.

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Calcula o cronograma hipotético de uma simulação de contratação sem persistir nada.
/// Pure compute: o handler constrói uma <see cref="SimulacaoContratacao"/> temporária,
/// delega ao <see cref="SimulacaoCronogramaCalculator"/> e descarta a entidade.
///
/// <para>
/// <b>Uso:</b> o frontend chama este endpoint antes de salvar a simulação para
/// pré-visualizar o fluxo financeiro. Pode ser chamado sem cenário existente.
/// </para>
///
/// <para>
/// <b>Erros:</b>
/// <list type="bullet">
///   <item><see cref="ArgumentException"/> — invariante de domínio violada (I-1..I-11) ou
///         CDI+Spread sem <paramref name="CdiReferenciaAaPercentual"/>. Controller → 400.</item>
///   <item><see cref="InvalidOperationException"/> — estado interno inválido (improvável via esta query).
///         Controller → 409.</item>
/// </list>
/// </para>
/// </summary>
/// <param name="Simulacao">Todos os campos de uma <see cref="SimulacaoContratacao"/>, sem CenarioId.</param>
/// <param name="CdiReferenciaAaPercentual">
/// CDI vigente em % a.a. (ex: 10.50). Obrigatório quando <c>TipoTaxa = CdiSpread</c>.
/// </param>
public sealed record SimularCronogramaHipoteticoQuery(
    AdicionarSimulacaoInput Simulacao,
    decimal? CdiReferenciaAaPercentual = null) : IRequest<CronogramaHipoteticoDto>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Handler da <see cref="SimularCronogramaHipoteticoQuery"/>.
///
/// <para>
/// Fluxo:
/// 1. Parseia os campos string do DTO de entrada para os tipos do domínio.
/// 2. Constrói uma <see cref="SimulacaoContratacao"/> temporária via factory —
///    sem CenarioId real (<see cref="Guid.Empty"/>) pois é hipotética.
/// 3. Chama <see cref="SimulacaoCronogramaCalculator.Calcular"/> (pure function).
/// 4. Mapeia os eventos para <see cref="EventoCronogramaItemDto"/> e calcula sumário.
/// </para>
///
/// <para>
/// O método <see cref="ConstruirSimulacao"/> é <c>public static</c> para permitir
/// que os testes unitários construam a simulação de referência com os mesmos parâmetros
/// e comparem bit-a-bit com o resultado do handler (garantia AD-4 para o endpoint).
/// </para>
/// </summary>
public sealed class SimularCronogramaHipoteticoQueryHandler(IClock clock)
    : IRequestHandler<SimularCronogramaHipoteticoQuery, CronogramaHipoteticoDto>
{
    /// <inheritdoc/>
    public Task<CronogramaHipoteticoDto> Handle(
        SimularCronogramaHipoteticoQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Construir SimulacaoContratacao temporária — valida invariantes I-1..I-11
        SimulacaoContratacao simulacao = ConstruirSimulacao(query.Simulacao, clock);

        // 2. Calcular cronograma (pode lançar ArgumentException quando CDI+Spread sem CDI)
        IReadOnlyList<EventoCronogramaGerado> eventos =
            SimulacaoCronogramaCalculator.Calcular(simulacao, query.CdiReferenciaAaPercentual);

        // 3. Resolver taxa efetiva para incluir no DTO
        decimal taxaEfetiva = ResolverTaxaEfetivaHumana(simulacao, query.CdiReferenciaAaPercentual);

        // 4. Mapear para DTOs e calcular sumário
        // EventoCronogramaItemDto está em Sgcf.Application.Simulacao.Dtos
        List<EventoCronogramaItemDto> eventosDto = eventos
            .Select(e => new EventoCronogramaItemDto(
                Numero: e.NumeroEvento,
                Tipo: e.Tipo.ToString(),
                Data: new DateOnly(e.DataPrevista.Year, e.DataPrevista.Month, e.DataPrevista.Day),
                Valor: DecimalArredondamento.Mostrar(e.Valor.Valor),
                SaldoDevedorApos: DecimalArredondamento.Mostrar(e.SaldoDevedorApos)))
            .ToList();

        string principalStr = TipoEventoCronograma.Principal.ToString();
        string jurosStr = TipoEventoCronograma.Juros.ToString();

        decimal principalTotal = eventosDto
            .Where(e => e.Tipo == principalStr)
            .Sum(e => e.Valor);

        decimal jurosTotal = eventosDto
            .Where(e => e.Tipo == jurosStr)
            .Sum(e => e.Valor);

        // CronogramaHipoteticoDto está em Sgcf.Application.Simulacao.Dtos
        CronogramaHipoteticoDto resultado = new(
            TaxaEfetivaAaPercentual: DecimalArredondamento.Mostrar(taxaEfetiva),
            QuantidadeEventos: eventosDto.Count,
            PrincipalTotal: DecimalArredondamento.Mostrar(principalTotal),
            JurosTotal: DecimalArredondamento.Mostrar(jurosTotal),
            Eventos: eventosDto);

        return Task.FromResult(resultado);
    }

    /// <summary>
    /// Constrói a entidade de domínio temporária a partir do DTO de input.
    /// Parseia strings para enums usando <see cref="Enum.Parse{T}"/> com <c>ignoreCase: true</c>.
    /// Lança <see cref="ArgumentException"/> se qualquer invariante I-1..I-11 for violada.
    /// </summary>
    /// <remarks>
    /// <c>public static</c>: exposto para testes unitários que precisam construir
    /// a simulação de referência com os mesmos inputs e comparar bit-a-bit (garantia AD-4).
    /// </remarks>
    public static SimulacaoContratacao ConstruirSimulacao(
        AdicionarSimulacaoInput input,
        IClock clock)
    {
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(input.Modalidade, ignoreCase: true);
        Moeda moeda = Enum.Parse<Moeda>(input.Moeda, ignoreCase: true);
        TipoTaxa tipoTaxa = Enum.Parse<TipoTaxa>(input.TipoTaxa, ignoreCase: true);
        BaseCalculo baseCalculo = Enum.Parse<BaseCalculo>(input.BaseCalculo, ignoreCase: true);
        EstruturaAmortizacao estrutura = Enum.Parse<EstruturaAmortizacao>(input.EstruturaAmortizacao, ignoreCase: true);
        Periodicidade periodicidade = Enum.Parse<Periodicidade>(input.Periodicidade, ignoreCase: true);
        AnchorDiaMes anchorDiaMes = Enum.Parse<AnchorDiaMes>(input.AnchorDiaMes, ignoreCase: true);

        LocalDate dataContratacao = new(
            input.DataContratacaoPrevista.Year,
            input.DataContratacaoPrevista.Month,
            input.DataContratacaoPrevista.Day);

        LocalDate dataPrimeiroVencimento = new(
            input.DataPrimeiroVencimento.Year,
            input.DataPrimeiroVencimento.Month,
            input.DataPrimeiroVencimento.Day);

        Percentual? taxaAa = input.TaxaAa.HasValue ? Percentual.De(input.TaxaAa.Value) : null;
        Percentual? spreadAa = input.SpreadAa.HasValue ? Percentual.De(input.SpreadAa.Value) : null;

        // Guid.Empty como cenarioId — hipotético, sem cenário persistido associado
        return SimulacaoContratacao.Criar(
            cenarioId: Guid.Empty,
            bancoId: input.BancoId,
            modalidade: modalidade,
            moeda: moeda,
            valorPrincipal: new Money(input.ValorPrincipal, moeda),
            dataContratacaoPrevista: dataContratacao,
            dataPrimeiroVencimento: dataPrimeiroVencimento,
            tipoTaxa: tipoTaxa,
            taxaAa: taxaAa,
            spreadAa: spreadAa,
            baseCalculo: baseCalculo,
            estruturaAmortizacao: estrutura,
            periodicidade: periodicidade,
            quantidadeParcelas: input.QuantidadeParcelas,
            anchorDiaMes: anchorDiaMes,
            anchorDiaFixo: input.AnchorDiaFixo,
            garantiaExigidaPrevista: input.GarantiaExigidaPrevista,
            observacoes: input.Observacoes,
            clock: clock,
            anoBase: null); // preview hipotético não valida ano-base (invariante I-4)
    }

    /// <summary>
    /// Resolve a taxa efetiva em % a.a. (valor "humano") para incluir no DTO.
    /// Para taxa fixa: retorna TaxaAa.AsHumano diretamente.
    /// Para CDI+Spread: aplica composição (1+CDI)×(1+spread)-1 convertida para %.
    /// </summary>
    private static decimal ResolverTaxaEfetivaHumana(
        SimulacaoContratacao simulacao,
        decimal? cdiReferenciaAaPercentual)
    {
        if (simulacao.TipoTaxa == TipoTaxa.Fixa)
        {
            // Invariante I-6 garante TaxaAa não-nulo quando TipoTaxa = Fixa
            return simulacao.TaxaAa!.Value.AsHumano;
        }

        // Para CdiSpread o calculator já validou que cdiReferenciaAaPercentual não é null.
        // Se chegamos aqui é porque o Calcular() não lançou — CDI está disponível.
        decimal cdi = cdiReferenciaAaPercentual!.Value / 100m;
        decimal spread = simulacao.SpreadAa!.Value.AsDecimal;
        return ((1m + cdi) * (1m + spread) - 1m) * 100m;
    }
}
