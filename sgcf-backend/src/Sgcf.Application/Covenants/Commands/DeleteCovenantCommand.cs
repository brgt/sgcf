using MediatR;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants.Commands;

public sealed record DeleteCovenantCommand(Guid Id) : IRequest;

public sealed class DeleteCovenantCommandHandler(ICovenantRepository repository)
    : IRequestHandler<DeleteCovenantCommand>
{
    public async Task Handle(DeleteCovenantCommand command, CancellationToken cancellationToken)
    {
        Covenant? covenant = await repository.GetAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Covenant {command.Id} não encontrado.");

        repository.Remove(covenant);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
