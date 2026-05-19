using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Sgcf.Application.Authorization;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Application.Simulacao.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints do módulo Simulações de Contratação.
///
/// <para>
/// SPEC §7.4 — Fase 2 Task 2.6.
/// </para>
///
/// <para>
/// Convenção de mapeamento de erros (consistente com demais controllers):
/// <list type="bullet">
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
///   <item><see cref="InvalidOperationException"/> → 409 Conflict</item>
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/simulacoes")]
public sealed class SimulacoesController(IMediator mediator) : ControllerBase
{
    // ── Preview de cronograma hipotético ─────────────────────────────────────

    /// <summary>
    /// Pré-visualiza o cronograma de uma simulação hipotética sem persistir nada.
    ///
    /// O endpoint é stateless (pure compute): constrói internamente uma
    /// <c>SimulacaoContratacao</c> temporária, delega ao motor de cronograma e descarta.
    /// Pode ser chamado sem cenário existente.
    ///
    /// Erros possíveis:
    /// - 400 — invariante de domínio violada (ex: ValorPrincipal = 0)
    ///          ou CDI+Spread sem CdiReferenciaAaPercentual.
    /// - 409 — estado interno inválido (raro em uso normal).
    /// </summary>
    /// <param name="query">
    /// Corpo com todos os campos de uma simulação (sem cenarioId) e,
    /// opcionalmente, o CDI de referência para operações indexadas ao CDI.
    /// </param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpPost("cronograma-hipotetico")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CronogramaHipoteticoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewCronograma(
        [FromBody] SimularCronogramaHipoteticoQuery query,
        CancellationToken ct)
    {
        try
        {
            CronogramaHipoteticoDto resultado = await mediator.Send(query, ct);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
