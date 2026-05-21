using MediatR;
using Microsoft.Extensions.Logging;

using NodaTime;

using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas.Commands;

/// <summary>
/// Dispensa o alerta identificado por <see cref="AlertaId"/>, transitando seu status para
/// <see cref="StatusAlerta.Dispensado"/>. A operação é idempotente: se o alerta já estava
/// dispensado, o handler retorna sem lançar exceção.
/// Lança <see cref="KeyNotFoundException"/> quando o alerta não existe no tenant corrente.
/// </summary>
public sealed record DispensarAlertaCommand(Guid AlertaId) : IRequest;

public sealed partial class DispensarAlertaCommandHandler(
    IAlertaRepository repo,
    IClock clock,
    ILogger<DispensarAlertaCommandHandler> logger)
    : IRequestHandler<DispensarAlertaCommand>
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Dispensando alerta {AlertaId}.")]
    private static partial void LogDispensando(ILogger logger, Guid alertaId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alerta {AlertaId} dispensado com sucesso.")]
    private static partial void LogDispensado(ILogger logger, Guid alertaId);

    /// <inheritdoc/>
    public async Task Handle(DispensarAlertaCommand cmd, CancellationToken cancellationToken)
    {
        LogDispensando(logger, cmd.AlertaId);

        Alerta alerta = await repo.GetByIdAsync(cmd.AlertaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Alerta '{cmd.AlertaId}' não encontrado.");

        // Idempotente: Dispensar() retorna sem lançar se já estava Dispensado.
        alerta.Dispensar(clock);

        await repo.SaveChangesAsync(cancellationToken);

        LogDispensado(logger, cmd.AlertaId);
    }
}
