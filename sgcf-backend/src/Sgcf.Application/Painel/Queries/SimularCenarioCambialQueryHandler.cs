using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Calcula quatro cenários cambiais (customizado, pessimista, realista, otimista) de forma puramente funcional.
/// Toda a lógica de cálculo por cenário está em métodos estáticos sem I/O.
/// </summary>
public sealed class SimularCenarioCambialQueryHandler(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<SimularCenarioCambialQuery, ResultadoCenarioCambialDto>
{
    private const decimal DeltaPessimistaPct = -10m;
    private const decimal DeltaOtimistaPct = 10m;

    public async Task<ResultadoCenarioCambialDto> Handle(
        SimularCenarioCambialQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        IReadOnlyList<InstrumentoHedge> hedgesAtivos = await hedgeRepo.ListAtivosAsync(cancellationToken);

        IReadOnlyList<Contrato> contratosAtivos = contratos
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToList()
            .AsReadOnly();

        // Cotações base (spot ou PTAX fallback)
        IReadOnlySet<Moeda> moedasEstrangeiras = contratosAtivos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> cotacoesBase =
            await ResolverCotacoesBaseAsync(moedasEstrangeiras, hoje, cancellationToken);

        // Deltas por cenário
        Dictionary<Moeda, decimal> deltasCustomizados = MontarDeltas(query);
        Dictionary<Moeda, decimal> deltasPessimistas = MontarDeltasUniformes(moedasEstrangeiras, DeltaPessimistaPct);
        Dictionary<Moeda, decimal> deltasOtimistas = MontarDeltasUniformes(moedasEstrangeiras, DeltaOtimistaPct);
        Dictionary<Moeda, decimal> deltasRealistas = MontarDeltasUniformes(moedasEstrangeiras, 0m);

        CenarioDto realistaDto = ComputarCenario(
            "REALISTA", contratosAtivos, hedgesAtivos, cotacoesBase, deltasRealistas, cotacoesBase);

        CenarioDto pessimistDto = ComputarCenario(
            "PESSIMISTA", contratosAtivos, hedgesAtivos, cotacoesBase, deltasPessimistas, cotacoesBase);

        CenarioDto otimistDto = ComputarCenario(
            "OTIMISTA", contratosAtivos, hedgesAtivos, cotacoesBase, deltasOtimistas, cotacoesBase);

        CenarioDto customizadoDto = ComputarCenario(
            "CUSTOMIZADO", contratosAtivos, hedgesAtivos, cotacoesBase, deltasCustomizados, cotacoesBase);

        return new ResultadoCenarioCambialDto(
            CenarioCustomizado: customizadoDto,
            CenarioPessimista: pessimistDto,
            CenarioRealista: realistaDto,
            CenarioOtimista: otimistDto);
    }

    private static Dictionary<Moeda, decimal> MontarDeltas(SimularCenarioCambialQuery query)
    {
        Dictionary<Moeda, decimal> deltas = new();

        if (query.DeltaUsdPct.HasValue) { deltas[Moeda.Usd] = query.DeltaUsdPct.Value; }
        if (query.DeltaEurPct.HasValue) { deltas[Moeda.Eur] = query.DeltaEurPct.Value; }
        if (query.DeltaJpyPct.HasValue) { deltas[Moeda.Jpy] = query.DeltaJpyPct.Value; }
        if (query.DeltaCnyPct.HasValue) { deltas[Moeda.Cny] = query.DeltaCnyPct.Value; }

        return deltas;
    }

    private static Dictionary<Moeda, decimal> MontarDeltasUniformes(
        IReadOnlySet<Moeda> moedas,
        decimal deltaPct)
    {
        Dictionary<Moeda, decimal> deltas = new();
        foreach (Moeda moeda in moedas)
        {
            deltas[moeda] = deltaPct;
        }
        return deltas;
    }

    /// <summary>
    /// Cálculo puro de um cenário: aplica deltas sobre cotações base, computa saldos e MTM.
    /// Sem I/O — só matemática.
    /// </summary>
    private static CenarioDto ComputarCenario(
        string nome,
        IReadOnlyList<Contrato> contratos,
        IReadOnlyList<InstrumentoHedge> hedgesAtivos,
        Dictionary<Moeda, decimal> cotacoesBase,
        Dictionary<Moeda, decimal> deltas,
        Dictionary<Moeda, decimal> cotacoesRealistas)
    {
        // Aplica deltas
        Dictionary<Moeda, decimal> cotacoesEstressadas = new();
        foreach (KeyValuePair<Moeda, decimal> kvp in cotacoesBase)
        {
            decimal delta = deltas.TryGetValue(kvp.Key, out decimal d) ? d : 0m;
            cotacoesEstressadas[kvp.Key] = Math.Round(
                kvp.Value * (1m + delta / 100m),
                6,
                MidpointRounding.AwayFromZero);
        }

        // Calcula por moeda
        Dictionary<Moeda, (decimal saldoMoeda, decimal saldoBase, decimal saldoEstressado)> porMoeda = new();

        foreach (Contrato c in contratos)
        {
            if (c.Moeda == Moeda.Brl)
            {
                continue;
            }

            if (!porMoeda.TryGetValue(c.Moeda, out (decimal saldoMoeda, decimal saldoBase, decimal saldoEstressado) atual))
            {
                atual = (0m, 0m, 0m);
            }

            decimal saldoMoedaAdicional = c.ValorPrincipal.Valor;
            decimal taxaBase = cotacoesBase.TryGetValue(c.Moeda, out decimal tb) ? tb : 0m;
            decimal taxaEstressada = cotacoesEstressadas.TryGetValue(c.Moeda, out decimal te) ? te : 0m;

            porMoeda[c.Moeda] = (
                Math.Round(atual.saldoMoeda + saldoMoedaAdicional, 6, MidpointRounding.AwayFromZero),
                Math.Round(atual.saldoBase + saldoMoedaAdicional * taxaBase, 6, MidpointRounding.AwayFromZero),
                Math.Round(atual.saldoEstressado + saldoMoedaAdicional * taxaEstressada, 6, MidpointRounding.AwayFromZero));
        }

        // Inclui contratos BRL no total base
        decimal saldoBrlBase = Math.Round(
            contratos.Where(c => c.Moeda == Moeda.Brl).Sum(c => c.ValorPrincipal.Valor),
            6,
            MidpointRounding.AwayFromZero);

        decimal dividaBrutaBase = Math.Round(
            saldoBrlBase + porMoeda.Values.Sum(v => v.saldoBase),
            2,
            MidpointRounding.AwayFromZero);

        decimal dividaBrutaEstressada = Math.Round(
            saldoBrlBase + porMoeda.Values.Sum(v => v.saldoEstressado),
            2,
            MidpointRounding.AwayFromZero);

        // MTM sobre cotações estressadas
        decimal mtmLiquido = 0m;
        foreach (InstrumentoHedge hedge in hedgesAtivos)
        {
            decimal spot = hedge.MoedaBase == Moeda.Brl
                ? 1m
                : cotacoesEstressadas.TryGetValue(hedge.MoedaBase, out decimal s) ? s : 0m;

            decimal payoff = hedge.Tipo switch
            {
                TipoHedge.NdfForward => NdfMtmCalculador.CalcularMtmForward(
                    hedge.Notional.Valor, hedge.StrikeForward ?? 0m, spot),
                TipoHedge.NdfCollar => NdfMtmCalculador.CalcularMtmCollar(
                    hedge.Notional.Valor, hedge.StrikePut ?? 0m, hedge.StrikeCall ?? 0m, spot),
                _ => 0m
            };

            mtmLiquido = Math.Round(mtmLiquido + payoff, 6, MidpointRounding.AwayFromZero);
        }

        decimal dividaLiquidaEstressada = Math.Round(
            dividaBrutaEstressada + mtmLiquido,
            2,
            MidpointRounding.AwayFromZero);

        decimal deltaVsRealistaBrl = Math.Round(
            dividaBrutaEstressada - dividaBrutaBase,
            2,
            MidpointRounding.AwayFromZero);

        // Monta breakdown por moeda
        List<LinhaCenarioMoedaDto> breakdown = porMoeda
            .Select(kvp =>
            {
                decimal cotBase = cotacoesBase.TryGetValue(kvp.Key, out decimal b) ? b : 0m;
                decimal cotEstressada = cotacoesEstressadas.TryGetValue(kvp.Key, out decimal e) ? e : 0m;
                decimal deltaPct = deltas.TryGetValue(kvp.Key, out decimal d) ? d : 0m;
                decimal impacto = Math.Round(kvp.Value.saldoEstressado - kvp.Value.saldoBase, 2, MidpointRounding.AwayFromZero);

                return new LinhaCenarioMoedaDto(
                    Moeda: kvp.Key.ToString().ToUpperInvariant(),
                    CotacaoBase: Math.Round(cotBase, 6, MidpointRounding.AwayFromZero),
                    CotacaoEstressada: Math.Round(cotEstressada, 6, MidpointRounding.AwayFromZero),
                    DeltaPct: deltaPct,
                    SaldoBrlBase: Math.Round(kvp.Value.saldoBase, 2, MidpointRounding.AwayFromZero),
                    SaldoBrlEstressado: Math.Round(kvp.Value.saldoEstressado, 2, MidpointRounding.AwayFromZero),
                    ImpactoBrl: impacto);
            })
            .ToList();

        return new CenarioDto(
            Nome: nome,
            BreakdownPorMoeda: breakdown.AsReadOnly(),
            DividaBrutaBrl: dividaBrutaEstressada,
            DividaLiquidaBrl: dividaLiquidaEstressada,
            DeltaVsRealistaBrl: deltaVsRealistaBrl);
    }

    private async Task<Dictionary<Moeda, decimal>> ResolverCotacoesBaseAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate hoje,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> resultado = new();

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                resultado[moeda] = spot.Value.Valor;
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
                resultado[moeda] = midRate;
            }
        }

        return resultado;
    }
}
