namespace Sgcf.Application.Auditoria;

/// <summary>
/// Grava eventos de auditoria de forma explícita (fora do change-tracking automático do AuditInterceptor).
/// Útil para operações que não passam pelo EF Core SaveChanges, como chamadas a serviços externos.
///
/// O TenantId e os campos de impersonação são preenchidos automaticamente a partir do ITenantContext
/// — o caller não precisa passá-los.
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Registra um evento de auditoria para a entidade e operação informadas.
    /// </summary>
    /// <param name="entity">Nome da entidade afetada (ex: "Contrato", "Parcela").</param>
    /// <param name="entityId">ID da entidade afetada, se aplicável.</param>
    /// <param name="operation">Operação realizada (ex: "CREATE", "UPDATE", "DELETE").</param>
    /// <param name="diff">Objeto de diff serializado como JSON. Null quando não aplicável.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public Task WriteAsync(
        string entity,
        Guid? entityId,
        string operation,
        object? diff,
        CancellationToken ct);
}
