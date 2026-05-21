using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Application.Conformidade.Commands;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Queries;

public sealed record GetRegistrosRegulatorioPorContratoQuery(
    Guid ContratoId) : IRequest<EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>>;

public sealed class GetRegistrosRegulatorioPorContratoQueryHandler(
    IRegistroRegulatorioRepository repository,
    IClock clock)
    : IRequestHandler<GetRegistrosRegulatorioPorContratoQuery, EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>>
{
    public async Task<EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>> Handle(
        GetRegistrosRegulatorioPorContratoQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<RegistroRegulatorio> registros =
            await repository.ListByContratoAsync(query.ContratoId, cancellationToken);

        List<RegistroRegulatorioDto> dtos = registros
            .Select(CreateRegistroRegulatorioCommandHandler.ToDto)
            .ToList();

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", dtos.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>(dtos.AsReadOnly(), meta);
    }
}
