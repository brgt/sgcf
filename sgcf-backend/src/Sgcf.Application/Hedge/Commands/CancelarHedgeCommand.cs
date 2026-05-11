using MediatR;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge.Commands;

public sealed record CancelarHedgeCommand(Guid HedgeId) : IRequest;

public sealed class CancelarHedgeCommandHandler(IHedgeRepository hedgeRepo)
    : IRequestHandler<CancelarHedgeCommand>
{
    public async Task Handle(CancelarHedgeCommand command, CancellationToken cancellationToken)
    {
        InstrumentoHedge hedge = await hedgeRepo.GetByIdAsync(command.HedgeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Hedge com Id '{command.HedgeId}' não encontrado.");

        hedge.Cancelar();

        await hedgeRepo.SaveChangesAsync(cancellationToken);
    }
}
