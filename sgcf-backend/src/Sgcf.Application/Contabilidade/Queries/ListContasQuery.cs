using MediatR;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Queries;

/// <summary>
/// Lista contas do plano gerencial com filtros opcionais por texto livre e situação.
/// O filtro de texto aplica ILIKE simultâneo em CodigoGerencial e Nome.
/// </summary>
public sealed record ListContasQuery(
    string? Search = null,
    bool? Ativo = null) : IRequest<IReadOnlyList<PlanoContasDto>>;

public sealed class ListContasQueryHandler(IPlanoContasRepository repo)
    : IRequestHandler<ListContasQuery, IReadOnlyList<PlanoContasDto>>
{
    public async Task<IReadOnlyList<PlanoContasDto>> Handle(ListContasQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlanoContasGerencial> contas = await repo.ListFilteredAsync(query.Search, query.Ativo, cancellationToken);
        List<PlanoContasDto> result = new(contas.Count);

        foreach (PlanoContasGerencial conta in contas)
        {
            result.Add(PlanoContasDto.From(conta));
        }

        return result.AsReadOnly();
    }
}
