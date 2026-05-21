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

        DocumentoContratualDto dto = ToDto(documento);
        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<DocumentoContratualDto>(dto, meta);
    }

    /// <summary>
    /// Mapeia uma entidade <see cref="DocumentoContratual"/> para <see cref="DocumentoContratualDto"/>.
    /// Compartilhado por todos os handlers de Documentos para evitar duplicação.
    /// </summary>
    internal static DocumentoContratualDto ToDto(DocumentoContratual d) =>
        new(d.Id,
            d.ContratoId,
            d.Tipo.ToString(),
            d.Status.ToString(),
            d.Nome,
            d.UrlArmazenamento,
            d.DataEmissao,
            d.DataVencimento,
            d.Observacao,
            d.CriadoEm,
            d.AtualizadoEm);
}
