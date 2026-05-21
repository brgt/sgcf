using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Tesouraria;
using Sgcf.Application.Tesouraria.Commands;
using Sgcf.Application.Tesouraria.Queries;
using Sgcf.Domain.Common;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/contas-bancarias")]
public sealed class ContasBancariasController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<ContaBancariaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? apenasAtivas,
        CancellationToken ct)
    {
        IReadOnlyList<ContaBancariaDto> result =
            await mediator.Send(new ListContasBancariasQuery(apenasAtivas), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<ContaBancariaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            ContaBancariaDto result = await mediator.Send(new GetContaBancariaByIdQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<ContaBancariaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CriarContaBancariaRequest request,
        CancellationToken ct)
    {
        CriarContaBancariaCommand command = new(
            request.BancoId,
            request.Nome,
            request.Agencia,
            request.NumeroConta,
            request.Moeda);

        ContaBancariaDto result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] AtualizarContaBancariaRequest request,
        CancellationToken ct)
    {
        try
        {
            AtualizarContaBancariaCommand command = new(
                id,
                request.Nome,
                request.Agencia,
                request.NumeroConta,
                request.Moeda);

            await mediator.Send(command, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new DeletarContaBancariaCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public sealed record CriarContaBancariaRequest(
    Guid BancoId,
    string Nome,
    string Agencia,
    string NumeroConta,
    Moeda Moeda);

public sealed record AtualizarContaBancariaRequest(
    string Nome,
    string Agencia,
    string NumeroConta,
    Moeda Moeda);
