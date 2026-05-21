using NodaTime;
using Sgcf.Domain.Documentos;

namespace Sgcf.Application.Documentos;

/// <summary>
/// DTO de leitura para DocumentoContratual. GAP-CKP-16.
/// Status e Tipo são representados como strings para legibilidade no JSON de resposta.
/// </summary>
public sealed record DocumentoContratualDto(
    Guid Id,
    Guid ContratoId,
    string Tipo,
    string Status,
    string Nome,
    string? UrlArmazenamento,
    LocalDate? DataEmissao,
    LocalDate? DataVencimento,
    string? Observacao,
    Instant CriadoEm,
    Instant AtualizadoEm)
{
    public static DocumentoContratualDto From(DocumentoContratual d) =>
        new(d.Id, d.ContratoId, d.Tipo.ToString(), d.Status.ToString(),
            d.Nome, d.UrlArmazenamento, d.DataEmissao, d.DataVencimento,
            d.Observacao, d.CriadoEm, d.AtualizadoEm);
}
