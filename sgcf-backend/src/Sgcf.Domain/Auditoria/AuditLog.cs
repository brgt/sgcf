using NodaTime;

namespace Sgcf.Domain.Auditoria;

public sealed class AuditLog
{
    private AuditLog() { }

    public long Id { get; private set; }
    public Instant OccurredAt { get; private set; }
    public string ActorSub { get; private set; } = string.Empty;
    public string ActorRole { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Entity { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string? DiffJson { get; private set; }
    public Guid RequestId { get; private set; }
    public byte[]? IpHash { get; private set; }

    public static AuditLog Create(
        Instant occurredAt,
        string actorSub,
        string actorRole,
        string source,
        string entity,
        Guid? entityId,
        string operation,
        string? diffJson,
        Guid requestId,
        byte[]? ipHash = null)
    {
        return new AuditLog
        {
            OccurredAt = occurredAt,
            ActorSub   = actorSub,
            ActorRole  = actorRole,
            Source     = source,
            Entity     = entity,
            EntityId   = entityId,
            Operation  = operation,
            DiffJson   = diffJson,
            RequestId  = requestId,
            IpHash     = ipHash,
        };
    }
}
