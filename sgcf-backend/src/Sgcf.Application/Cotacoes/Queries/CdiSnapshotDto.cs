using Sgcf.Application.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

public sealed record CdiSnapshotDto(
    Guid Id,
    DateOnly Data,
    decimal CdiAaPercentual,
    DateTimeOffset CreatedAt)
{
    public static CdiSnapshotDto From(CdiSnapshot s) => new(
        s.Id,
        new DateOnly(s.Data.Year, s.Data.Month, s.Data.Day),
        s.CdiAaPercentual,
        s.CreatedAt.ToDateTimeOffset());
}
