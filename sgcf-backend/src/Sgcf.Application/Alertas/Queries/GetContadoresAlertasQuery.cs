using MediatR;
using Microsoft.Extensions.Logging;

using Sgcf.Application.Alertas.Dtos;
using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas.Queries;

/// <summary>
/// Retorna a contagem de alertas abertos por severidade para o perfil informado.
/// Usado pelo cockpit para exibir os badges de notificação.
/// O global query filter do EF Core garante isolamento por tenant automaticamente.
/// </summary>
public sealed record GetContadoresAlertasQuery(PerfilCockpit Perfil)
    : IRequest<ContadoresAlertaDto>;

public sealed partial class GetContadoresAlertasQueryHandler(
    IAlertaRepository repo,
    ILogger<GetContadoresAlertasQueryHandler> logger)
    : IRequestHandler<GetContadoresAlertasQuery, ContadoresAlertaDto>
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "GetContadoresAlertas — perfil={Perfil}.")]
    private static partial void LogConsultando(ILogger logger, PerfilCockpit perfil);

    /// <inheritdoc/>
    public async Task<ContadoresAlertaDto> Handle(
        GetContadoresAlertasQuery query,
        CancellationToken cancellationToken)
    {
        LogConsultando(logger, query.Perfil);

        ContadoresAlerta contadores = await repo.GetContadoresAsync(query.Perfil, cancellationToken);
        return ContadoresAlertaDto.From(contadores);
    }
}
