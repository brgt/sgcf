using MediatR;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge.Queries;

public sealed record GetMtmQuery(Guid HedgeId, decimal NotionalContratoSaldo = 0m) : IRequest<MtmResultadoDto>;

public sealed class GetMtmQueryHandler(
    IHedgeRepository hedgeRepo,
    ICotacaoSpotCache spotCache,
    IClock clock)
    : IRequestHandler<GetMtmQuery, MtmResultadoDto>
{
    public async Task<MtmResultadoDto> Handle(GetMtmQuery query, CancellationToken cancellationToken)
    {
        InstrumentoHedge hedge = await hedgeRepo.GetByIdAsync(query.HedgeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Hedge com Id '{query.HedgeId}' não encontrado.");

        Money spot = await spotCache.GetSpotAsync(hedge.MoedaBase, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Cotação spot indisponível para moeda {hedge.MoedaBase}. Verifique o cache de cotações.");

        decimal spotValor = spot.Valor;
        decimal notional = hedge.Notional.Valor;

        decimal payoff = hedge.Tipo == TipoHedge.NdfForward
            ? NdfMtmCalculador.CalcularMtmForward(notional, hedge.StrikeForward!.Value, spotValor)
            : NdfMtmCalculador.CalcularMtmCollar(notional, hedge.StrikePut!.Value, hedge.StrikeCall!.Value, spotValor);

        string posicao = payoff > 0m ? "RECEBER" : payoff < 0m ? "PAGAR" : "NEUTRO";

        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InUtc().Date;

        IReadOnlyList<string> alertas = AlertasExposicaoService.GerarAlertas(
            hedge,
            spotValor,
            payoff,
            query.NotionalContratoSaldo,
            hoje);

        string dataHoraCotacao = agora
            .ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        return new MtmResultadoDto(
            HedgeId: hedge.Id,
            PayoffBrl: payoff,
            Posicao: posicao,
            SpotUtilizado: spotValor,
            TipoCotacao: "SPOT_INTRADAY",
            DataHoraCotacao: dataHoraCotacao,
            Alertas: alertas);
    }
}
