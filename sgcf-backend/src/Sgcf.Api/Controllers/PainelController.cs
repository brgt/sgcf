using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Authorization;
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
public sealed class PainelController(IMediator mediator, IClock clock) : ControllerBase
{
    /// <summary>
    /// Retorna o painel consolidado de dívida com breakdown por moeda, ajuste MTM e alertas.
    /// </summary>
    [HttpGet("divida")]
    [Authorize(Policy = Policies.Leitura)]
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
    [Authorize(Policy = Policies.Leitura)]
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
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<CalendarioVencimentosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCalendarioVencimentos(
        [FromQuery] int? ano,
        [FromQuery] Guid? bancoId,
        [FromQuery] string? modalidade,
        [FromQuery] string? moeda,
        [FromQuery] decimal? cdiAnualPct,
        CancellationToken cancellationToken)
    {
        if (!ano.HasValue)
        {
            return BadRequest(new { detail = "O parâmetro 'ano' é obrigatório. Ex: ?ano=2026" });
        }

        CalendarioVencimentosDto resultado = await mediator.Send(
            new GetCalendarioVencimentosQuery(ano.Value, bancoId, modalidade, moeda, cdiAnualPct),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna os KPIs executivos do dashboard (dívida, custo médio, prazo médio, share por banco).
    /// </summary>
    [HttpGet("kpis")]
    [Authorize(Policy = Policies.Executivo)]
    [ProducesResponseType<KpiDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpis(CancellationToken cancellationToken)
    {
        KpiDto resultado = await mediator.Send(new GetDashboardKpisQuery(), cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// Retorna o quadro da dívida para o ano informado: snapshot atual, projeção mês a mês e sumário anual.
    /// Sem <c>ano</c> usa o ano corrente. Apenas o ano corrente é suportado no MVP (Q9).
    /// Quando <c>cenarioId</c> é informado, a projeção incorpora as captações hipotéticas do cenário (AD-9).
    /// </summary>
    [HttpGet("quadro-divida")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<QuadroDividaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetQuadroDivida(
        [FromQuery] int? ano,
        [FromQuery] Guid? cenarioId,
        CancellationToken cancellationToken)
    {
        int anoEfetivo = ano ?? clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"])
            .Year;

        try
        {
            QuadroDividaDto resultado = await mediator.Send(
                new GetQuadroDividaQuery(anoEfetivo, cenarioId), cancellationToken);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // AD-7: ano fora do suporte MVP (Q9) ou AnoBase incompatível → 409 Conflict
            return Conflict(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Cadastra ou atualiza o EBITDA mensal (upsert). Requer perfil administrativo.
    /// </summary>
    [HttpPost("ebitda")]
    [Authorize(Policy = Policies.Auditoria)]
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
