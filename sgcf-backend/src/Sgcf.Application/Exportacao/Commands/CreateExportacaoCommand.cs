using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Exportacao;

namespace Sgcf.Application.Exportacao.Commands;

public sealed record CreateExportacaoCommand(
    TipoExportacao Tipo,
    string? ParametrosJson) : IRequest<EnvelopeResponse<ExportacaoJobDto>>;

public sealed class CreateExportacaoCommandHandler(
    IExportacaoJobRepository repository,
    IClock clock,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateExportacaoCommand, EnvelopeResponse<ExportacaoJobDto>>
{
    public async Task<EnvelopeResponse<ExportacaoJobDto>> Handle(
        CreateExportacaoCommand command,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        ExportacaoJob job = ExportacaoJob.Criar(
            command.Tipo,
            command.ParametrosJson,
            currentUserService.ActorSub,
            agora);

        await repository.AddAsync(job, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<ExportacaoJobDto>(ToDto(job), meta);
    }

    internal static ExportacaoJobDto ToDto(ExportacaoJob j) =>
        new(j.Id,
            j.Tipo.ToString(),
            j.Status.ToString(),
            j.ParametrosJson,
            j.ResultadoJson,
            j.MensagemErro,
            j.SolicitadoPor,
            j.CriadoEm,
            j.IniciadoEm,
            j.ConcluidoEm);
}
