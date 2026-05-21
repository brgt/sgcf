using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Benchmarks;
using Sgcf.Application.Benchmarks.Commands;
using Sgcf.Application.Benchmarks.Queries;
using Sgcf.Application.Common;

namespace Sgcf.Api.Controllers;

public sealed record UpsertTaxaBenchmarkRequest(
    string DataReferencia,
    decimal TaxaAa,
    string Fonte);

public sealed record GetEconomiaBenchmarkRequest(
    int DeAno,
    int DeMes,
    int AteAno,
    int AteMes,
    Guid? BancoId = null);

[ApiController]
[Route("api/v1/benchmarks")]
public sealed class BenchmarksController(IMediator mediator) : ControllerBase
{
    [HttpPost("{tipo}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<TaxaBenchmarkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        string tipo,
        [FromBody] UpsertTaxaBenchmarkRequest body,
        CancellationToken ct)
    {
        try
        {
            TaxaBenchmarkDto result = await mediator.Send(
                new UpsertTaxaBenchmarkCommand(tipo, body.DataReferencia, body.TaxaAa, body.Fonte),
                ct);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("{tipo}/serie")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<TaxaBenchmarkDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSerie(
        string tipo,
        [FromQuery] string? de,
        [FromQuery] string? ate,
        CancellationToken ct)
    {
        EnvelopeResponse<IReadOnlyList<TaxaBenchmarkDto>> result = await mediator.Send(
            new GetTaxaBenchmarkSerieQuery(tipo, de, ate),
            ct);

        return Ok(result);
    }

    [HttpGet("{tipo}/economia")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<EconomiaBenchmarkDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEconomia(
        string tipo,
        [FromQuery] int deAno,
        [FromQuery] int deMes,
        [FromQuery] int ateAno,
        [FromQuery] int ateMes,
        [FromQuery] Guid? bancoId,
        CancellationToken ct)
    {
        try
        {
            YearMonth de = new(deAno, deMes);
            YearMonth ate = new(ateAno, ateMes);

            EnvelopeResponse<EconomiaBenchmarkDto> result = await mediator.Send(
                new GetEconomiaBenchmarkQuery(de, ate, tipo, bancoId),
                ct);

            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
