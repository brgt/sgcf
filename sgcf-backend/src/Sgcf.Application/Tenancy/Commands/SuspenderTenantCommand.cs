using MediatR;
using NodaTime;

namespace Sgcf.Application.Tenancy.Commands;

public sealed record SuspenderTenantCommand(string IdOrSlug, string Motivo)
    : IRequest<TenantDto>;

public sealed class SuspenderTenantCommandHandler(ITenantRepository repo, IClock clock)
    : IRequestHandler<SuspenderTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(SuspenderTenantCommand cmd, CancellationToken cancellationToken)
    {
        Domain.Tenancy.Tenant tenant = await repo.GetByIdOrSlugAsync(cmd.IdOrSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{cmd.IdOrSlug}' não encontrado.");

        tenant.Suspender(cmd.Motivo, clock);
        await repo.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }
}
