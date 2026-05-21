using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Auditoria.Queries;
using Sgcf.Application.Common;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/auditoria")]
public sealed class AuditoriaController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lista eventos de auditoria do tenant corrente (isolado por EF global filter).
    /// Filtro opcional <c>impersonating=true</c> retorna apenas operações realizadas por super-admin
    /// em nome deste tenant — conforme decisão de transparência LGPD (2026-05-20).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Auditoria)]
    [ProducesResponseType<PagedResult<AuditLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEventos(
        [FromQuery] ListAuditEventosQuery query,
        CancellationToken ct)
    {
        PagedResult<AuditLogDto> result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
