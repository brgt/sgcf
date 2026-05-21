using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Documentos;

namespace Sgcf.Application.Documentos.Commands;

public sealed record AtualizarStatusDocumentoCommand(
    Guid Id,
    Guid ContratoId,
    StatusDocumento NovoStatus,
    string? Observacao) : IRequest<EnvelopeResponse<DocumentoContratualDto>>;

public sealed class AtualizarStatusDocumentoCommandHandler(
    IDocumentoContratualRepository repository,
    IClock clock)
    : IRequestHandler<AtualizarStatusDocumentoCommand, EnvelopeResponse<DocumentoContratualDto>>
{
    public async Task<EnvelopeResponse<DocumentoContratualDto>> Handle(
        AtualizarStatusDocumentoCommand command,
        CancellationToken cancellationToken)
    {
        DocumentoContratual documento = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"DocumentoContratual {command.Id} não encontrado.");

        if (documento.ContratoId != command.ContratoId)
        {
            throw new KeyNotFoundException($"DocumentoContratual {command.Id} não encontrado no contrato {command.ContratoId}.");
        }

        Instant agora = clock.GetCurrentInstant();

        documento.AtualizarStatus(command.NovoStatus, command.Observacao, agora);

        await repository.SaveChangesAsync(cancellationToken);

        DocumentoContratualDto dto = DocumentoContratualDto.From(documento);
        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<DocumentoContratualDto>(dto, meta);
    }
}
