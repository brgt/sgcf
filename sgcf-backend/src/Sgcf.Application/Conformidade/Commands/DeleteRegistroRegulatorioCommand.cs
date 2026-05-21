using MediatR;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Commands;

public sealed record DeleteRegistroRegulatorioCommand(
    Guid Id,
    Guid ContratoId) : IRequest;

public sealed class DeleteRegistroRegulatorioCommandHandler(
    IRegistroRegulatorioRepository repository)
    : IRequestHandler<DeleteRegistroRegulatorioCommand>
{
    public async Task Handle(
        DeleteRegistroRegulatorioCommand command,
        CancellationToken cancellationToken)
    {
        RegistroRegulatorio registro = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"RegistroRegulatorio {command.Id} não encontrado.");

        await repository.DeleteAsync(registro, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
