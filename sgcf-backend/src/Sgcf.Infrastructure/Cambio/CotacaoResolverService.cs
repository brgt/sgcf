using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;

namespace Sgcf.Infrastructure.Cambio;

internal sealed class CotacaoResolverService(
    IParametroCotacaoRepository parametroRepo,
    ICotacaoFxRepository cotacaoRepo,
    ICotacaoSpotCache spotCache,
    IClock clock) : IResolveTipoCotacaoService
{
    // dataRef is a BR calendar date (vencimento context) — must use BRT, not UTC.
    // At 23:30 BRT (02:30 UTC+1d) InUtc().Date would return tomorrow's date.
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<ResultadoCotacao?> ResolveAsync(
        Moeda moeda,
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ParametroCotacao> parametros = await parametroRepo.ListAtivosAsync(cancellationToken);
        TipoCotacao tipo = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        LocalDate dataRef = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        if (tipo == TipoCotacao.SpotIntraday)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);
            if (spot is not null)
            {
                return new ResultadoCotacao(spot.Value, tipo, clock.GetCurrentInstant());
            }
        }

        CotacaoFx? cotacao = await ResolverFxAsync(moeda, tipo, dataRef, cancellationToken);
        if (cotacao is null)
        {
            return null;
        }

        decimal midRate = Math.Round((cotacao.ValorCompra.Valor + cotacao.ValorVenda.Valor) / 2m, 6, MidpointRounding.AwayFromZero);
        return new ResultadoCotacao(new Money(midRate, Moeda.Brl), tipo, cotacao.Momento);
    }

    public Task<CotacaoFx?> ResolverFxAsync(
        Moeda moeda,
        TipoCotacao tipoLogico,
        LocalDate dataReferencia,
        CancellationToken cancellationToken = default)
    {
        // PtaxD1 é um tipo lógico: o fechamento de D-1. O ingestor grava apenas PtaxD0,
        // então traduzimos a consulta para PtaxD0 no dia anterior à referência.
        LocalDate dataConsulta = tipoLogico == TipoCotacao.PtaxD1 ? dataReferencia.PlusDays(-1) : dataReferencia;
        TipoCotacao tipoConsulta = tipoLogico == TipoCotacao.PtaxD1 ? TipoCotacao.PtaxD0 : tipoLogico;

        return cotacaoRepo.GetMaisRecenteAsync(moeda, tipoConsulta, dataConsulta, cancellationToken);
    }
}
