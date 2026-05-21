using FluentValidation;
using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos.Queries;

/// <summary>
/// Retorna a sensibilidade do portfólio ativo a uma variação hipotética nos indexadores de taxa de juros.
/// O cálculo é feito server-side para evitar drift com implementações no front-end.
/// </summary>
/// <param name="DeltaBps">Variação hipotética em basis points. Padrão: 100 bps (+1%).</param>
public sealed record GetSensibilidadeIndexadoresQuery(int DeltaBps = 100)
    : IRequest<EnvelopeResponse<SensibilidadeIndexadoresDto>>;

/// <summary>
/// Handler para <see cref="GetSensibilidadeIndexadoresQuery"/>.
///
/// Estratégia de indexador:
/// - Moeda == BRL  → "CDI"  (contratos domésticos seguem tipicamente CDI + spread)
/// - Moeda != BRL  → "SOFR" (proxy para taxa flutuante estrangeira)
///
/// NOTA: esta classificação é uma aproximação baseada na moeda. A tarefa 4.4
/// adicionará o campo explícito <c>TipoIndicadorTaxa</c> ao <c>Contrato</c>.
///
/// Estratégia de FX:
/// 1. Spot intraday via <see cref="ICotacaoSpotCache"/> (Redis)
/// 2. PTAX D-1 via <see cref="ICotacaoFxRepository"/> como fallback
/// 3. Taxa zero se nenhuma cotação estiver disponível (degrada completude para Parcial)
/// </summary>
public sealed class GetSensibilidadeIndexadoresQueryHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository eventoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetSensibilidadeIndexadoresQuery, EnvelopeResponse<SensibilidadeIndexadoresDto>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<SensibilidadeIndexadoresDto>> Handle(
        GetSensibilidadeIndexadoresQuery query,
        CancellationToken cancellationToken)
    {
        ValidarDeltaBps(query.DeltaBps);

        Instant agora = clock.GetCurrentInstant();
        LocalDate dataHoje = agora.InZone(FusoBrasilia).Date;

        // 1. Carrega contratos ativos: (Id, ValorPrincipal decimal, Moeda)
        IReadOnlyList<(Guid Id, decimal ValorPrincipal, Moeda Moeda)> contratosAtivos =
            await contratoRepo.ListAtivosValoresPrincipaisAsync(cancellationToken);

        if (contratosAtivos.Count == 0)
        {
            return ConstruirRespostaVazia(query.DeltaBps, agora);
        }

        // 2. Carrega eventos futuros para calcular saldo devedor real
        IReadOnlyList<EventoCronograma> eventosFuturos =
            await eventoRepo.ListPrevistosNoPeriodoAsync(
                dataHoje, dataHoje.PlusYears(100), cancellationToken);

        // 3. Soma saldo devedor por contrato a partir de eventos Principal futuros
        Dictionary<Guid, decimal> saldoDevedorPorContrato = eventosFuturos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .GroupBy(e => e.ContratoId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => e.ValorMoedaOriginal.Valor));

        // 4. Resolve cotações FX para moedas estrangeiras presentes
        IReadOnlySet<Moeda> moedasEstrangeiras = contratosAtivos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        (Dictionary<Moeda, decimal> cotacoes, bool hasCotacaoFallback, bool hasCotacaoAusente) =
            await ResolverCotacoesAsync(moedasEstrangeiras, dataHoje, cancellationToken);

        // 5. Classifica contratos por indexador e calcula saldo em BRL
        var grupos = new Dictionary<string, GrupoIndexador>(StringComparer.Ordinal);

        foreach ((Guid id, decimal valorPrincipalOriginal, Moeda moeda) in contratosAtivos)
        {
            // Prefere saldo devedor de eventos futuros; cai para valor principal se não houver cronograma
            decimal saldoMoedaOriginal = saldoDevedorPorContrato.TryGetValue(id, out decimal saldoEvento)
                ? saldoEvento
                : valorPrincipalOriginal;

            decimal taxaConversao = moeda == Moeda.Brl
                ? 1m
                : cotacoes.TryGetValue(moeda, out decimal taxa) ? taxa : 0m;

            decimal saldoBrl = Math.Round(saldoMoedaOriginal * taxaConversao, 2, MidpointRounding.AwayFromZero);

            // Classificação por indexador via moeda (aproximação — ver nota no header do handler)
            string indexador = ClassificarIndexador(moeda);

            if (!grupos.TryGetValue(indexador, out GrupoIndexador? grupo))
            {
                grupo = new GrupoIndexador(indexador);
                grupos[indexador] = grupo;
            }

            grupo.AdicionarContrato(saldoBrl);
        }

        // 6. Monta DTOs por indexador
        decimal totalBrl = grupos.Values.Sum(g => g.SaldoDevedorBrl);
        decimal deltaTotalBrl = CalcularDelta(totalBrl, query.DeltaBps);

        List<SensibilidadePorIndexadorDto> porIndexador = grupos.Values
            .OrderBy(g => g.Indexador)
            .Select(g =>
            {
                decimal deltaBrl = CalcularDelta(g.SaldoDevedorBrl, query.DeltaBps);
                decimal deltaPercentual = deltaTotalBrl > 0m
                    ? Math.Round(deltaBrl / deltaTotalBrl * 100m, 4, MidpointRounding.AwayFromZero)
                    : 0m;

                return new SensibilidadePorIndexadorDto(
                    Indexador: g.Indexador,
                    QuantidadeContratos: g.QuantidadeContratos,
                    SaldoDevedorBrl: Math.Round(g.SaldoDevedorBrl, 2, MidpointRounding.AwayFromZero),
                    DeltaCustoAnualBrl: Math.Round(deltaBrl, 2, MidpointRounding.AwayFromZero),
                    DeltaCustoAnualPercentual: deltaPercentual);
            })
            .ToList();

        SensibilidadeIndexadoresDto dados = new(
            DeltaBps: query.DeltaBps,
            SaldoDevedorTotalBrl: Math.Round(totalBrl, 2, MidpointRounding.AwayFromZero),
            DeltaCustoAnualTotalBrl: Math.Round(deltaTotalBrl, 2, MidpointRounding.AwayFromZero),
            PorIndexador: porIndexador.AsReadOnly());

        // 7. Monta metadados de completude e fontes consultadas
        Completude completude = DeterminarCompletude(hasCotacaoFallback, hasCotacaoAusente);
        List<FonteConsultada> fontes = MontarFontes(
            contratosAtivos.Count, eventosFuturos.Count, hasCotacaoFallback, hasCotacaoAusente);

        EnvelopeMeta meta = new(
            DataHoraCalculo: agora,
            FontesConsultadas: fontes.AsReadOnly(),
            Completude: completude);

        return new EnvelopeResponse<SensibilidadeIndexadoresDto>(dados, meta);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static void ValidarDeltaBps(int deltaBps)
    {
        if (deltaBps is < 1 or > 10_000)
        {
            throw new ValidationException(
                $"deltaBps deve ser entre 1 e 10000. Valor recebido: {deltaBps}.");
        }
    }

    private static string ClassificarIndexador(Moeda moeda) =>
        // Aproximação baseada em moeda: BRL → CDI, moeda estrangeira → SOFR (proxy).
        // Task 4.4 adicionará TipoIndicadorTaxa explícito ao Contrato.
        moeda == Moeda.Brl ? "CDI" : "SOFR";

    private static decimal CalcularDelta(decimal saldoBrl, int deltaBps) =>
        Math.Round(saldoBrl * deltaBps / 10_000m, 2, MidpointRounding.AwayFromZero);

    private async Task<(Dictionary<Moeda, decimal> cotacoes, bool hasFallback, bool hasAusente)>
        ResolverCotacoesAsync(
            IReadOnlySet<Moeda> moedas,
            LocalDate hoje,
            CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> cotacoes = new();
        bool hasFallback = false;
        bool hasAusente = false;

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                cotacoes[moeda] = spot.Value.Valor;
                continue;
            }

            CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
                moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

            if (ptax is not null)
            {
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
                cotacoes[moeda] = midRate;
                hasFallback = true;
            }
            else
            {
                // Taxa zero: contrato contribuirá com zero saldo BRL
                hasAusente = true;
            }
        }

        return (cotacoes, hasFallback, hasAusente);
    }

    private static Completude DeterminarCompletude(bool hasFallback, bool hasAusente)
    {
        if (hasAusente)
        {
            return Completude.Degradado;
        }

        return hasFallback ? Completude.Parcial : Completude.Completo;
    }

    private static List<FonteConsultada> MontarFontes(
        int qtdContratos,
        int qtdEventos,
        bool hasFallback,
        bool hasAusente)
    {
        string statusFx = hasAusente ? "COTACAO_AUSENTE" : hasFallback ? "PTAX_D1_FALLBACK" : "SPOT_INTRADAY";

        return
        [
            new FonteConsultada("ContratoRepository", "OK", qtdContratos),
            new FonteConsultada("EventoCronogramaRepository", "OK", qtdEventos),
            new FonteConsultada("CotacaoFx", statusFx, null),
        ];
    }

    private static EnvelopeResponse<SensibilidadeIndexadoresDto> ConstruirRespostaVazia(
        int deltaBps,
        Instant agora)
    {
        SensibilidadeIndexadoresDto dados = new(
            DeltaBps: deltaBps,
            SaldoDevedorTotalBrl: 0m,
            DeltaCustoAnualTotalBrl: 0m,
            PorIndexador: Array.Empty<SensibilidadePorIndexadorDto>());

        EnvelopeMeta meta = new(
            DataHoraCalculo: agora,
            FontesConsultadas: new[]
            {
                new FonteConsultada("ContratoRepository", "OK", 0),
            },
            Completude: Completude.Completo);

        return new EnvelopeResponse<SensibilidadeIndexadoresDto>(dados, meta);
    }

    /// <summary>
    /// Acumulador mutável para a fase de agregação por indexador.
    /// Somente usado internamente no handler; não exposto ao caller.
    /// </summary>
    private sealed class GrupoIndexador(string indexador)
    {
        public string Indexador { get; } = indexador;
        public int QuantidadeContratos { get; private set; }
        public decimal SaldoDevedorBrl { get; private set; }

        public void AdicionarContrato(decimal saldoBrl)
        {
            QuantidadeContratos++;
            SaldoDevedorBrl = Math.Round(SaldoDevedorBrl + saldoBrl, 6, MidpointRounding.AwayFromZero);
        }
    }
}
