using NodaTime;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Auditoria;

/// <summary>
/// Evento de auditoria imutável — registra toda ação com impacto no sistema.
///
/// Task −1.11: adicionados <see cref="Impersonating"/> e <see cref="ImpersonatedBy"/>
/// para registrar quando super-admin atua em nome de um tenant.
/// Conforme decisão sponsor 2026-05-20, impersonação é sempre visível ao admin do tenant (LGPD).
/// </summary>
public sealed class AuditLog : ITenantScoped
{
    private AuditLog() { }

    public long Id { get; private set; }
    public Guid TenantId { get; private set; }
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

    /// <summary>Indica que a ação foi realizada por super-admin em nome do tenant.</summary>
    public bool Impersonating { get; private set; }

    /// <summary>
    /// Sub do super-admin que realizou a impersonação.
    /// Null quando <see cref="Impersonating"/> é false.
    /// </summary>
    public string? ImpersonatedBy { get; private set; }

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
        byte[]? ipHash = null,
        bool impersonating = false,
        string? impersonatedBy = null)
    {
        return new AuditLog
        {
            OccurredAt      = occurredAt,
            ActorSub        = actorSub,
            ActorRole       = actorRole,
            Source          = source,
            Entity          = entity,
            EntityId        = entityId,
            Operation       = operation,
            DiffJson        = diffJson,
            RequestId       = requestId,
            IpHash          = ipHash,
            Impersonating   = impersonating,
            ImpersonatedBy  = impersonatedBy,
        };
    }
}
