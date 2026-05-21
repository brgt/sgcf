using MediatR;
using Microsoft.Extensions.Logging;

using Sgcf.Application.Alertas.Dtos;
using Sgcf.Application.Common;
using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas.Queries;

/// <summary>
/// Retorna uma página de alertas do tenant corrente com filtros opcionais.
/// O global query filter do EF Core garante isolamento por tenant — nenhum filtro manual necessário aqui.
/// </summary>
public sealed record ListAlertasQuery(
    PerfilCockpit? Perfil,
    SeveridadeAlerta? Severidade,
    CategoriaAlerta? Categoria,
    StatusAlerta? Status,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AlertaDto>>;

public sealed partial class ListAlertasQueryHandler(
    IAlertaRepository repo,
    ILogger<ListAlertasQueryHandler> logger)
    : IRequestHandler<ListAlertasQuery, PagedResult<AlertaDto>>
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ListAlertas — page={Page} pageSize={PageSize}.")]
    private static partial void LogListando(ILogger logger, int page, int pageSize);

    /// <inheritdoc/>
    public async Task<PagedResult<AlertaDto>> Handle(ListAlertasQuery query, CancellationToken cancellationToken)
    {
        // Guard: paginação razoável — evita pageSize=0 ou valores negativos de callers descuidados.
        int page     = Math.Max(1, query.PageNumber);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        LogListando(logger, page, pageSize);

        AlertaFilter filter = new(
            Perfil:     query.Perfil,
            Severidade: query.Severidade,
            Categoria:  query.Categoria,
            Status:     query.Status,
            PageNumber: page,
            PageSize:   pageSize);

        IReadOnlyList<Alerta> alertas = await repo.ListAsync(filter, cancellationToken);

        List<AlertaDto> dtos = new(alertas.Count);
        foreach (Alerta alerta in alertas)
        {
            dtos.Add(AlertaDto.From(alerta));
        }

        // IAlertaRepository.ListAsync não retorna total — usamos o tamanho da página atual.
        // Quando o cliente precisar de paginação completa, o contrato do repositório deverá
        // ser estendido com um CountAsync separado (ou um ListPagedAsync como IContratoRepository).
        return new PagedResult<AlertaDto>(dtos.AsReadOnly(), dtos.Count, page, pageSize);
    }
}
