using MediatR;
using Sgcf.Application.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy.Queries;

public sealed record ListTenantsQuery(
    StatusTenant? Status,
    int Page,
    int PageSize)
    : IRequest<PagedResult<TenantDto>>;

public sealed class ListTenantsQueryHandler(ITenantRepository repo)
    : IRequestHandler<ListTenantsQuery, PagedResult<TenantDto>>
{
    public async Task<PagedResult<TenantDto>> Handle(
        ListTenantsQuery query,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        PagedResult<Domain.Tenancy.Tenant> result = await repo.ListAsync(
            query.Status, page, pageSize, cancellationToken);

        IReadOnlyList<TenantDto> items = result.Items
            .Select(TenantDto.From)
            .ToList()
            .AsReadOnly();

        return new PagedResult<TenantDto>(items, result.Total, page, pageSize);
    }
}
