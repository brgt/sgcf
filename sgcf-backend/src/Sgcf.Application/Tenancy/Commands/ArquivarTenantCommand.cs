using MediatR;
using NodaTime;

namespace Sgcf.Application.Tenancy.Commands;

public sealed record ArquivarTenantCommand(string IdOrSlug)
    : IRequest<TenantDto>;

public sealed class ArquivarTenantCommandHandler(ITenantRepository repo, IClock clock)
    : IRequestHandler<ArquivarTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(ArquivarTenantCommand cmd, CancellationToken cancellationToken)
    {
        Domain.Tenancy.Tenant tenant = await repo.GetByIdOrSlugAsync(cmd.IdOrSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{cmd.IdOrSlug}' não encontrado.");

        tenant.Arquivar(clock);
        await repo.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }
}
