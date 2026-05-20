using MediatR;
using NodaTime;

using Sgcf.Application.Simulacao.Cache;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Remove uma simulação de contratação de um cenário.
/// Permitido em Rascunho e Ativo. Bloqueado em Arquivado.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir.
/// Lança <see cref="InvalidOperationException"/> se o cenário estiver Arquivado
/// ou se a simulação não pertencer ao cenário.
/// SPEC §7.5.
/// </summary>
public sealed record RemoverSimulacaoCommand(
    Guid CenarioId,
    Guid SimulacaoId) : IRequest<CenarioSimulacaoDto>;

public sealed class RemoverSimulacaoCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ICronogramaSimulacaoCache cache) : IRequestHandler<RemoverSimulacaoCommand, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        RemoverSimulacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        // Domínio lança InvalidOperationException se Arquivado ou simulação não encontrada.
        cenario.RemoverSimulacao(cmd.SimulacaoId, clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        // Remove todas as versões em cache desta simulação deletada. Sem esta chamada,
        // leituras com chave v=N antiga retornariam cronograma de uma simulação que já não existe.
        await cache.InvalidarPorSimulacaoAsync(cmd.CenarioId, cmd.SimulacaoId, cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
