using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

public sealed record ListParametrosQuery : IRequest<IReadOnlyList<ParametroCotacaoDto>>;

public sealed class ListParametrosQueryHandler(IParametroCotacaoRepository repo)
    : IRequestHandler<ListParametrosQuery, IReadOnlyList<ParametroCotacaoDto>>
{
    public async Task<IReadOnlyList<ParametroCotacaoDto>> Handle(ListParametrosQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<ParametroCotacao> parametros = await repo.ListAllAsync(cancellationToken);
        List<ParametroCotacaoDto> result = new(parametros.Count);

        foreach (ParametroCotacao parametro in parametros)
        {
            result.Add(ParametroCotacaoDto.From(parametro));
        }

        return result.AsReadOnly();
    }
}
