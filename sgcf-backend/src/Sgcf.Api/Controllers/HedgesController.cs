using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Hedge;
using Sgcf.Application.Hedge.Commands;
using Sgcf.Application.Hedge.Queries;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/hedges")]
public sealed class HedgesController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}/mtm")]
    [ProducesResponseType<MtmResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetMtm(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            MtmResultadoDto result = await mediator.Send(new GetMtmQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new CancelarHedgeCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
