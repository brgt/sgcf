using MediatR;
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

public sealed class AtivarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock) : IRequestHandler<AtivarCenarioCommand, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        AtivarCenarioCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        cenario.Ativar(clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
