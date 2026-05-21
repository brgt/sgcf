using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Covenants;
using Sgcf.Application.Covenants.Commands;
using Sgcf.Application.Covenants.Queries;
using Sgcf.Domain.Covenants;

namespace Sgcf.Api.Controllers;

public sealed record CreateCovenantRequest(
    string Descricao,
    TipoCovenant Tipo,
    int PeriodicidadeVerificacaoMeses,
    string? ProximaVerificacaoEm,
    decimal? LimiteNumerico);

public sealed record UpdateCovenantRequest(
    string Descricao,
    int PeriodicidadeVerificacaoMeses,
    string? ProximaVerificacaoEm,
    decimal? LimiteNumerico);

public sealed record VerificarCovenantRequest(
    string DataVerificacao,
    StatusCovenant NovoStatus,
    string? ProximaVerificacaoEm,
    decimal? ValorApurado,
    string? Observacao);

[ApiController]
[Route("api/v1/contratos/{contratoId:guid}/covenants")]
public sealed class CovenantsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<CovenantDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid contratoId, CancellationToken ct)
    {
        EnvelopeResponse<IReadOnlyList<CovenantDto>> result =
            await mediator.Send(new GetCovenantsQuery(contratoId), ct);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CovenantDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid contratoId,
        [FromBody] CreateCovenantRequest body,
        CancellationToken ct)
    {
        try
        {
            CovenantDto result = await mediator.Send(
                new CreateCovenantCommand(
                    contratoId,
                    body.Descricao,
                    body.Tipo,
                    body.PeriodicidadeVerificacaoMeses,
                    body.ProximaVerificacaoEm,
                    body.LimiteNumerico),
                ct);

            return CreatedAtAction(nameof(List), new { contratoId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CovenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid contratoId,
        Guid id,
        [FromBody] UpdateCovenantRequest body,
        CancellationToken ct)
    {
        try
        {
            CovenantDto result = await mediator.Send(
                new UpdateCovenantCommand(
                    id,
                    body.Descricao,
                    body.PeriodicidadeVerificacaoMeses,
                    body.ProximaVerificacaoEm,
                    body.LimiteNumerico),
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/verificacoes")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CovenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verificar(
        Guid contratoId,
        Guid id,
        [FromBody] VerificarCovenantRequest body,
        CancellationToken ct)
    {
        try
        {
            CovenantDto result = await mediator.Send(
                new VerificarCovenantCommand(
                    id,
                    body.DataVerificacao,
                    body.NovoStatus,
                    body.ProximaVerificacaoEm,
                    body.ValorApurado,
                    body.Observacao),
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Gerencial)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid contratoId, Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new DeleteCovenantCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

[ApiController]
[Route("api/v1/covenants")]
public sealed class CovenantsMonitorController(IMediator mediator) : ControllerBase
{
    [HttpGet("violacoes")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<CovenantDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetViolacoes(CancellationToken ct)
    {
        EnvelopeResponse<IReadOnlyList<CovenantDto>> result =
            await mediator.Send(new GetCovenantsVioladosQuery(), ct);

        return Ok(result);
    }
}
