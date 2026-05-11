using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Contabilidade.Commands;
using Sgcf.Application.Contabilidade.Queries;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/plano-contas")]
public sealed class PlanoContasController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlanoContasDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        IReadOnlyList<PlanoContasDto> result = await mediator.Send(new ListContasQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlanoContasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            PlanoContasDto result = await mediator.Send(new GetContaQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ProducesResponseType<PlanoContasDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateContaCommand command, CancellationToken ct)
    {
        PlanoContasDto result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PlanoContasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarContaRequest request, CancellationToken ct)
    {
        try
        {
            AtualizarContaCommand command = new(id, request.Nome, request.Natureza, request.CodigoSapB1);
            PlanoContasDto result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public sealed record AtualizarContaRequest(
    string Nome,
    string Natureza,
    string? CodigoSapB1);
