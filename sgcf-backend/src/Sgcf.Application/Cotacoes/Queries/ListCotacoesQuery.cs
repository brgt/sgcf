using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>Lista paginada de cotações com filtros. SPEC §6.2.</summary>
public sealed record ListCotacoesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Modalidade = null,
    DateOnly? Desde = null,
    DateOnly? Ate = null) : IRequest<PagedResult<CotacaoDto>>;

public sealed class ListCotacoesQueryHandler(ICotacaoRepository repo)
    : IRequestHandler<ListCotacoesQuery, PagedResult<CotacaoDto>>
{
    public async Task<PagedResult<CotacaoDto>> Handle(ListCotacoesQuery query, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        StatusCotacao? status = query.Status is not null && Enum.TryParse<StatusCotacao>(query.Status, true, out StatusCotacao s)
            ? s : null;

        ModalidadeContrato? modalidade = query.Modalidade is not null && Enum.TryParse<ModalidadeContrato>(query.Modalidade, true, out ModalidadeContrato m)
            ? m : null;

        LocalDate? desde = query.Desde.HasValue
            ? new LocalDate(query.Desde.Value.Year, query.Desde.Value.Month, query.Desde.Value.Day)
            : null;

        LocalDate? ate = query.Ate.HasValue
            ? new LocalDate(query.Ate.Value.Year, query.Ate.Value.Month, query.Ate.Value.Day)
            : null;

        CotacaoFilter filter = new(status, modalidade, desde, ate, page, pageSize);

        (IReadOnlyList<Cotacao> items, int total) = await repo.ListPagedAsync(filter, page, pageSize, cancellationToken);

        List<CotacaoDto> result = new(items.Count);
        foreach (Cotacao c in items)
        {
            result.Add(CotacaoDto.From(c));
        }

        return new PagedResult<CotacaoDto>(result.AsReadOnly(), total, page, pageSize);
    }
}
