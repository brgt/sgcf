using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Commands;

public sealed record RegistrarNumeroCommand(
    Guid Id,
    Guid ContratoId,
    string NumeroRegistro,
    LocalDate DataRegistro,
    string? Observacao) : IRequest<EnvelopeResponse<RegistroRegulatorioDto>>;

public sealed class RegistrarNumeroCommandHandler(
    IRegistroRegulatorioRepository repository,
    IClock clock)
    : IRequestHandler<RegistrarNumeroCommand, EnvelopeResponse<RegistroRegulatorioDto>>
{
    public async Task<EnvelopeResponse<RegistroRegulatorioDto>> Handle(
        RegistrarNumeroCommand command,
        CancellationToken cancellationToken)
    {
        RegistroRegulatorio registro = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"RegistroRegulatorio {command.Id} não encontrado.");

        Instant agora = clock.GetCurrentInstant();
        registro.Registrar(command.NumeroRegistro, command.DataRegistro, command.Observacao, agora);

        await repository.SaveChangesAsync(cancellationToken);

        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<RegistroRegulatorioDto>(
            RegistroRegulatorioDto.From(registro), meta);
    }
}
