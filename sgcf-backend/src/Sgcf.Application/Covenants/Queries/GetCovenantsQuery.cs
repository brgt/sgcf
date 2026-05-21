using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Application.Covenants.Commands;
using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants.Queries;

public sealed record GetCovenantsQuery(
    Guid ContratoId) : IRequest<EnvelopeResponse<IReadOnlyList<CovenantDto>>>;

public sealed class GetCovenantsQueryHandler(
    ICovenantRepository repository,
    IClock clock)
    : IRequestHandler<GetCovenantsQuery, EnvelopeResponse<IReadOnlyList<CovenantDto>>>
{
    public async Task<EnvelopeResponse<IReadOnlyList<CovenantDto>>> Handle(
        GetCovenantsQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<Covenant> covenants = await repository.ListByContratoAsync(query.ContratoId, cancellationToken);

        List<CovenantDto> dtos = covenants
            .Select(CreateCovenantCommandHandler.ToDto)
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<CovenantDto>>(dtos.AsReadOnly(), meta);
    }
}
