using MediatR;

namespace Sgcf.Application.Tenancy.Queries;

public sealed record GetTenantQuery(string IdOrSlug) : IRequest<TenantDto>;

public sealed class GetTenantQueryHandler(ITenantRepository repo)
    : IRequestHandler<GetTenantQuery, TenantDto>
{
    public async Task<TenantDto> Handle(GetTenantQuery query, CancellationToken cancellationToken)
    {
        Domain.Tenancy.Tenant tenant = await repo.GetByIdOrSlugAsync(query.IdOrSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{query.IdOrSlug}' não encontrado.");

        return TenantDto.From(tenant);
    }
}
