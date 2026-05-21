using MediatR;
using Sgcf.Application.Auditoria;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Trilha de auditoria de uma cotação específica.
/// Delega ao serviço de auditoria existente. SPEC §4.2, §6.2.
/// </summary>
public sealed record GetCotacaoAuditTrailQuery(Guid CotacaoId) : IRequest<IReadOnlyList<AuditLogDto>>;

public sealed class GetCotacaoAuditTrailQueryHandler(IAuditLogRepository auditRepo)
    : IRequestHandler<GetCotacaoAuditTrailQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(
        GetCotacaoAuditTrailQuery query,
        CancellationToken cancellationToken)
    {
        AuditFilter filter = new(
            Entity: "Cotacao",
            EntityId: query.CotacaoId,
            PageSize: 200);

        Application.Common.PagedResult<AuditLogDto> resultado = await auditRepo.ListAsync(filter, cancellationToken);

        return resultado.Items;
    }
}
