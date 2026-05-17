using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Gerencia limites operacionais de bancos por modalidade.
/// O limite disponível é validado ao adicionar bancos a uma cotação (SPEC §3.2 regra 8).
/// </summary>
[ApiController]
[Route("api/v1/limites-banco")]
public sealed class LimitesBancoController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lista limites operacionais com filtros opcionais por banco e modalidade.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<LimiteBancoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? bancoId,
        [FromQuery] string? modalidade,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LimiteBancoDto> result = await mediator.Send(
            new ListLimitesBancoQuery(bancoId, modalidade),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retorna um limite operacional pelo seu identificador.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<LimiteBancoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            LimiteBancoDto result = await mediator.Send(new GetLimiteBancoByIdQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Cria novo limite operacional para banco/modalidade. Política: Admin.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<LimiteBancoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar(
        [FromBody] CreateLimiteBancoCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            LimiteBancoDto result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // ArgumentOutOfRangeException is a subtype — handled here as well.
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza o limite operacional existente (semântica PATCH).
    /// Garante e substitui garantias exigidas quando fornecidas. Política: Admin.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<LimiteBancoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] UpdateLimiteBancoCommand command,
        CancellationToken cancellationToken)
    {
        UpdateLimiteBancoCommand cmd = command with { LimiteId = id };

        try
        {
            LimiteBancoDto result = await mediator.Send(cmd, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // ArgumentOutOfRangeException is a subtype — handled here as well.
            return BadRequest(new { error = ex.Message });
        }
    }
}
