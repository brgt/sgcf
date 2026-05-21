using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Commands;

public sealed record CreateRegistroRegulatorioCommand(
    Guid ContratoId,
    TipoRegistroRegulatorio Tipo,
    LocalDate? DataVencimento,
    string? Observacao) : IRequest<EnvelopeResponse<RegistroRegulatorioDto>>;

public sealed class CreateRegistroRegulatorioCommandHandler(
    IRegistroRegulatorioRepository repository,
    IClock clock)
    : IRequestHandler<CreateRegistroRegulatorioCommand, EnvelopeResponse<RegistroRegulatorioDto>>
{
    public async Task<EnvelopeResponse<RegistroRegulatorioDto>> Handle(
        CreateRegistroRegulatorioCommand command,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        RegistroRegulatorio registro = RegistroRegulatorio.Criar(
            command.ContratoId,
            command.Tipo,
            command.DataVencimento,
            command.Observacao,
            agora);

        await repository.AddAsync(registro, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<RegistroRegulatorioDto>(RegistroRegulatorioDto.From(registro), meta);
    }
}
