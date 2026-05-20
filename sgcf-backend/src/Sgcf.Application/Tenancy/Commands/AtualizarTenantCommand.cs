using MediatR;
using NodaTime;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy.Commands;

public sealed record AtualizarTenantCommand(
    string IdOrSlug,
    PlanoAssinatura? Plano)
    : IRequest<TenantDto>;

public sealed class AtualizarTenantCommandHandler(ITenantRepository repo, IClock clock)
    : IRequestHandler<AtualizarTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(AtualizarTenantCommand cmd, CancellationToken cancellationToken)
    {
        Tenant tenant = await repo.GetByIdOrSlugAsync(cmd.IdOrSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{cmd.IdOrSlug}' não encontrado.");

        if (cmd.Plano.HasValue)
        {
            tenant.AtualizarPlano(cmd.Plano.Value, clock);
        }

        await repo.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }
}
