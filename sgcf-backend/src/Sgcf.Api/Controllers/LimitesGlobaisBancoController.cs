using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Gerencia limites globais (guarda-chuva) de bancos.
/// Cada banco pode ter no máximo um limite vigente por período (SPEC §7 — LG-05).
/// </summary>
[ApiController]
[Route("api/v1/limites-globais-banco")]
public sealed class LimitesGlobaisBancoController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Cria um novo limite global para o banco informado. Política: Admin.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<LimiteGlobalBancoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarLimiteGlobalBancoCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            LimiteGlobalBancoDto result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lista limites globais com filtro opcional por banco. Política: Leitura.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<LimiteGlobalBancoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? bancoId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LimiteGlobalBancoDto> result = await mediator.Send(
            new ListarLimitesGlobaisBancoQuery(bancoId),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retorna um limite global pelo seu identificador. Política: Leitura.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<LimiteGlobalBancoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            LimiteGlobalBancoDto result = await mediator.Send(
                new GetLimiteGlobalBancoQuery(id),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Atualiza um limite global existente com semântica PATCH. Política: Admin.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<LimiteGlobalBancoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarLimiteGlobalBancoCommand command,
        CancellationToken cancellationToken)
    {
        AtualizarLimiteGlobalBancoCommand cmd = command with { Id = id };

        try
        {
            LimiteGlobalBancoDto result = await mediator.Send(cmd, cancellationToken);
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
    }

    /// <summary>
    /// Encerra a vigência de um limite global definindo a data de fim. Política: Admin.
    /// </summary>
    [HttpDelete("{id:guid}/vigencia")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EncerrarVigencia(
        Guid id,
        [FromBody] EncerrarVigenciaRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(
                new EncerrarVigenciaLimiteGlobalBancoCommand(id, request.DataFim),
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

public sealed record EncerrarVigenciaRequest(DateOnly DataFim);
