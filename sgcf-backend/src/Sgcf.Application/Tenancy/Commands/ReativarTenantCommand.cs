using MediatR;
using NodaTime;

namespace Sgcf.Application.Tenancy.Commands;

public sealed record ReativarTenantCommand(string IdOrSlug)
    : IRequest<TenantDto>;

public sealed class ReativarTenantCommandHandler(ITenantRepository repo, IClock clock)
    : IRequestHandler<ReativarTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(ReativarTenantCommand cmd, CancellationToken cancellationToken)
    {
        Domain.Tenancy.Tenant tenant = await repo.GetByIdOrSlugAsync(cmd.IdOrSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{cmd.IdOrSlug}' não encontrado.");

        tenant.Reativar(clock);
        await repo.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }
}
