using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Sgcf.Domain.Hedge;
using Sgcf.Domain.Painel;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Computa os KPIs executivos usando a mesma lógica de conversão BRL do painel de dívida.
/// O comparativo mês anterior usa PTAX D-1 com data máxima = último dia do mês anterior.
/// </summary>
public sealed class GetDashboardKpisQueryHandler(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IEbitdaMensalRepository ebitdaRepo,
    IClock clock)
    : IRequestHandler<GetDashboardKpisQuery, KpiDto>
{
    public async Task<KpiDto> Handle(GetDashboardKpisQuery query, CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        IReadOnlyList<InstrumentoHedge> hedgesAtivos = await hedgeRepo.ListAtivosAsync(cancellationToken);

        IReadOnlyList<Contrato> contratosAtivos = contratos
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToList()
            .AsReadOnly();

        // Cotações correntes
        Dictionary<Moeda, decimal> taxasAtuais =
            await ResolverTaxasAsync(ExtrairMoedas(contratosAtivos), hoje, false, cancellationToken);

        decimal dividaTotal = ComputarDividaTotal(contratosAtivos, taxasAtuais);
        decimal mtmLiquido = ComputarMtmLiquido(hedgesAtivos, taxasAtuais);
        decimal dividaLiquida = Math.Round(dividaTotal + mtmLiquido, 2, MidpointRounding.AwayFromZero);

        IReadOnlyList<ShareBancoDto> sharePorBanco =
            ComputarSharePorBanco(contratosAtivos, taxasAtuais, dividaTotal);

        decimal custoMedio = ComputarCustoMedioPonderado(contratosAtivos, taxasAtuais);
        decimal prazoMedio = ComputarPrazoMedioPonderado(contratosAtivos, taxasAtuais, hoje);

        // EBITDA do mês corrente
        EbitdaMensal? ebitdaAtual = await ebitdaRepo.GetAsync(hoje.Year, hoje.Month, cancellationToken);
        decimal? dividaEbitda = ebitdaAtual is not null && ebitdaAtual.ValorBrl.Valor > 0m
            ? Math.Round(dividaLiquida / ebitdaAtual.ValorBrl.Valor, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        // Comparativo com mês anterior usando PTAX — último dia do mês anterior
        LocalDate primeiroDiaMesAtual = new LocalDate(hoje.Year, hoje.Month, 1);
        LocalDate ultimoDiaMesAnterior = primeiroDiaMesAtual.PlusDays(-1);
        KpiComparativoDto? comparativo = await ComputarComparativoAsync(
            contratosAtivos, hedgesAtivos, ultimoDiaMesAnterior, dividaTotal, dividaLiquida, cancellationToken);

        return new KpiDto(
            DividaTotalBrl: dividaTotal,
            DividaLiquidaBrl: dividaLiquida,
            DividaEbitda: dividaEbitda,
            SharePorBanco: sharePorBanco,
            CustoMedioPonderadoAaPct: custoMedio,
            PrazoMedioRemanescenteDias: prazoMedio,
            Comparativo: comparativo);
    }

    private static HashSet<Moeda> ExtrairMoedas(IReadOnlyList<Contrato> contratos) =>
        contratos.Where(c => c.Moeda != Moeda.Brl).Select(c => c.Moeda).ToHashSet();

    private static decimal ComputarDividaTotal(
        IReadOnlyList<Contrato> contratos,
        Dictionary<Moeda, decimal> taxas)
    {
        decimal total = 0m;

        foreach (Contrato c in contratos)
        {
            decimal taxa = c.Moeda == Moeda.Brl
                ? 1m
                : taxas.TryGetValue(c.Moeda, out decimal t) ? t : 0m;

            total = Math.Round(
                total + c.ValorPrincipal.Valor * taxa,
                6,
                MidpointRounding.AwayFromZero);
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputarMtmLiquido(
        IReadOnlyList<InstrumentoHedge> hedges,
        Dictionary<Moeda, decimal> taxas)
    {
        decimal mtm = 0m;

        foreach (InstrumentoHedge hedge in hedges)
        {
            decimal spot = hedge.MoedaBase == Moeda.Brl
                ? 1m
                : taxas.TryGetValue(hedge.MoedaBase, out decimal t) ? t : 0m;

            decimal payoff = hedge.Tipo switch
            {
                TipoHedge.NdfForward => NdfMtmCalculador.CalcularMtmForward(
                    hedge.Notional.Valor, hedge.StrikeForward ?? 0m, spot),
                TipoHedge.NdfCollar => NdfMtmCalculador.CalcularMtmCollar(
                    hedge.Notional.Valor, hedge.StrikePut ?? 0m, hedge.StrikeCall ?? 0m, spot),
                _ => 0m
            };

            mtm = Math.Round(mtm + payoff, 6, MidpointRounding.AwayFromZero);
        }

        return Math.Round(mtm, 6, MidpointRounding.AwayFromZero);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<ShareBancoDto> ComputarSharePorBanco(
        IReadOnlyList<Contrato> contratos,
        Dictionary<Moeda, decimal> taxas,
        decimal totalBrl)
    {
        Dictionary<Guid, decimal> porBanco = new();

        foreach (Contrato c in contratos)
        {
            decimal taxa = c.Moeda == Moeda.Brl
                ? 1m
                : taxas.TryGetValue(c.Moeda, out decimal t) ? t : 0m;

            decimal valorBrl = Math.Round(c.ValorPrincipal.Valor * taxa, 6, MidpointRounding.AwayFromZero);

            if (porBanco.TryGetValue(c.BancoId, out decimal existente))
            {
                porBanco[c.BancoId] = Math.Round(existente + valorBrl, 6, MidpointRounding.AwayFromZero);
            }
            else
            {
                porBanco[c.BancoId] = valorBrl;
            }
        }

        return porBanco
            .Select(kvp =>
            {
                decimal valorArredondado = Math.Round(kvp.Value, 2, MidpointRounding.AwayFromZero);
                decimal pct = totalBrl > 0m
                    ? Math.Round(kvp.Value / totalBrl * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                return new ShareBancoDto(kvp.Key, valorArredondado, pct);
            })
            .OrderByDescending(s => s.ValorBrl)
            .ToList()
            .AsReadOnly();
    }

    private static decimal ComputarCustoMedioPonderado(
        IReadOnlyList<Contrato> contratos,
        Dictionary<Moeda, decimal> taxas)
    {
        decimal somaPesosTaxa = 0m;
        decimal somaPesos = 0m;

        foreach (Contrato c in contratos)
        {
            decimal taxa = c.Moeda == Moeda.Brl
                ? 1m
                : taxas.TryGetValue(c.Moeda, out decimal t) ? t : 0m;

            decimal pesoBrl = Math.Round(c.ValorPrincipal.Valor * taxa, 6, MidpointRounding.AwayFromZero);
            decimal taxaAaPct = Math.Round(c.TaxaAa.AsDecimal * 100m, 6, MidpointRounding.AwayFromZero);

            somaPesosTaxa = Math.Round(somaPesosTaxa + pesoBrl * taxaAaPct, 6, MidpointRounding.AwayFromZero);
            somaPesos = Math.Round(somaPesos + pesoBrl, 6, MidpointRounding.AwayFromZero);
        }

        if (somaPesos == 0m)
        {
            return 0m;
        }

        return Math.Round(somaPesosTaxa / somaPesos, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputarPrazoMedioPonderado(
        IReadOnlyList<Contrato> contratos,
        Dictionary<Moeda, decimal> taxas,
        LocalDate hoje)
    {
        decimal somaPesosPrazo = 0m;
        decimal somaPesos = 0m;

        foreach (Contrato c in contratos)
        {
            decimal taxa = c.Moeda == Moeda.Brl
                ? 1m
                : taxas.TryGetValue(c.Moeda, out decimal t) ? t : 0m;

            decimal pesoBrl = Math.Round(c.ValorPrincipal.Valor * taxa, 6, MidpointRounding.AwayFromZero);

            int prazoRemanescente = Period.Between(hoje, c.DataVencimento, PeriodUnits.Days).Days;
            prazoRemanescente = Math.Max(0, prazoRemanescente);

            somaPesosPrazo = Math.Round(
                somaPesosPrazo + pesoBrl * prazoRemanescente,
                6,
                MidpointRounding.AwayFromZero);
            somaPesos = Math.Round(somaPesos + pesoBrl, 6, MidpointRounding.AwayFromZero);
        }

        if (somaPesos == 0m)
        {
            return 0m;
        }

        return Math.Round(somaPesosPrazo / somaPesos, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<KpiComparativoDto?> ComputarComparativoAsync(
        IReadOnlyList<Contrato> contratosAtivos,
        IReadOnlyList<InstrumentoHedge> hedgesAtivos,
        LocalDate dataReferenciaAnterior,
        decimal dividaTotalAtual,
        decimal dividaLiquidaAtual,
        CancellationToken cancellationToken)
    {
        HashSet<Moeda> moedas = ExtrairMoedas(contratosAtivos);

        // Para o comparativo, usa apenas PTAX (cotação histórica)
        Dictionary<Moeda, decimal> taxasAnteriores =
            await ResolverTaxasAsync(moedas, dataReferenciaAnterior, true, cancellationToken);

        // Se não há nenhuma cotação histórica disponível, não computa o comparativo
        if (taxasAnteriores.Count == 0 && moedas.Count > 0)
        {
            return null;
        }

        decimal dividaTotalAnterior = ComputarDividaTotal(contratosAtivos, taxasAnteriores);
        decimal mtmAnterior = ComputarMtmLiquido(hedgesAtivos, taxasAnteriores);
        decimal dividaLiquidaAnterior = Math.Round(
            dividaTotalAnterior + mtmAnterior,
            2,
            MidpointRounding.AwayFromZero);

        if (dividaTotalAnterior == 0m && dividaLiquidaAnterior == 0m)
        {
            return null;
        }

        decimal variacaoTotal = dividaTotalAnterior != 0m
            ? Math.Round(
                (dividaTotalAtual - dividaTotalAnterior) / dividaTotalAnterior * 100m,
                2,
                MidpointRounding.AwayFromZero)
            : 0m;

        decimal variacaoLiquida = dividaLiquidaAnterior != 0m
            ? Math.Round(
                (dividaLiquidaAtual - dividaLiquidaAnterior) / dividaLiquidaAnterior * 100m,
                2,
                MidpointRounding.AwayFromZero)
            : 0m;

        return new KpiComparativoDto(
            DividaTotalBrlMesAnterior: dividaTotalAnterior,
            DividaLiquidaBrlMesAnterior: dividaLiquidaAnterior,
            VariacaoDividaTotalPct: variacaoTotal,
            VariacaoDividaLiquidaPct: variacaoLiquida);
    }

    private async Task<Dictionary<Moeda, decimal>> ResolverTaxasAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate dataReferencia,
        bool apenasHistorico,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> resultado = new();

        foreach (Moeda moeda in moedas)
        {
            if (!apenasHistorico)
            {
                Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);
                if (spot is not null)
                {
                    resultado[moeda] = spot.Value.Valor;
                    continue;
                }
            }

            CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
                moeda, TipoCotacao.PtaxD1, dataReferencia, cancellationToken);

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
