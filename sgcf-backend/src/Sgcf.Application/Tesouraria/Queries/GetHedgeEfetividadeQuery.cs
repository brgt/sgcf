using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Tesouraria.Queries;

/// <summary>Solicita o consolidado de efetividade de hedge por moeda.</summary>
public sealed record GetHedgeEfetividadeQuery : IRequest<EnvelopeResponse<HedgeEfetividadeDto>>;

/// <summary>
/// Consolida exposição cambial (contratos em moeda estrangeira) com cobertura de hedge (instrumentos
/// NDF ativos) e MtM mais recente armazenado em <see cref="PosicaoSnapshot"/>.
///
/// Estratégia de cotação para conversão de nocional em BRL:
///   1. Tenta spot intraday via <see cref="ICotacaoSpotCache"/>.
///   2. Na ausência de spot, cai para PTAX D-1 via <see cref="ICotacaoFxRepository"/>.
///   3. Se nem PTAX disponível, a exposição/cobertura nessa moeda contribui com zero BRL
///      e a completude do envelope é marcada como <see cref="Completude.Parcial"/>.
///
/// MtM vem do último <see cref="PosicaoSnapshot"/> registrado para cada instrumento.
/// Se não há snapshot, o MtM do instrumento é considerado zero.
/// </summary>
public sealed class GetHedgeEfetividadeQueryHandler(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    ICotacaoSpotCache spotCache,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock)
    : IRequestHandler<GetHedgeEfetividadeQuery, EnvelopeResponse<HedgeEfetividadeDto>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<HedgeEfetividadeDto>> Handle(
        GetHedgeEfetividadeQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();
        // Datas de calendário brasileiro derivam do fuso BRT, não UTC.
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        // ── Dados brutos ────────────────────────────────────────────────────────
        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        IReadOnlyList<InstrumentoHedge> hedgesAtivos = await hedgeRepo.ListAtivosAsync(cancellationToken);

        // Apenas contratos ativos em moeda estrangeira geram exposição cambial.
        List<Contrato> contratosEstrangeiros = contratos
            .Where(c => c.Status == StatusContrato.Ativo && c.Moeda != Moeda.Brl)
            .ToList();

        // ── Cotações ────────────────────────────────────────────────────────────
        HashSet<Moeda> moedasExpostas = contratosEstrangeiros
            .Select(c => c.Moeda)
            .ToHashSet();

        HashSet<Moeda> moedasHedge = hedgesAtivos
            .Select(h => h.MoedaBase)
            .Where(m => m != Moeda.Brl)
            .ToHashSet();

        // Resolve para todas as moedas de uma vez (exposição + hedge podem diferir).
        HashSet<Moeda> todasMoedas = moedasExpostas.Union(moedasHedge).ToHashSet();
        (Dictionary<Moeda, decimal> taxas, bool cotacaoIncompleta) =
            await ResolverTaxasAsync(todasMoedas, hoje, cancellationToken);

        // ── Exposição por moeda ─────────────────────────────────────────────────
        Dictionary<Moeda, decimal> exposicaoPorMoeda = contratosEstrangeiros
            .GroupBy(c => c.Moeda)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    decimal taxa = taxas.GetValueOrDefault(g.Key, 0m);
                    decimal saldoMoedaOriginal = g.Sum(c => c.ValorPrincipal.Valor);
                    return Math.Round(saldoMoedaOriginal * taxa, 6, MidpointRounding.AwayFromZero);
                });

        // ── MtM por instrumento (via snapshot persistido) ────────────────────────
        // Busca snapshots em paralelo para reduzir latência em carteiras grandes.
        HedgeInstrumentoResumoDto[] instrumentosResumo =
            await ResolverInstrumentosAsync(hedgesAtivos, taxas, cancellationToken);

        // ── Agrupamento por moeda ───────────────────────────────────────────────
        Dictionary<Moeda, List<HedgeInstrumentoResumoDto>> instrumentosPorMoeda =
            new();

        for (int i = 0; i < hedgesAtivos.Count; i++)
        {
            Moeda moeda = hedgesAtivos[i].MoedaBase;
            if (!instrumentosPorMoeda.TryGetValue(moeda, out List<HedgeInstrumentoResumoDto>? lista))
            {
                lista = new List<HedgeInstrumentoResumoDto>();
                instrumentosPorMoeda[moeda] = lista;
            }

            lista.Add(instrumentosResumo[i]);
        }

        // ── Constrói quebra por moeda ───────────────────────────────────────────
        // Agrupa pela union de moedas com exposição ou cobertura.
        IEnumerable<Moeda> todasMoedasPresentes = exposicaoPorMoeda.Keys
            .Union(instrumentosPorMoeda.Keys);

        List<HedgeEfetividadeMoedaDto> porMoeda = new();

        foreach (Moeda moeda in todasMoedasPresentes)
        {
            decimal exposicaoBrl = exposicaoPorMoeda.GetValueOrDefault(moeda, 0m);
            List<HedgeInstrumentoResumoDto> instrumentos =
                instrumentosPorMoeda.GetValueOrDefault(moeda, []);

            decimal coberturaBrl = Math.Round(
                instrumentos.Sum(i => i.NocionalBrl), 6, MidpointRounding.AwayFromZero);

            decimal mtmBrl = Math.Round(
                instrumentos.Sum(i => i.MtmBrl), 6, MidpointRounding.AwayFromZero);

            decimal taxaCobertura = CalcularTaxaCobertura(exposicaoBrl, coberturaBrl);

            porMoeda.Add(new HedgeEfetividadeMoedaDto(
                Moeda: moeda.ToString().ToUpperInvariant(),
                ExposicaoBrl: Math.Round(exposicaoBrl, 2, MidpointRounding.AwayFromZero),
                CoberturaHedgeBrl: Math.Round(coberturaBrl, 2, MidpointRounding.AwayFromZero),
                TaxaCoberturaPct: Math.Round(taxaCobertura, 2, MidpointRounding.AwayFromZero),
                MtmBrl: Math.Round(mtmBrl, 2, MidpointRounding.AwayFromZero),
                Instrumentos: instrumentos.AsReadOnly()));
        }

        // ── Totais consolidados ─────────────────────────────────────────────────
        decimal exposicaoTotalBrl = Math.Round(
            porMoeda.Sum(m => m.ExposicaoBrl), 2, MidpointRounding.AwayFromZero);

        decimal coberturaHedgeTotalBrl = Math.Round(
            porMoeda.Sum(m => m.CoberturaHedgeBrl), 2, MidpointRounding.AwayFromZero);

        decimal ajusteMtmTotalBrl = Math.Round(
            instrumentosResumo.Sum(i => i.MtmBrl), 2, MidpointRounding.AwayFromZero);

        decimal taxaCoberturaTotalPct =
            CalcularTaxaCobertura(exposicaoTotalBrl, coberturaHedgeTotalBrl);

        HedgeEfetividadeDto dto = new(
            PorMoeda: porMoeda.AsReadOnly(),
            ExposicaoTotalBrl: exposicaoTotalBrl,
            CoberturaHedgeBrl: coberturaHedgeTotalBrl,
            TaxaCoberturaPct: Math.Round(taxaCoberturaTotalPct, 2, MidpointRounding.AwayFromZero),
            AjusteMtmTotalBrl: ajusteMtmTotalBrl);

        Completude completude = cotacaoIncompleta ? Completude.Parcial : Completude.Completo;

        EnvelopeMeta meta = new(
            DataHoraCalculo: agora,
            FontesConsultadas:
            [
                new FonteConsultada("banco_de_dados", "ok", contratos.Count + hedgesAtivos.Count),
                new FonteConsultada("cotacoes_fx", cotacaoIncompleta ? "parcial" : "ok", todasMoedas.Count)
            ],
            Completude: completude);

        return new EnvelopeResponse<HedgeEfetividadeDto>(dto, meta);
    }

    /// <summary>
    /// Resolve taxas de câmbio para o conjunto de moedas informado.
    /// Retorna o dicionário de taxas e um flag indicando se alguma moeda ficou sem cotação.
    /// </summary>
    private async Task<(Dictionary<Moeda, decimal> Taxas, bool Incompleta)> ResolverTaxasAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate hoje,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> taxas = new();
        bool incompleta = false;

        foreach (Moeda moeda in moedas)
        {
            // BRL não precisa de conversão.
            if (moeda == Moeda.Brl)
            {
                taxas[moeda] = 1m;
                continue;
            }

            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                taxas[moeda] = spot.Value.Valor;
                continue;
            }

            CotacaoFx? ptax = await cotacaoResolver.ResolverFxAsync(
                moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

            if (ptax is not null)
            {
                // Mid-rate (média entre compra e venda) — mesmo padrão do GetPainelDividaQueryHandler.
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);

                taxas[moeda] = midRate;
                continue;
            }

            // Sem cotação disponível: contribui com zero e sinaliza completude parcial.
            incompleta = true;
        }

        return (taxas, incompleta);
    }

    /// <summary>
    /// Monta os resumos de instrumentos usando o MtM do último snapshot persistido.
    /// Quando não há snapshot, o MtM é zero (instrumento ainda não calculado).
    /// O array retornado preserva a mesma ordem de <paramref name="hedgesAtivos"/>.
    /// </summary>
    private async Task<HedgeInstrumentoResumoDto[]> ResolverInstrumentosAsync(
        IReadOnlyList<InstrumentoHedge> hedgesAtivos,
        Dictionary<Moeda, decimal> taxas,
        CancellationToken cancellationToken)
    {
        HedgeInstrumentoResumoDto[] resultado = new HedgeInstrumentoResumoDto[hedgesAtivos.Count];

        for (int i = 0; i < hedgesAtivos.Count; i++)
        {
            InstrumentoHedge hedge = hedgesAtivos[i];

            PosicaoSnapshot? snapshot =
                await hedgeRepo.GetSnapshotMaisRecenteAsync(hedge.Id, cancellationToken);

            decimal mtmBrl = snapshot is not null
                ? Math.Round(snapshot.MtmBrl.Valor, 6, MidpointRounding.AwayFromZero)
                : 0m;

            // Nocional em BRL = Notional × taxa spot ou PTAX D-1.
            decimal taxa = taxas.GetValueOrDefault(hedge.MoedaBase, 0m);
            decimal nocionalBrl = Math.Round(
                hedge.Notional.Valor * taxa, 6, MidpointRounding.AwayFromZero);

            resultado[i] = new HedgeInstrumentoResumoDto(
                Id: hedge.Id,
                Tipo: hedge.Tipo.ToString(),
                NocionalBrl: Math.Round(nocionalBrl, 2, MidpointRounding.AwayFromZero),
                MtmBrl: Math.Round(mtmBrl, 2, MidpointRounding.AwayFromZero),
                DataVencimento: hedge.DataVencimento);
        }

        return resultado;
    }

    /// <summary>
    /// Calcula a taxa de cobertura do hedge em percentual.
    /// Retorna zero quando a exposição é zero para evitar divisão por zero.
    /// </summary>
    private static decimal CalcularTaxaCobertura(decimal exposicaoBrl, decimal coberturaBrl)
    {
        if (exposicaoBrl == 0m)
        {
            return 0m;
        }

        return Math.Round(coberturaBrl / exposicaoBrl * 100m, 6, MidpointRounding.AwayFromZero);
    }
}
