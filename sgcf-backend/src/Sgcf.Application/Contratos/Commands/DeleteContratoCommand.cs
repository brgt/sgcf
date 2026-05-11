using MediatR;
using NodaTime;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Commands;

public sealed record DeleteContratoCommand(Guid Id) : IRequest;

public sealed class DeleteContratoCommandHandler(IContratoRepository repo, IClock clock)
    : IRequestHandler<DeleteContratoCommand>
{
    public async Task Handle(DeleteContratoCommand cmd, CancellationToken cancellationToken)
    {
        Contrato contrato = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{cmd.Id}' não encontrado.");

        contrato.Deletar(clock);
        await repo.SaveChangesAsync(cancellationToken);
    }
}
