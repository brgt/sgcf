using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Conformidade;
using Sgcf.Application.Conformidade.Commands;
using Sgcf.Application.Conformidade.Queries;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Api.Controllers;

public sealed record CreateRegistroRegulatorioRequest(
    TipoRegistroRegulatorio Tipo,
    LocalDate? DataVencimento,
    string? Observacao);

public sealed record UpdateRegistroRegulatorioRequest(
    LocalDate? DataVencimento,
    string? Observacao);

public sealed record RegistrarNumeroRequest(
    string NumeroRegistro,
    LocalDate DataRegistro,
    string? Observacao);

public sealed record AtualizarStatusRegistroRequest(
    StatusRegistroRegulatorio NovoStatus,
    string? Observacao);

[ApiController]
[Route("api/v1/contratos/{contratoId:guid}/registros-regulatorios")]
public sealed class ConformidadeController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid contratoId, CancellationToken cancellationToken)
    {
        EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>> result =
            await mediator.Send(new GetRegistrosRegulatorioPorContratoQuery(contratoId), cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<RegistroRegulatorioDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid contratoId,
        [FromBody] CreateRegistroRegulatorioRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            EnvelopeResponse<RegistroRegulatorioDto> result = await mediator.Send(
                new CreateRegistroRegulatorioCommand(
                    contratoId,
                    body.Tipo,
                    body.DataVencimento,
                    body.Observacao),
                cancellationToken);

            return CreatedAtAction(nameof(List), new { contratoId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<RegistroRegulatorioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid contratoId,
        Guid id,
        [FromBody] UpdateRegistroRegulatorioRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            EnvelopeResponse<RegistroRegulatorioDto> result = await mediator.Send(
                new UpdateRegistroRegulatorioCommand(id, contratoId, body.DataVencimento, body.Observacao),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Gerencial)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid contratoId, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new DeleteRegistroRegulatorioCommand(id, contratoId), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/numero")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<RegistroRegulatorioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegistrarNumero(
        Guid contratoId,
        Guid id,
        [FromBody] RegistrarNumeroRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            EnvelopeResponse<RegistroRegulatorioDto> result = await mediator.Send(
                new RegistrarNumeroCommand(
                    id,
                    contratoId,
                    body.NumeroRegistro,
                    body.DataRegistro,
                    body.Observacao),
                cancellationToken);

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

    [HttpPost("{id:guid}/status")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<RegistroRegulatorioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarStatus(
        Guid contratoId,
        Guid id,
        [FromBody] AtualizarStatusRegistroRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            EnvelopeResponse<RegistroRegulatorioDto> result = await mediator.Send(
                new AtualizarStatusRegistroCommand(id, contratoId, body.NovoStatus, body.Observacao),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}

[ApiController]
[Route("api/v1/conformidade")]
public sealed class ConformidadeMonitorController(ISender mediator) : ControllerBase
{
    [HttpGet("pendentes")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendentes(CancellationToken cancellationToken)
    {
        EnvelopeResponse<IReadOnlyList<RegistroRegulatorioDto>> result =
            await mediator.Send(new GetRegistrosPendentesQuery(), cancellationToken);

        return Ok(result);
    }
}
