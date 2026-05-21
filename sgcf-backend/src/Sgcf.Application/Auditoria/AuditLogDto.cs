namespace Sgcf.Application.Auditoria;

/// <summary>
/// Representação pública de um evento de auditoria com campos de impersonação.
/// IpHash é omitido por privacidade (LGPD).
/// </summary>
public sealed record AuditLogDto(
    long Id,
    DateTimeOffset OccurredAt,
    string ActorSub,
    string ActorRole,
    string Source,
    string Entity,
    Guid? EntityId,
    string Operation,
    string? DiffJson,
    Guid RequestId,
    bool Impersonating,
    string? ImpersonatedBy);
