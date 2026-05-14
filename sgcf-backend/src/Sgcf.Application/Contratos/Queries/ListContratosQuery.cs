using MediatR;
using Sgcf.Application.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

/// <summary>
/// Query paginada de contratos. Aceita um filtro composto e devolve uma página de resultados
/// junto com o total de registros que satisfazem o filtro (para cálculo de paginação no cliente).
/// </summary>
public sealed record ListContratosQuery(ContratoFilter Filter) : IRequest<PagedResult<ContratoDto>>;

public sealed class ListContratosQueryHandler(IContratoRepository repo)
    : IRequestHandler<ListContratosQuery, PagedResult<ContratoDto>>
{
    private static readonly HashSet<string> AllowedSorts =
        new(StringComparer.OrdinalIgnoreCase) { "DataVencimento", "DataContratacao", "ValorPrincipal", "NumeroExterno" };

    public async Task<PagedResult<ContratoDto>> Handle(ListContratosQuery query, CancellationToken cancellationToken)
    {
        ContratoFilter f = query.Filter;

        // Sanitize sort/paging inputs — guard against invalid values from callers
        string sort = AllowedSorts.Contains(f.Sort) ? f.Sort : "DataVencimento";
        int page = Math.Max(1, f.Page);
        int pageSize = Math.Clamp(f.PageSize, 1, 100);

        (IReadOnlyList<Contrato> items, int total) = await repo.ListPagedAsync(f, sort, f.Dir, page, pageSize, cancellationToken);

        List<ContratoDto> result = new(items.Count);
        foreach (Contrato c in items)
        {
            result.Add(ContratoDto.From(c, null));
        }

        return new PagedResult<ContratoDto>(result.AsReadOnly(), total, page, pageSize);
    }
}
