using MediatR;
using Microsoft.Extensions.Logging;
using NodaTime;

using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Realiza o soft delete de um cenário preenchendo <c>DeletedAt</c>.
/// O cenário deixa de aparecer nas listagens padrão (query filter no repositório).
/// Permitido em qualquer status — o domínio não impõe restrição de status para deleção.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir.
/// SPEC §7.4.
/// </summary>
public sealed record DeletarCenarioCommand(Guid CenarioId) : IRequest;

public sealed partial class DeletarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ILogger<DeletarCenarioCommandHandler> logger) : IRequestHandler<DeletarCenarioCommand>
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Iniciando DeletarCenario para Cenário {CenarioId}.")]
    private static partial void LogIniciando(ILogger logger, Guid cenarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "DeletarCenario concluída para Cenário {CenarioId}.")]
    private static partial void LogConcluida(ILogger logger, Guid cenarioId);

    /// <inheritdoc/>
    public async Task Handle(DeletarCenarioCommand cmd, CancellationToken cancellationToken)
    {
        LogIniciando(logger, cmd.CenarioId);

        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        cenario.Deletar(clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        LogConcluida(logger, cmd.CenarioId);
    }
}
