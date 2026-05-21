using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Application.Conformidade.Commands;

public sealed record AtualizarStatusRegistroCommand(
    Guid Id,
    Guid ContratoId,
    StatusRegistroRegulatorio NovoStatus,
    string? Observacao) : IRequest<EnvelopeResponse<RegistroRegulatorioDto>>;

public sealed class AtualizarStatusRegistroCommandHandler(
    IRegistroRegulatorioRepository repository,
    IClock clock)
    : IRequestHandler<AtualizarStatusRegistroCommand, EnvelopeResponse<RegistroRegulatorioDto>>
{
    public async Task<EnvelopeResponse<RegistroRegulatorioDto>> Handle(
        AtualizarStatusRegistroCommand command,
        CancellationToken cancellationToken)
    {
        RegistroRegulatorio registro = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"RegistroRegulatorio {command.Id} não encontrado.");

        Instant agora = clock.GetCurrentInstant();
        registro.AtualizarStatus(command.NovoStatus, command.Observacao, agora);

        await repository.SaveChangesAsync(cancellationToken);

        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<RegistroRegulatorioDto>(
            CreateRegistroRegulatorioCommandHandler.ToDto(registro), meta);
    }
}
