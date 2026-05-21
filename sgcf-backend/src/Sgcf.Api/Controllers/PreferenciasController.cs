using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Preferencias;
using Sgcf.Application.Preferencias.Commands;
using Sgcf.Application.Preferencias.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints para persistir e recuperar preferências de UI por usuário.
/// GAP-CKP-24 — Task 4.9.
/// </summary>
[ApiController]
[Route("api/v1/preferencias")]
public sealed class PreferenciasController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retorna todas as preferências do usuário autenticado no tenant atual.
    /// </summary>
    [HttpGet]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<PreferenciaUsuarioDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        EnvelopeResponse<IReadOnlyList<PreferenciaUsuarioDto>> resultado =
            await mediator.Send(new GetPreferenciasQuery(userId), ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Cria ou atualiza a preferência identificada pela <paramref name="chave"/>.
    /// </summary>
    /// <param name="chave">Chave da preferência (ex: "theme", "cockpit.layout").</param>
    /// <param name="request">Corpo contendo o valor da preferência.</param>
    [HttpPut("{chave}")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<PreferenciaUsuarioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        string chave,
        [FromBody] UpsertPreferenciaRequest request,
        CancellationToken ct)
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        try
        {
            PreferenciaUsuarioDto resultado = await mediator.Send(
                new UpsertPreferenciaCommand(userId, chave, request.Valor), ct);

            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a preferência identificada pela <paramref name="chave"/>.
    /// </summary>
    /// <param name="chave">Chave da preferência a remover.</param>
    [HttpDelete("{chave}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string chave, CancellationToken ct)
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        try
        {
            await mediator.Send(new DeletePreferenciaCommand(userId, chave), ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

/// <summary>
/// Corpo da requisição para upsert de preferência.
/// </summary>
/// <param name="Valor">Valor serializado da preferência. Máximo 4000 caracteres.</param>
public sealed record UpsertPreferenciaRequest(string Valor);
