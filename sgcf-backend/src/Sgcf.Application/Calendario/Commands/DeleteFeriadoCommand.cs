using MediatR;

using Sgcf.Domain.Calendario;

namespace Sgcf.Application.Calendario.Commands;

public sealed record DeleteFeriadoCommand(Guid Id) : IRequest;

public sealed class DeleteFeriadoCommandHandler(IFeriadoRepository repo)
    : IRequestHandler<DeleteFeriadoCommand>
{
    public async Task Handle(
        DeleteFeriadoCommand cmd,
        CancellationToken cancellationToken)
    {
        Feriado feriado = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feriado com Id '{cmd.Id}' não encontrado.");

        repo.Remove(feriado);
        await repo.SaveChangesAsync(cancellationToken);
    }
}
