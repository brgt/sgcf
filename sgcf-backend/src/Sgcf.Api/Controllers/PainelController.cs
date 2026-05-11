using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Commands;
using Sgcf.Application.Painel.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Requisição para cadastrar ou atualizar o EBITDA mensal.
/// </summary>
public sealed record UpsertEbitdaRequest(int Ano, int Mes, decimal ValorBrl);

/// <summary>
/// Endpoints do painel executivo: dívida consolidada, garantias, calendário de vencimentos e KPIs.
/// </summary>
[ApiController]
[Route("api/v1/painel")]
public sealed class PainelController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retorna o painel consolidado de dívida com breakdown por moeda, ajuste MTM e alertas.
    /// </summary>
    [HttpGet("divida")]
    [ProducesResponseType<PainelDividaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPainelDivida(
        [FromQuery] Guid? bancoId,
        [FromQuery] string? modalidade,
        CancellationToken cancellationToken)
    {
        PainelDividaDto resultado = await mediator.Send(
            new GetPainelDividaQuery(bancoId, modalidade),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna o painel de garantias ativas com distribuição por tipo e por banco.
    /// </summary>
    [HttpGet("garantias")]
    [ProducesResponseType<PainelGarantiasDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPainelGarantias(CancellationToken cancellationToken)
    {
        PainelGarantiasDto resultado = await mediator.Send(
            new GetPainelGarantiasQuery(),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna o calendário de vencimentos de parcelas abertas para o ano informado.
    /// </summary>
    [HttpGet("vencimentos")]
    [ProducesResponseType<CalendarioVencimentosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCalendarioVencimentos(
        [FromQuery] int? ano,
        [FromQuery] Guid? bancoId,
        [FromQuery] string? modalidade,
        [FromQuery] string? moeda,
        CancellationToken cancellationToken)
    {
        if (!ano.HasValue)
        {
            return BadRequest(new { detail = "O parâmetro 'ano' é obrigatório. Ex: ?ano=2026" });
        }

        CalendarioVencimentosDto resultado = await mediator.Send(
            new GetCalendarioVencimentosQuery(ano.Value, bancoId, modalidade, moeda),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna os KPIs executivos do dashboard (dívida, custo médio, prazo médio, share por banco).
    /// </summary>
    [HttpGet("kpis")]
    [ProducesResponseType<KpiDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpis(CancellationToken cancellationToken)
    {
        KpiDto resultado = await mediator.Send(new GetDashboardKpisQuery(), cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// Cadastra ou atualiza o EBITDA mensal (upsert). Requer perfil administrativo.
    /// </summary>
    [HttpPost("ebitda")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertEbitda(
        [FromBody] UpsertEbitdaRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            string usuario = User.Identity?.Name ?? "system";

            await mediator.Send(
                new UpsertEbitdaMensalCommand(body.Ano, body.Mes, body.ValorBrl, usuario),
                cancellationToken);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
