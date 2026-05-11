namespace Sgcf.Application.Auditoria;

/// <summary>
/// Registra eventos de exportação de dados (PDF, XLSX) para fins de auditoria.
/// A implementação atual persiste via structured logging (sem tabela dedicada no MVP).
/// </summary>
public interface IExportacaoAuditLog
{
    /// <summary>Registra que um usuário exportou uma tabela completa de contrato.</summary>
    public Task RegistrarAsync(
        Guid contratoId,
        string formato,
        string usuario,
        CancellationToken cancellationToken = default);
}
