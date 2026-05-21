using MediatR;
using Sgcf.Domain.Documentos;

namespace Sgcf.Application.Documentos.Commands;

public sealed record DeleteDocumentoContratualCommand(
    Guid Id,
    Guid ContratoId) : IRequest<Unit>;

public sealed class DeleteDocumentoContratualCommandHandler(
    IDocumentoContratualRepository repository)
    : IRequestHandler<DeleteDocumentoContratualCommand, Unit>
{
    public async Task<Unit> Handle(
        DeleteDocumentoContratualCommand command,
        CancellationToken cancellationToken)
    {
        DocumentoContratual documento = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"DocumentoContratual {command.Id} não encontrado.");

        if (documento.ContratoId != command.ContratoId)
        {
            throw new KeyNotFoundException($"DocumentoContratual {command.Id} não encontrado no contrato {command.ContratoId}.");
        }

        await repository.DeleteAsync(documento, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
