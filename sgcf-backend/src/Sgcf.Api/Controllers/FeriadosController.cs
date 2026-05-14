using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Sgcf.Application.Authorization;
using Sgcf.Application.Calendario;
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
}
