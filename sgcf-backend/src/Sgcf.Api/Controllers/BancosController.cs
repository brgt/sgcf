using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Bancos;
using Sgcf.Application.Bancos.Commands;
using Sgcf.Application.Bancos.Queries;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/bancos")]
public sealed class BancosController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BancoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        IReadOnlyList<BancoDto> result = await mediator.Send(new ListBancosQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BancoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            BancoDto result = await mediator.Send(new GetBancoQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ProducesResponseType<BancoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBancoCommand command, CancellationToken ct)
    {
        BancoDto result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/config-antecipacao")]
    [ProducesResponseType<BancoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateConfig(Guid id, [FromBody] UpdateBancoConfigRequest request, CancellationToken ct)
    {
        try
        {
            UpdateBancoConfigCommand command = new(
                id,
                request.AceitaLiquidacaoTotal,
                request.AceitaLiquidacaoParcial,
                request.ExigeAnuenciaExpressa,
                request.ExigeParcelaInteira,
                request.AvisoPrevioMinDiasUteis,
                request.PadraoAntecipacao,
                request.ValorMinimoParcialPct,
                request.BreakFundingFeePct,
                request.TlaPctSobreSaldo,
                request.TlaPctPorMesRemanescente,
                request.ObservacoesAntecipacao);

            BancoDto result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public sealed record UpdateBancoConfigRequest(
    bool AceitaLiquidacaoTotal,
    bool AceitaLiquidacaoParcial,
    bool ExigeAnuenciaExpressa,
    bool ExigeParcelaInteira,
    int AvisoPrevioMinDiasUteis,
    string PadraoAntecipacao,
    decimal? ValorMinimoParcialPct,
    decimal? BreakFundingFeePct,
    decimal? TlaPctSobreSaldo,
    decimal? TlaPctPorMesRemanescente,
    string? ObservacoesAntecipacao);
