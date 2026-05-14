using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Auditoria.Queries;
using Sgcf.Application.Common;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("audit")]
public sealed class AuditoriaController(IMediator mediator) : ControllerBase
{
    [HttpGet("eventos")]
    [Authorize(Policy = Policies.Auditoria)]
    [ProducesResponseType<PagedResult<AuditEventoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEventos(
        [FromQuery] ListAuditEventosQuery query,
        CancellationToken ct)
    {
        PagedResult<AuditEventoDto> result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
