using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Hedge;
using Sgcf.Application.Hedge.Commands;
using Sgcf.Application.Hedge.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Requisição para registrar ou atualizar o snapshot diário de MtM de um hedge.
/// </summary>
public sealed record RegistrarHistoricoMtmRequest(
    decimal PayoffBrl,
    decimal SpotUtilizado,
    string? DataReferencia = null,
    string TipoCotacao = "SPOT_INTRADAY");

[ApiController]
[Route("api/v1/hedges")]
public sealed class HedgesController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}/mtm")]
    [Authorize(Policy = Policies.Leitura)]
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
    [Authorize(Policy = Policies.Gerencial)]
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

    /// <summary>
    /// Registra ou atualiza (upsert) o snapshot diário de MtM para o hedge informado.
    /// Quando <c>dataReferencia</c> é omitido, usa a data corrente no fuso de Brasília.
    /// </summary>
    [HttpPost("{hedgeId:guid}/historico-mtm")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<HistoricoMtmDiarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarHistoricoMtm(
        Guid hedgeId,
        [FromBody] RegistrarHistoricoMtmRequest body,
        CancellationToken ct)
    {
        try
        {
            HistoricoMtmDiarioDto result = await mediator.Send(
                new RegistrarHistoricoMtmCommand(
                    hedgeId,
                    body.DataReferencia,
                    body.PayoffBrl,
                    body.SpotUtilizado,
                    body.TipoCotacao),
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Retorna a série histórica de snapshots de MtM para o hedge informado.
    /// Parâmetros <c>de</c> e <c>ate</c> são datas no formato yyyy-MM-dd (padrão: hoje−90d a hoje BRT).
    /// </summary>
    [HttpGet("{hedgeId:guid}/historico-mtm")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<HistoricoMtmDiarioDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistoricoMtmSerie(
        Guid hedgeId,
        [FromQuery] string? de,
        [FromQuery] string? ate,
        CancellationToken ct)
    {
        try
        {
            EnvelopeResponse<IReadOnlyList<HistoricoMtmDiarioDto>> result = await mediator.Send(
                new GetHistoricoMtmSerieQuery(hedgeId, de, ate),
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
