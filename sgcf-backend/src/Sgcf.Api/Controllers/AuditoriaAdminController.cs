using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Auditoria.Queries;
using Sgcf.Application.Common;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoint admin cross-tenant para consulta de trilha de auditoria.
///
/// Rota: /api/v1/admin/auditoria (bypassa TenantResolverMiddleware pelo prefixo /admin/).
/// Acesso exclusivo a super-admin. O <c>tenantId</c> no query string é obrigatório.
///
/// Transparência LGPD (decisão sponsor 2026-05-20): impersonação é sempre visível.
/// O filtro <c>impersonating=true</c> retorna apenas operações realizadas por super-admin
/// em nome do tenant consultado.
/// </summary>
[ApiController]
[Route("api/v1/admin/auditoria")]
[Authorize(Policy = Policies.SuperAdmin)]
public sealed class AuditoriaAdminController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lista eventos de auditoria de um tenant específico.
    /// </summary>
    /// <param name="tenantId">ID do tenant a consultar. Obrigatório.</param>
    /// <param name="entity">Filtro opcional por nome da entidade.</param>
    /// <param name="entityId">Filtro opcional por ID da entidade.</param>
    /// <param name="actorSub">Filtro opcional por sub do ator.</param>
    /// <param name="source">Filtro opcional por source (path HTTP ou "system").</param>
    /// <param name="operation">Filtro opcional por operação (CREATE, UPDATE, DELETE).</param>
    /// <param name="de">Filtro de data início (inclusivo).</param>
    /// <param name="ate">Filtro de data fim (inclusivo).</param>
    /// <param name="impersonating">Quando true, retorna apenas eventos de impersonação.</param>
    /// <param name="page">Página (1-based, padrão 1).</param>
    /// <param name="pageSize">Itens por página (1–200, padrão 50).</param>
    /// <param name="ct">Token de cancelamento.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid tenantId,
        [FromQuery] string? entity,
        [FromQuery] Guid? entityId,
        [FromQuery] string? actorSub,
        [FromQuery] string? source,
        [FromQuery] string? operation,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] bool? impersonating,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        ListAdminAuditEventosQuery query = new(
            TenantId:       tenantId,
            Entity:         entity,
            EntityId:       entityId,
            ActorSub:       actorSub,
            Source:         source,
            Operation:      operation,
            De:             de,
            Ate:            ate,
            Impersonating:  impersonating,
            Page:           page,
            PageSize:       pageSize);

        PagedResult<AuditLogDto> result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
