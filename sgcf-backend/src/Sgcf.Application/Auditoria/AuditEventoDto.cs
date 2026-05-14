namespace Sgcf.Application.Auditoria;

/// <summary>
/// Representação pública de um evento de auditoria. IpHash é omitido por privacidade.
/// </summary>
public sealed record AuditEventoDto(
    long Id,
    DateTimeOffset OccurredAt,
    string ActorSub,
    string ActorRole,
    string Source,
    string Entity,
    Guid? EntityId,
    string Operation,
    string? DiffJson,
    Guid RequestId);
