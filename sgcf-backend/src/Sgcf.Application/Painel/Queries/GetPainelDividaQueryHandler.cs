using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Monta o painel consolidado de dívida em BRL.
/// Estratégia de cotação: tenta spot intraday via Redis; na falha cai para PTAX D-1.
/// </summary>
public sealed class GetPainelDividaQueryHandler(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetPainelDividaQuery, PainelDividaDto>
{
    public async Task<PainelDividaDto> Handle(
        GetPainelDividaQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        IReadOnlyList<InstrumentoHedge> hedgesAtivos = await hedgeRepo.ListAtivosAsync(cancellationToken);

        contratos = AplicarFiltros(contratos, query);

        // Resolve cotações para todas as moedas estrangeiras presentes
        IReadOnlySet<Moeda> moedasEstrangeiras = contratos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, (decimal taxa, bool ehSpot)> cotacoes =
            await ResolverCotacoesAsync(moedasEstrangeiras, hoje, cancellationToken);

        bool todosSpot = cotacoes.Values.All(v => v.ehSpot);
        string tipoCotacao = todosSpot ? "SPOT_INTRADAY" : "PTAX_D1_FALLBACK";

        // Agrupa contratos por moeda
        Dictionary<Moeda, List<Contrato>> porMoeda = contratos
            .GroupBy(c => c.Moeda)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<LinhaBreakdownMoedaDto> breakdown = new();

        foreach (KeyValuePair<Moeda, List<Contrato>> entrada in porMoeda)
        {
            Moeda moeda = entrada.Key;
            List<Contrato> grupo = entrada.Value;

            decimal taxaConversao = moeda == Moeda.Brl
                ? 1m
                : cotacoes.TryGetValue(moeda, out (decimal taxa, bool ehSpot) cotacao) ? cotacao.taxa : 0m;

            decimal saldoMoedaOriginal = Math.Round(
                grupo.Sum(c => c.ValorPrincipal.Valor),
                6,
                MidpointRounding.AwayFromZero);

            decimal saldoBrl = Math.Round(
                saldoMoedaOriginal * taxaConversao,
                6,
                MidpointRounding.AwayFromZero);

            breakdown.Add(new LinhaBreakdownMoedaDto(
                Moeda: moeda.ToString().ToUpperInvariant(),
                SaldoMoedaOriginal: Math.Round(saldoMoedaOriginal, 2, MidpointRounding.AwayFromZero),
                CotacaoAplicada: Math.Round(taxaConversao, 6, MidpointRounding.AwayFromZero),
                SaldoBrl: Math.Round(saldoBrl, 2, MidpointRounding.AwayFromZero),
                QuantidadeContratos: grupo.Count));
        }

        decimal dividaBrutaBrl = Math.Round(
            breakdown.Sum(b => b.SaldoBrl),
            2,
            MidpointRounding.AwayFromZero);

        // Calcula MTM dos hedges ativos
        IReadOnlySet<Moeda> moedasHedge = hedgesAtivos
            .Select(h => h.MoedaBase)
            .Where(m => m != Moeda.Brl)
            .ToHashSet();

        Dictionary<Moeda, (decimal taxa, bool ehSpot)> cotacoesHedge =
            await ResolverCotacoesAsync(moedasHedge, hoje, cancellationToken);

        decimal mtmAReceber = 0m;
        decimal mtmAPagar = 0m;

        foreach (InstrumentoHedge hedge in hedgesAtivos)
        {
            decimal spot = hedge.MoedaBase == Moeda.Brl
                ? 1m
                : cotacoesHedge.TryGetValue(hedge.MoedaBase, out (decimal taxa, bool ehSpot) c) ? c.taxa : 0m;

            decimal mtm = hedge.Tipo switch
            {
                TipoHedge.NdfForward => NdfMtmCalculador.CalcularMtmForward(
                    hedge.Notional.Valor,
                    hedge.StrikeForward ?? 0m,
                    spot),
                TipoHedge.NdfCollar => NdfMtmCalculador.CalcularMtmCollar(
                    hedge.Notional.Valor,
                    hedge.StrikePut ?? 0m,
                    hedge.StrikeCall ?? 0m,
                    spot),
                _ => 0m
            };

            if (mtm >= 0m)
            {
                mtmAReceber = Math.Round(mtmAReceber + mtm, 6, MidpointRounding.AwayFromZero);
            }
            else
            {
                mtmAPagar = Math.Round(mtmAPagar + Math.Abs(mtm), 6, MidpointRounding.AwayFromZero);
            }
        }

        decimal mtmLiquido = Math.Round(mtmAReceber - mtmAPagar, 6, MidpointRounding.AwayFromZero);

        AjusteMtmDto ajusteMtm = new(
            MtmAReceberBrl: Math.Round(mtmAReceber, 2, MidpointRounding.AwayFromZero),
            MtmAPagarBrl: Math.Round(mtmAPagar, 2, MidpointRounding.AwayFromZero),
            MtmLiquidoBrl: Math.Round(mtmLiquido, 2, MidpointRounding.AwayFromZero));

        decimal dividaLiquida = Math.Round(
            dividaBrutaBrl + mtmLiquido,
            2,
            MidpointRounding.AwayFromZero);

        List<string> alertas = GerarAlertasSemHedge(contratos, hedgesAtivos);

        return new PainelDividaDto(
            DataHoraCalculo: agora.ToString(),
            TipoCotacao: tipoCotacao,
            BreakdownPorMoeda: breakdown.AsReadOnly(),
            DividaBrutaBrl: dividaBrutaBrl,
            AjusteMtm: ajusteMtm,
            DividaLiquidaPosHedgeBrl: dividaLiquida,
            Alertas: alertas.AsReadOnly());
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<Contrato> AplicarFiltros(IReadOnlyList<Contrato> contratos, GetPainelDividaQuery query)
    {
        // ListAsync already applies the soft-delete query filter; only filter by status
        IEnumerable<Contrato> resultado = contratos.Where(c => c.Status == StatusContrato.Ativo);

        if (query.BancoId.HasValue)
        {
            resultado = resultado.Where(c => c.BancoId == query.BancoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Modalidade))
        {
            if (Enum.TryParse<ModalidadeContrato>(query.Modalidade, ignoreCase: true, out ModalidadeContrato modalidade))
            {
                resultado = resultado.Where(c => c.Modalidade == modalidade);
            }
        }

        return resultado.ToList().AsReadOnly();
    }

    private async Task<Dictionary<Moeda, (decimal taxa, bool ehSpot)>> ResolverCotacoesAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate hoje,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, (decimal taxa, bool ehSpot)> resultado = new();

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                resultado[moeda] = (spot.Value.Valor, true);
            }
            else
            {
                CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
                    moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

                if (ptax is not null)
                {
                    // Usa a média entre compra e venda (mid-rate) para conversão de saldo
                    decimal midRate = Math.Round(
                        (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                        6,
                        MidpointRounding.AwayFromZero);
                    resultado[moeda] = (midRate, false);
                }
                // Se nem PTAX disponível, não registra — contrato contribuirá com zero saldo BRL
            }
        }

        return resultado;
    }

    private static List<string> GerarAlertasSemHedge(
        IReadOnlyList<Contrato> contratos,
        IReadOnlyList<InstrumentoHedge> hedgesAtivos)
    {
        HashSet<Guid> contratoIdsComHedge = hedgesAtivos
            .Select(h => h.ContratoId)
            .ToHashSet();

        List<string> alertas = new();

        foreach (Contrato contrato in contratos)
        {
            if (contrato.Moeda == Moeda.Brl)
            {
                continue;
            }

            if (!contratoIdsComHedge.Contains(contrato.Id))
            {
                alertas.Add(
                    $"CONTRATO_SEM_HEDGE: contrato {contrato.Id} em {contrato.Moeda} sem NDF vinculado");
            }
        }

        return alertas;
    }
}
