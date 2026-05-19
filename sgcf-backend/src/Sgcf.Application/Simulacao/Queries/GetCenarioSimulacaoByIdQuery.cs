using MediatR;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Queries;

/// <summary>
/// Retorna o cenário completo com todas as simulações filhas.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir (ou estiver soft-deletado).
/// SPEC §7.4.
/// </summary>
public sealed record GetCenarioSimulacaoByIdQuery(Guid Id) : IRequest<CenarioSimulacaoDto>;

public sealed class GetCenarioSimulacaoByIdQueryHandler(
    ICenarioSimulacaoRepository repo) : IRequestHandler<GetCenarioSimulacaoByIdQuery, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        GetCenarioSimulacaoByIdQuery query,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{query.Id}' não encontrado.");

        return CenarioSimulacaoDto.From(cenario);
    }
}
