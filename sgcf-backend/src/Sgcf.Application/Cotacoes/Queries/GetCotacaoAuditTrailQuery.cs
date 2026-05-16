using MediatR;
using Sgcf.Application.Auditoria;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Trilha de auditoria de uma cotação específica.
/// Delega ao serviço de auditoria existente. SPEC §4.2, §6.2.
/// </summary>
public sealed record GetCotacaoAuditTrailQuery(Guid CotacaoId) : IRequest<IReadOnlyList<AuditEventoDto>>;

public sealed class GetCotacaoAuditTrailQueryHandler(IAuditLogRepository auditRepo)
    : IRequestHandler<GetCotacaoAuditTrailQuery, IReadOnlyList<AuditEventoDto>>
{
    public async Task<IReadOnlyList<AuditEventoDto>> Handle(
        GetCotacaoAuditTrailQuery query,
        CancellationToken cancellationToken)
    {
        AuditFilter filter = new(
            Entity: "Cotacao",
            EntityId: query.CotacaoId,
            PageSize: 200);

        Application.Common.PagedResult<AuditEventoDto> resultado = await auditRepo.ListAsync(filter, cancellationToken);

        return resultado.Items;
    }
}
