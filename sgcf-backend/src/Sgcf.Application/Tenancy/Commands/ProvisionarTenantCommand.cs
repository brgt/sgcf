using MediatR;
using Sgcf.Application.Tenancy.Services;

namespace Sgcf.Application.Tenancy.Commands;

/// <summary>
/// Provisiona os dados mestres de um tenant ativo.
/// Delega toda a lógica para <see cref="ITenantProvisioner"/>, mantendo o handler fino.
/// </summary>
public sealed record ProvisionarTenantCommand(Guid TenantId) : IRequest<ResultadoProvisionamento>;

public sealed class ProvisionarTenantCommandHandler(ITenantProvisioner provisioner)
    : IRequestHandler<ProvisionarTenantCommand, ResultadoProvisionamento>
{
    public Task<ResultadoProvisionamento> Handle(
        ProvisionarTenantCommand command,
        CancellationToken cancellationToken) =>
        provisioner.ProvisionarAsync(command.TenantId, cancellationToken);
}
