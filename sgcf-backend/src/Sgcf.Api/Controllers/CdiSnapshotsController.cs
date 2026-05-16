using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Gerencia snapshots diários de CDI cadastrados manualmente no MVP.
/// O CDI é utilizado para equalização de propostas com prazos distintos (SPEC §5.3, §13 decisão 2).
/// </summary>
[ApiController]
[Route("api/v1/cdi-snapshots")]
public sealed class CdiSnapshotsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Cria snapshot de CDI para uma data específica. Política: Admin.
    /// Retorna 409 se já existe snapshot para a data informada.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CdiSnapshotDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar(
        [FromBody] CreateCdiSnapshotCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            CdiSnapshotDto result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(List), new { desde = result.Data }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lista snapshots de CDI por período. Parâmetros opcionais; padrão: últimos 30 dias.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<CdiSnapshotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? ate,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CdiSnapshotDto> result = await mediator.Send(
            new ListCdiSnapshotsQuery(desde, ate),
            cancellationToken);

        return Ok(result);
    }
}
