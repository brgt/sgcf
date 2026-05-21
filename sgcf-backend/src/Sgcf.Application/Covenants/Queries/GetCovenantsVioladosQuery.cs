using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Application.Covenants.Commands;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants.Queries;

public sealed record GetCovenantsVioladosQuery : IRequest<EnvelopeResponse<IReadOnlyList<CovenantDto>>>;

public sealed class GetCovenantsVioladosQueryHandler(
    ICovenantRepository repository,
    IClock clock)
    : IRequestHandler<GetCovenantsVioladosQuery, EnvelopeResponse<IReadOnlyList<CovenantDto>>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<IReadOnlyList<CovenantDto>>> Handle(
        GetCovenantsVioladosQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        IReadOnlyList<Covenant> violados = await repository.ListVioladosAsync(cancellationToken);
        IReadOnlyList<Covenant> vencendo = await repository.ListVencendoAsync(hoje.PlusMonths(1), cancellationToken);

        HashSet<Guid> violadosIds = violados.Select(c => c.Id).ToHashSet();
        List<Covenant> todos = [..violados, ..vencendo.Where(c => !violadosIds.Contains(c.Id))];

        List<CovenantDto> dtos = todos
            .Select(CreateCovenantCommandHandler.ToDto)
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<CovenantDto>>(dtos.AsReadOnly(), meta);
    }
}
