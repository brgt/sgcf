using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Application.Exportacao.Commands;
using Sgcf.Domain.Exportacao;

namespace Sgcf.Application.Exportacao.Queries;

public sealed record GetExportacaoQuery(Guid Id) : IRequest<EnvelopeResponse<ExportacaoJobDto>>;

public sealed class GetExportacaoQueryHandler(
    IExportacaoJobRepository repository,
    IClock clock)
    : IRequestHandler<GetExportacaoQuery, EnvelopeResponse<ExportacaoJobDto>>
{
    public async Task<EnvelopeResponse<ExportacaoJobDto>> Handle(
        GetExportacaoQuery query,
        CancellationToken cancellationToken)
    {
        ExportacaoJob job = await repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ExportacaoJob '{query.Id}' não encontrado.");

        Instant agora = clock.GetCurrentInstant();
        EnvelopeMeta meta = new(agora, [new FonteConsultada("banco_de_dados", "ok", 1)], Completude.Completo);
        return new EnvelopeResponse<ExportacaoJobDto>(CreateExportacaoCommandHandler.ToDto(job), meta);
    }
}
