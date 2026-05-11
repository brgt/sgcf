using MediatR;
using NodaTime;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Commands;

/// <summary>Cancela uma garantia existente, marcando seu status como <c>Cancelada</c>.</summary>
public sealed record CancelarGarantiaCommand(Guid ContratoId, Guid GarantiaId) : IRequest;

public sealed class CancelarGarantiaCommandHandler(
    IGarantiaRepository garantiaRepo,
    IClock clock)
    : IRequestHandler<CancelarGarantiaCommand>
{
    public async Task Handle(CancelarGarantiaCommand command, CancellationToken cancellationToken)
    {
        Garantia garantia = await garantiaRepo.GetByIdAsync(command.GarantiaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Garantia com Id '{command.GarantiaId}' não encontrada.");

        if (garantia.ContratoId != command.ContratoId)
        {
            throw new KeyNotFoundException(
                $"Garantia '{command.GarantiaId}' não pertence ao contrato '{command.ContratoId}'.");
        }

        garantia.Cancelar(clock);
        await garantiaRepo.SaveChangesAsync(cancellationToken);
    }
}
