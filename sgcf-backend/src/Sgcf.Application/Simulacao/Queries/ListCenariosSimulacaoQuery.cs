using MediatR;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Queries;

/// <summary>
/// Lista cenários com filtros opcionais, excluindo soft-deletados.
/// Retorna DTOs resumidos (sem simulações filhas) para eficiência de payload.
/// SPEC §7.4.
/// </summary>
public sealed record ListCenariosSimulacaoQuery(
    StatusCenarioSimulacao? Status,
    int? AnoBase,
    string? CriadoPor) : IRequest<IReadOnlyList<CenarioSimulacaoResumoDto>>;

public sealed class ListCenariosSimulacaoQueryHandler(
    ICenarioSimulacaoRepository repo) : IRequestHandler<ListCenariosSimulacaoQuery, IReadOnlyList<CenarioSimulacaoResumoDto>>
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<CenarioSimulacaoResumoDto>> Handle(
        ListCenariosSimulacaoQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CenarioSimulacao> cenarios = await repo.ListAsync(
            query.Status,
            query.AnoBase,
            query.CriadoPor,
            cancellationToken);

        List<CenarioSimulacaoResumoDto> resultado = new(cenarios.Count);
        foreach (CenarioSimulacao c in cenarios)
        {
            resultado.Add(CenarioSimulacaoResumoDto.From(c));
        }

        return resultado.AsReadOnly();
    }
}
