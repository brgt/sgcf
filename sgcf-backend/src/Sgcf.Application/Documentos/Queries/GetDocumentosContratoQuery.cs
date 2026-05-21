using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Application.Documentos.Commands;
using Sgcf.Domain.Documentos;

namespace Sgcf.Application.Documentos.Queries;

public sealed record GetDocumentosContratoQuery(
    Guid ContratoId) : IRequest<EnvelopeResponse<IReadOnlyList<DocumentoContratualDto>>>;

public sealed class GetDocumentosContratoQueryHandler(
    IDocumentoContratualRepository repository,
    IClock clock)
    : IRequestHandler<GetDocumentosContratoQuery, EnvelopeResponse<IReadOnlyList<DocumentoContratualDto>>>
{
    public async Task<EnvelopeResponse<IReadOnlyList<DocumentoContratualDto>>> Handle(
        GetDocumentosContratoQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<DocumentoContratual> documentos =
            await repository.ListByContratoAsync(query.ContratoId, cancellationToken);

        List<DocumentoContratualDto> dtos = documentos
            .Select(CreateDocumentoContratualCommandHandler.ToDto)
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<DocumentoContratualDto>>(dtos.AsReadOnly(), meta);
    }
}
