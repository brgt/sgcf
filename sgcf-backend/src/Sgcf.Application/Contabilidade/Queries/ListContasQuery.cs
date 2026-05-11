using MediatR;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Queries;

public sealed record ListContasQuery : IRequest<IReadOnlyList<PlanoContasDto>>;

public sealed class ListContasQueryHandler(IPlanoContasRepository repo)
    : IRequestHandler<ListContasQuery, IReadOnlyList<PlanoContasDto>>
{
    public async Task<IReadOnlyList<PlanoContasDto>> Handle(ListContasQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlanoContasGerencial> contas = await repo.ListAllAsync(cancellationToken);
        List<PlanoContasDto> result = new(contas.Count);

        foreach (PlanoContasGerencial conta in contas)
        {
            result.Add(PlanoContasDto.From(conta));
        }

        return result.AsReadOnly();
    }
}
