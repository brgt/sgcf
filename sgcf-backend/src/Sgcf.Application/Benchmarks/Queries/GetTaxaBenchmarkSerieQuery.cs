using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Application.Common;
using Sgcf.Domain.Benchmarks;

namespace Sgcf.Application.Benchmarks.Queries;

public sealed record GetTaxaBenchmarkSerieQuery(
    string Tipo,
    string? De,
    string? Ate) : IRequest<EnvelopeResponse<IReadOnlyList<TaxaBenchmarkDto>>>;

public sealed class GetTaxaBenchmarkSerieQueryHandler(
    ITaxaBenchmarkRepository repository,
    IClock clock)
    : IRequestHandler<GetTaxaBenchmarkSerieQuery, EnvelopeResponse<IReadOnlyList<TaxaBenchmarkDto>>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    private static readonly LocalDatePattern DatePattern =
        LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");

    public async Task<EnvelopeResponse<IReadOnlyList<TaxaBenchmarkDto>>> Handle(
        GetTaxaBenchmarkSerieQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        LocalDate de = ParseDateOrDefault(query.De, hoje.PlusMonths(-12));
        LocalDate ate = ParseDateOrDefault(query.Ate, hoje);

        IReadOnlyList<TaxaBenchmark> taxas = await repository.ListAsync(
            query.Tipo, de, ate, cancellationToken);

        List<TaxaBenchmarkDto> dtos = taxas
            .Select(t => new TaxaBenchmarkDto(
                t.TipoBenchmark,
                t.DataReferencia.ToString("yyyy-MM-dd", null),
                t.TaxaAa,
                t.Fonte))
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<TaxaBenchmarkDto>>(dtos.AsReadOnly(), meta);
    }

    private static LocalDate ParseDateOrDefault(string? input, LocalDate defaultValue)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        ParseResult<LocalDate> result = DatePattern.Parse(input);
        return result.Success ? result.Value : defaultValue;
    }
}
