using Microsoft.Extensions.Logging;
using Sgcf.Application.Auditoria;

namespace Sgcf.Infrastructure.Auditoria;

/// <summary>
/// Implementação de auditoria de exportação usando structured logging.
/// No MVP não requer tabela dedicada — eventos ficam no log centralizado (Cloud Logging / Serilog).
/// </summary>
internal sealed partial class ExportacaoAuditLog(ILogger<ExportacaoAuditLog> logger) : IExportacaoAuditLog
{
    public Task RegistrarAsync(
        Guid contratoId,
        string formato,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        LogExportacao(logger, contratoId, formato, usuario);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Exportação realizada — contratoId={ContratoId} formato={Formato} usuario={Usuario}")]
    private static partial void LogExportacao(
        ILogger logger,
        Guid contratoId,
        string formato,
        string usuario);
}
