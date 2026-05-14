using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>Parâmetros para simulação de cenário cambial.</summary>
public sealed record SimularCenarioCambialRequest(
    decimal? DeltaUsdPct = null,
    decimal? DeltaEurPct = null,
    decimal? DeltaJpyPct = null,
    decimal? DeltaCnyPct = null);

/// <summary>Parâmetros para simulação de antecipação de portfólio.</summary>
public sealed record SimularAntecipacaoPortfolioRequest(
    decimal CaixaDisponivelBrl,
    decimal? TaxaCdiAa = null);

/// <summary>
/// Endpoints de simulação de cenários cambiais e antecipação de portfólio.
/// </summary>
[ApiController]
[Route("api/v1/simulador")]
public sealed class SimuladorController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Simula o impacto de variações cambiais sobre a dívida total.
    /// Retorna cenário customizado + pessimista (-10%), realista (0%) e otimista (+10%).
    /// </summary>
    [HttpPost("cenario-cambial")]
    [Authorize(Policy = Policies.Executivo)]
    [ProducesResponseType<ResultadoCenarioCambialDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SimularCenarioCambial(
        [FromBody] SimularCenarioCambialRequest body,
        CancellationToken cancellationToken)
    {
        ResultadoCenarioCambialDto resultado = await mediator.Send(
            new SimularCenarioCambialQuery(
                body.DeltaUsdPct,
                body.DeltaEurPct,
                body.DeltaJpyPct,
                body.DeltaCnyPct),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Ranqueia os top-5 contratos com maior economia líquida de antecipação,
    /// considerando o custo de oportunidade do CDI.
    /// </summary>
    [HttpPost("antecipacao-portfolio")]
    [Authorize(Policy = Policies.Executivo)]
    [ProducesResponseType<ResultadoAntecipacaoPortfolioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimularAntecipacaoPortfolio(
        [FromBody] SimularAntecipacaoPortfolioRequest body,
        CancellationToken cancellationToken)
    {
        if (body.CaixaDisponivelBrl <= 0)
        {
            return BadRequest(new { detail = "CaixaDisponivelBrl deve ser maior que zero." });
        }

        ResultadoAntecipacaoPortfolioDto resultado = await mediator.Send(
            new SimularAntecipacaoPortfolioQuery(body.CaixaDisponivelBrl, body.TaxaCdiAa),
            cancellationToken);

        return Ok(resultado);
    }
}
