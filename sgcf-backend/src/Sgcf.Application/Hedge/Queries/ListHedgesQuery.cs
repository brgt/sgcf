using MediatR;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge.Queries;

public sealed record ListHedgesQuery(Guid ContratoId) : IRequest<IReadOnlyList<HedgeDto>>;

public sealed class ListHedgesQueryHandler(IHedgeRepository hedgeRepo)
    : IRequestHandler<ListHedgesQuery, IReadOnlyList<HedgeDto>>
{
    public async Task<IReadOnlyList<HedgeDto>> Handle(ListHedgesQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<InstrumentoHedge> hedges =
            await hedgeRepo.ListByContratoAsync(query.ContratoId, cancellationToken);

        List<HedgeDto> dtos = new(hedges.Count);
        foreach (InstrumentoHedge hedge in hedges)
        {
            dtos.Add(ToDto(hedge));
        }

        return dtos.AsReadOnly();
    }

    private static HedgeDto ToDto(InstrumentoHedge hedge) =>
        new(
            Id: hedge.Id,
            ContratoId: hedge.ContratoId,
            Tipo: hedge.Tipo.ToString(),
            ContraparteId: hedge.ContraparteId,
            NotionalMoedaOriginal: hedge.Notional.Valor,
            MoedaBase: hedge.MoedaBase.ToString().ToUpperInvariant(),
            DataContratacao: hedge.DataContratacao.ToString(),
            DataVencimento: hedge.DataVencimento.ToString(),
            StrikeForward: hedge.StrikeForward,
            StrikePut: hedge.StrikePut,
            StrikeCall: hedge.StrikeCall,
            Status: hedge.Status.ToString(),
            Alertas: Array.Empty<string>());
}
