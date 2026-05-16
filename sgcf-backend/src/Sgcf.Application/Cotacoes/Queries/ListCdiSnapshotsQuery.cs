using MediatR;
using NodaTime;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Lista snapshots de CDI por período. Parâmetros opcionais: se não informados, retorna os últimos 30 dias.
/// SPEC §13 decisão 2.
/// </summary>
public sealed record ListCdiSnapshotsQuery(
    DateOnly? Desde = null,
    DateOnly? Ate = null) : IRequest<IReadOnlyList<CdiSnapshotDto>>;

public sealed class ListCdiSnapshotsQueryHandler(ICdiSnapshotRepository repo, IClock clock)
    : IRequestHandler<ListCdiSnapshotsQuery, IReadOnlyList<CdiSnapshotDto>>
{
    public async Task<IReadOnlyList<CdiSnapshotDto>> Handle(
        ListCdiSnapshotsQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        LocalDate ate = query.Ate.HasValue
            ? new LocalDate(query.Ate.Value.Year, query.Ate.Value.Month, query.Ate.Value.Day)
            : hoje;

        LocalDate desde = query.Desde.HasValue
            ? new LocalDate(query.Desde.Value.Year, query.Desde.Value.Month, query.Desde.Value.Day)
            : ate.PlusDays(-29); // últimos 30 dias por padrão

        IReadOnlyList<CdiSnapshot> snapshots = await repo.ListByPeriodoAsync(desde, ate, cancellationToken);

        List<CdiSnapshotDto> result = new(snapshots.Count);
        foreach (CdiSnapshot s in snapshots)
        {
            result.Add(CdiSnapshotDto.From(s));
        }

        return result.AsReadOnly();
    }
}
