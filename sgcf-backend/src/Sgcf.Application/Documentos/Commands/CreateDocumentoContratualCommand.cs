using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Documentos;

namespace Sgcf.Application.Documentos.Commands;

public sealed record CreateDocumentoContratualCommand(
    Guid ContratoId,
    TipoDocumento Tipo,
    string Nome,
    LocalDate? DataEmissao,
    LocalDate? DataVencimento,
    string? UrlArmazenamento,
    string? Observacao) : IRequest<EnvelopeResponse<DocumentoContratualDto>>;

public sealed class CreateDocumentoContratualCommandHandler(
    IDocumentoContratualRepository repository,
    IClock clock)
    : IRequestHandler<CreateDocumentoContratualCommand, EnvelopeResponse<DocumentoContratualDto>>
{
    public async Task<EnvelopeResponse<DocumentoContratualDto>> Handle(
        CreateDocumentoContratualCommand command,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        DocumentoContratual documento = DocumentoContratual.Criar(
            command.ContratoId,
            command.Tipo,
            command.Nome,
            command.DataEmissao,
            command.DataVencimento,
            command.UrlArmazenamento,
            command.Observacao,
            agora);

        await repository.AddAsync(documento, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        DocumentoContratualDto dto = DocumentoContratualDto.From(documento);
        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<DocumentoContratualDto>(dto, meta);
    }
}
