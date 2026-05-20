namespace Sgcf.Domain.Auditoria;

/// <summary>
/// Constantes de auditoria compartilhadas entre camadas.
/// Evita magic strings duplicadas em handlers, jobs e serviços de infraestrutura.
/// </summary>
public static class AuditConstants
{
    /// <summary>
    /// Valor de <c>actor_sub</c> gravado em <c>audit_log</c> quando não há
    /// usuário autenticado — ex.: seeds, migrations, jobs em background.
    /// Permite distinguir alterações administrativas de bugs em code paths sem auth.
    /// </summary>
    public const string SystemActor = "SYSTEM";
}
