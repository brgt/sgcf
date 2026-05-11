using MediatR;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

public sealed record ListContratosQuery : IRequest<IReadOnlyList<ContratoDto>>;

public sealed class ListContratosQueryHandler(IContratoRepository repo)
    : IRequestHandler<ListContratosQuery, IReadOnlyList<ContratoDto>>
{
    public async Task<IReadOnlyList<ContratoDto>> Handle(ListContratosQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Contrato> contratos = await repo.ListAsync(cancellationToken);
        List<ContratoDto> result = new(contratos.Count);

        foreach (Contrato c in contratos)
        {
            result.Add(ContratoDto.From(c, null));
        }

        return result.AsReadOnly();
    }
}
