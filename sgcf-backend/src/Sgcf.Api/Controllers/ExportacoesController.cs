using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Exportacao;
using Sgcf.Application.Exportacao.Commands;
using Sgcf.Application.Exportacao.Queries;
using Sgcf.Domain.Exportacao;

namespace Sgcf.Api.Controllers;

public sealed record CreateExportacaoRequest(
    TipoExportacao Tipo,
    string? ParametrosJson);

/// <summary>
/// Endpoints para gerenciamento de jobs de exportação assíncrona.
/// </summary>
[ApiController]
[Route("api/v1/exportacoes")]
[Authorize]
public sealed class ExportacoesController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Enfileira uma nova solicitação de exportação de dados.
    /// O job é criado com status Pendente e processado assincronamente.
    /// Consulte o status e o resultado via GET /api/v1/exportacoes/{id}.
    /// </summary>
    [HttpPost]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<ExportacaoJobDto>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateExportacaoRequest body,
        CancellationToken cancellationToken)
    {
        EnvelopeResponse<ExportacaoJobDto> result =
            await mediator.Send(new CreateExportacaoCommand(body.Tipo, body.ParametrosJson), cancellationToken);

        return StatusCode(StatusCodes.Status202Accepted, result);
    }

    /// <summary>
    /// Retorna o status e o resultado de um job de exportação.
    /// Quando o status for Concluido, o campo ResultadoJson conterá o payload exportado.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<ExportacaoJobDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            EnvelopeResponse<ExportacaoJobDto> result =
                await mediator.Send(new GetExportacaoQuery(id), cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }
}
