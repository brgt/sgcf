using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Sgcf.Application.Authorization;
using Sgcf.Application.Calendario;
using Sgcf.Application.Calendario.Commands;
using Sgcf.Application.Calendario.Queries;
using Sgcf.Domain.Calendario;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/feriados")]
public sealed class FeriadosController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lista feriados do ano informado, com filtro opcional por escopo.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<FeriadoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] int ano,
        [FromQuery] EscopoFeriado? escopo,
        CancellationToken ct)
    {
        if (ano < 1900 || ano > 2200)
        {
            return BadRequest(new { error = "Parâmetro 'ano' deve estar entre 1900 e 2200." });
        }

        IReadOnlyList<FeriadoDto> result = await mediator.Send(
            new ListFeriadosQuery(ano, escopo), ct);

        return Ok(result);
    }

    /// <summary>
    /// Cria um novo feriado.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<FeriadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFeriadoCommand command,
        CancellationToken ct)
    {
        try
        {
            FeriadoDto result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(List), new { ano = result.AnoReferencia }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove um feriado pelo identificador.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new DeleteFeriadoCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
