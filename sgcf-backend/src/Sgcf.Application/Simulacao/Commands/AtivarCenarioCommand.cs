using MediatR;
using Microsoft.Extensions.Logging;
using NodaTime;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Transição de status: Rascunho → Ativo.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir.
/// Lança <see cref="InvalidOperationException"/> se o status atual não permitir a ativação (ex: já Ativo ou Arquivado).
/// SPEC §7.4.
/// </summary>
public sealed record AtivarCenarioCommand(Guid CenarioId) : IRequest<CenarioSimulacaoDto>;

public sealed partial class AtivarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ILogger<AtivarCenarioCommandHandler> logger) : IRequestHandler<AtivarCenarioCommand, CenarioSimulacaoDto>
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Iniciando AtivarCenario para Cenário {CenarioId}.")]
    private static partial void LogIniciando(ILogger logger, Guid cenarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "AtivarCenario concluída para Cenário {CenarioId}.")]
    private static partial void LogConcluida(ILogger logger, Guid cenarioId);

    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        AtivarCenarioCommand cmd,
        CancellationToken cancellationToken)
    {
        LogIniciando(logger, cmd.CenarioId);

        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        cenario.Ativar(clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        LogConcluida(logger, cmd.CenarioId);
        return CenarioSimulacaoDto.From(cenario);
    }
}
