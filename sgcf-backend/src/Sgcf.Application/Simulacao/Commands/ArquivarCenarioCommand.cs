using MediatR;
using Microsoft.Extensions.Logging;
using NodaTime;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Transição de status: Ativo → Arquivado.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir.
/// Lança <see cref="InvalidOperationException"/> se o cenário não estiver Ativo.
/// SPEC §7.4.
/// </summary>
public sealed record ArquivarCenarioCommand(Guid CenarioId) : IRequest<CenarioSimulacaoDto>;

public sealed partial class ArquivarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ILogger<ArquivarCenarioCommandHandler> logger) : IRequestHandler<ArquivarCenarioCommand, CenarioSimulacaoDto>
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Iniciando ArquivarCenario para Cenário {CenarioId}.")]
    private static partial void LogIniciando(ILogger logger, Guid cenarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "ArquivarCenario concluída para Cenário {CenarioId}.")]
    private static partial void LogConcluida(ILogger logger, Guid cenarioId);

    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        ArquivarCenarioCommand cmd,
        CancellationToken cancellationToken)
    {
        LogIniciando(logger, cmd.CenarioId);

        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        cenario.Arquivar(clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        LogConcluida(logger, cmd.CenarioId);
        return CenarioSimulacaoDto.From(cenario);
    }
}
