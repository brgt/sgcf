using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Sistema;
using Sgcf.Application.Sistema.Commands;
using Sgcf.Application.Sistema.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints para gerenciamento dos parâmetros globais do sistema.
/// Fase 3 Task 3.4 — tetão mensal configurável (D-11).
/// </summary>
[ApiController]
[Route("api/v1/parametros-sistema")]
public sealed class ParametrosSistemaController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retorna os parâmetros globais do sistema.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<ParametrosSistemaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        ParametrosSistemaDto result = await mediator.Send(new GetParametrosSistemaQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza o tetão mensal de movimentação.
    /// Passe <c>null</c> em <c>valor</c> para remover o limite.
    /// </summary>
    [HttpPatch("tetao-mensal")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ParametrosSistemaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarTetaoMensal(
        [FromBody] AtualizarTetaoMensalRequest request,
        CancellationToken ct)
    {
        try
        {
            ParametrosSistemaDto result = await mediator.Send(
                new AtualizarTetaoMensalCommand(request.Valor), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            // ArgumentOutOfRangeException é subclasse de ArgumentException — capturado aqui também
            return BadRequest(new { error = ex.Message });
        }
    }
}

/// <summary>
/// Corpo da requisição para atualizar o tetão mensal.
/// </summary>
/// <param name="Valor">Novo valor em BRL. Null remove o limite.</param>
public sealed record AtualizarTetaoMensalRequest(decimal? Valor);
