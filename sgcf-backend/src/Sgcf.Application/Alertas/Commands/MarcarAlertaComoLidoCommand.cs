using MediatR;
using Microsoft.Extensions.Logging;

using NodaTime;

using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas.Commands;

/// <summary>
/// Marca o alerta identificado por <see cref="AlertaId"/> como lido, transitando
/// seu status de <see cref="StatusAlerta.Aberto"/> para <see cref="StatusAlerta.Lido"/>.
/// A operação é idempotente quando o alerta já está Lido.
/// Lança <see cref="KeyNotFoundException"/> quando o alerta não existe no tenant corrente.
/// Lança <see cref="InvalidOperationException"/> se o alerta já estiver Dispensado.
/// </summary>
public sealed record MarcarAlertaComoLidoCommand(Guid AlertaId) : IRequest;

public sealed partial class MarcarAlertaComoLidoCommandHandler(
    IAlertaRepository repo,
    IClock clock,
    ILogger<MarcarAlertaComoLidoCommandHandler> logger)
    : IRequestHandler<MarcarAlertaComoLidoCommand>
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Marcando alerta {AlertaId} como lido.")]
    private static partial void LogMarcando(ILogger logger, Guid alertaId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alerta {AlertaId} marcado como lido.")]
    private static partial void LogMarcado(ILogger logger, Guid alertaId);

    /// <inheritdoc/>
    public async Task Handle(MarcarAlertaComoLidoCommand cmd, CancellationToken cancellationToken)
    {
        LogMarcando(logger, cmd.AlertaId);

        Alerta alerta = await repo.GetByIdAsync(cmd.AlertaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Alerta '{cmd.AlertaId}' não encontrado.");

        // MarcarComoLido() é idempotente quando já Lido; lança InvalidOperationException se Dispensado.
        alerta.MarcarComoLido(clock);

        await repo.SaveChangesAsync(cancellationToken);

        LogMarcado(logger, cmd.AlertaId);
    }
}
