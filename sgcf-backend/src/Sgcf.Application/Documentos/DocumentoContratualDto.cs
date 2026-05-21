using NodaTime;

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
    Instant AtualizadoEm);
