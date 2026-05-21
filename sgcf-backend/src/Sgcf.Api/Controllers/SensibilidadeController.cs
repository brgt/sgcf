using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints de sensibilidade do portfólio a variações em indexadores de taxa de juros.
/// </summary>
[ApiController]
[Route("api/v1/contratos")]
[Authorize]
public sealed class SensibilidadeController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Sensibilidade do portfólio ativo a uma variação nos indexadores de taxa de juros.
    ///
    /// Retorna o custo anual adicional (em BRL) para cada grupo de indexador (CDI, SOFR)
    /// caso as taxas de referência subam <paramref name="deltaBps"/> basis points.
    /// O cálculo é feito server-side para garantir consistência com o restante do sistema.
    /// </summary>
    /// <param name="deltaBps">
    /// Variação hipotética em basis points. Padrão: 100 (+1%). Deve ser entre 1 e 10000.
    /// </param>
    [HttpGet("sensibilidade-indexadores")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<SensibilidadeIndexadoresDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSensibilidadeIndexadores(
        [FromQuery] int deltaBps = 100,
        CancellationToken ct = default)
    {
        try
        {
            EnvelopeResponse<SensibilidadeIndexadoresDto> resultado =
                await mediator.Send(new GetSensibilidadeIndexadoresQuery(deltaBps), ct);
            return Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
