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
    public async Task<ResultadoCotacao?> ResolveAsync(
        Moeda moeda,
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ParametroCotacao> parametros = await parametroRepo.ListAtivosAsync(cancellationToken);
        TipoCotacao tipo = ResolveTipoCotacaoService.Resolve(parametros, bancoId, modalidade);

        LocalDate dataRef = clock.GetCurrentInstant().InUtc().Date;

        if (tipo == TipoCotacao.SpotIntraday)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);
            if (spot is not null)
            {
                return new ResultadoCotacao(spot.Value, tipo, clock.GetCurrentInstant());
            }
        }

        LocalDate dataConsulta = tipo == TipoCotacao.PtaxD1 ? dataRef.PlusDays(-1) : dataRef;
        TipoCotacao tipoConsulta = tipo == TipoCotacao.PtaxD1 ? TipoCotacao.PtaxD0 : tipo;

        CotacaoFx? cotacao = await cotacaoRepo.GetMaisRecenteAsync(moeda, tipoConsulta, dataConsulta, cancellationToken);
        if (cotacao is null)
        {
            return null;
        }

        decimal midRate = Math.Round((cotacao.ValorCompra.Valor + cotacao.ValorVenda.Valor) / 2m, 6, MidpointRounding.AwayFromZero);
        return new ResultadoCotacao(new Money(midRate, Moeda.Brl), tipo, cotacao.Momento);
    }
}
