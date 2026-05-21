using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Queries;

public sealed record GetRegistrosPendentesQuery : IRequest<EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>>;

public sealed class GetRegistrosPendentesQueryHandler(
    IRegistroRegulatorioRepository repository,
    IClock clock)
    : IRequestHandler<GetRegistrosPendentesQuery, EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>>
{
    public async Task<EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>> Handle(
        GetRegistrosPendentesQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<RegistroRegulatorio> registros =
            await repository.ListPendentesAsync(cancellationToken);

        List<RegistroRegulatorioDto> dtos = registros
            .Select(RegistroRegulatorioDto.From)
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>(dtos.AsReadOnly(), meta);
    }
}
