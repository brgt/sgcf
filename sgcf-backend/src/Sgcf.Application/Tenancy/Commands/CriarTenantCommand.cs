using MediatR;
using NodaTime;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy.Commands;

public sealed record CriarTenantCommand(
    string Slug,
    string Nome,
    string Cnpj,
    PlanoAssinatura Plano)
    : IRequest<TenantDto>;

public sealed class CriarTenantCommandHandler(ITenantRepository repo, IClock clock)
    : IRequestHandler<CriarTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(CriarTenantCommand cmd, CancellationToken cancellationToken)
    {
        Guid id = Guid.CreateVersion7();
        Tenant tenant = Tenant.Criar(id, cmd.Slug, cmd.Nome, cmd.Cnpj, cmd.Plano, clock);
        await repo.AddAsync(tenant, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }
}
