using MediatR;
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

public sealed class ArquivarCenarioCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock) : IRequestHandler<ArquivarCenarioCommand, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        ArquivarCenarioCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        cenario.Arquivar(clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
