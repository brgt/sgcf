using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Tesouraria;
using Sgcf.Application.Tesouraria.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints de tesouraria: consolidados de hedge, exposição cambial e efetividade de cobertura.
/// </summary>
[ApiController]
[Route("api/v1/tesouraria")]
[Authorize]
public sealed class TesourariaController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Retorna a efetividade de hedge consolidada: exposição por moeda, nocional de cobertura,
    /// taxa de cobertura e MtM atual de cada instrumento NDF ativo.
    /// </summary>
    [HttpGet("hedge-efetividade")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<HedgeEfetividadeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHedgeEfetividade(CancellationToken ct)
    {
        EnvelopeResponse<HedgeEfetividadeDto> resultado =
            await mediator.Send(new GetHedgeEfetividadeQuery(), ct);

        return Ok(resultado);
    }
}
