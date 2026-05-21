using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Commands;

public sealed record UpdateRegistroRegulatorioCommand(
    Guid Id,
    Guid ContratoId,
    LocalDate? DataVencimento,
    string? Observacao) : IRequest<EnvelopeResponse<RegistroRegulatorioDto>>;

public sealed class UpdateRegistroRegulatorioCommandHandler(
    IRegistroRegulatorioRepository repository,
    IClock clock)
    : IRequestHandler<UpdateRegistroRegulatorioCommand, EnvelopeResponse<RegistroRegulatorioDto>>
{
    public async Task<EnvelopeResponse<RegistroRegulatorioDto>> Handle(
        UpdateRegistroRegulatorioCommand command,
        CancellationToken cancellationToken)
    {
        RegistroRegulatorio registro = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"RegistroRegulatorio {command.Id} não encontrado.");

        if (registro.ContratoId != command.ContratoId)
        {
            throw new KeyNotFoundException($"RegistroRegulatorio {command.Id} não encontrado no contrato {command.ContratoId}.");
        }

        Instant agora = clock.GetCurrentInstant();
        registro.Atualizar(command.DataVencimento, command.Observacao, agora);

        await repository.SaveChangesAsync(cancellationToken);

        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<RegistroRegulatorioDto>(
            CreateRegistroRegulatorioCommandHandler.ToDto(registro), meta);
    }
}
