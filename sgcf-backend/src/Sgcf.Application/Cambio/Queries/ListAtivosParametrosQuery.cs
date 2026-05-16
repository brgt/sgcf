using MediatR;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio.Queries;

public sealed record ListAtivosParametrosQuery : IRequest<IReadOnlyList<ParametroCotacao>>;

public sealed class ListAtivosParametrosQueryHandler(IParametroCotacaoRepository repo)
    : IRequestHandler<ListAtivosParametrosQuery, IReadOnlyList<ParametroCotacao>>
{
    public async Task<IReadOnlyList<ParametroCotacao>> Handle(ListAtivosParametrosQuery query, CancellationToken cancellationToken)
    {
        return await repo.ListAtivosAsync(cancellationToken);
    }
}
