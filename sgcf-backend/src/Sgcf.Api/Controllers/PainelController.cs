using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Contabilidade.Commands;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Commands;
using Sgcf.Application.Painel.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Requisição para cadastrar ou atualizar o EBITDA mensal.
/// </summary>
public sealed record UpsertEbitdaRequest(int Ano, int Mes, decimal ValorBrl);

/// <summary>
/// Requisição para cadastrar ou atualizar os dados contábeis mensais.
/// </summary>
public sealed record UpsertDadosContabeisRequest(
    int Ano,
    int Mes,
    decimal PatrimonioLiquidoBrl,
    decimal DespesaFinanceiraBrl);

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
    /// Retorna a dívida ativa agregada por modalidade de contrato, com valor BRL e percentual
    /// de participação de cada modalidade no total da carteira.
    /// </summary>
    [HttpGet("divida/breakdown-modalidade")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<BreakdownModalidadeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBreakdownModalidade(CancellationToken cancellationToken)
    {
        EnvelopeResponse<BreakdownModalidadeDto> resultado = await mediator.Send(
            new GetBreakdownModalidadeQuery(),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna a curva de vencimentos futuros agrupada em buckets temporais (mês, trimestre ou ano)
    /// com breakdown por modalidade de contrato. Todos os valores são convertidos para BRL.
    /// </summary>
    /// <param name="meses">Horizonte em meses: 12, 24, 36 ou 60. Qualquer outro valor usa 12.</param>
    /// <param name="granularidade">Granularidade dos buckets: Mes, Trimestre ou Ano.</param>
    /// <param name="bancoId">Filtro opcional por banco credor.</param>
    /// <param name="modalidade">Filtro opcional por modalidade de contrato (ex: CapitalDeGiro).</param>
    /// <param name="moeda">Filtro opcional por moeda original do contrato (ex: Brl, Usd).</param>
    [HttpGet("vencimentos/horizonte")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<CurvaVencimentosDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurvaVencimentos(
        [FromQuery] int meses = 12,
        [FromQuery] GranularidadeHorizonte granularidade = GranularidadeHorizonte.Mes,
        [FromQuery] Guid? bancoId = null,
        [FromQuery] string? modalidade = null,
        [FromQuery] string? moeda = null,
        CancellationToken ct = default)
    {
        EnvelopeResponse<CurvaVencimentosDto> resultado = await mediator.Send(
            new GetCurvaVencimentosQuery(meses, granularidade, bancoId, modalidade, moeda),
            ct);

        return Ok(resultado);
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

    /// <summary>
    /// Cadastra ou atualiza os dados contábeis mensais (Patrimônio Líquido e Despesa Financeira).
    /// Requer perfil com permissão de escrita.
    /// </summary>
    [HttpPost("dados-contabeis")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertDadosContabeis(
        [FromBody] UpsertDadosContabeisRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(
                new UpsertDadosContabeisCommand(
                    body.Ano,
                    body.Mes,
                    body.PatrimonioLiquidoBrl,
                    body.DespesaFinanceiraBrl),
                cancellationToken);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Retorna a estrutura de capital consolidada com ICR (EBITDA / Despesa Financeira).
    /// Quando dados contábeis estão ausentes, retorna completude Parcial com alerta.
    /// </summary>
    [HttpGet("estrutura-capital")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<EstruturaCapitalDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEstruturaCapital(CancellationToken cancellationToken)
    {
        EnvelopeResponse<EstruturaCapitalDto> resultado = await mediator.Send(
            new GetEstruturaCapitalQuery(),
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna os contratos inadimplentes com dias de atraso médio e exposição total em BRL.
    /// Considera apenas eventos de cronograma com <c>Status == Atrasado</c> e vencidos antes de hoje.
    /// </summary>
    [HttpGet("inadimplencia")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<InadimplenciaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInadimplencia(CancellationToken ct)
    {
        EnvelopeResponse<InadimplenciaDto> resultado = await mediator.Send(
            new GetInadimplenciaQuery(),
            ct);

        return Ok(resultado);
    }
}
