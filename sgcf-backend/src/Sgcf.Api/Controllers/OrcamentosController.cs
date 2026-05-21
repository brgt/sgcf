using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.OrcamentosEncargo;
using Sgcf.Application.OrcamentosEncargo.Commands;
using Sgcf.Application.OrcamentosEncargo.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Corpo da requisição de upsert de orçamento de encargo financeiro.
/// </summary>
public sealed record UpsertOrcamentoEncargoRequest(
    int Ano,
    int Mes,
    string TipoEncargo,
    decimal ValorOrcadoBrl,
    Guid? BancoId,
    Guid? ContratoId,
    string? Observacao);

[ApiController]
[Route("api/v1/orcamentos-encargos")]
public sealed class OrcamentosController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Cadastra ou atualiza o orçamento de um encargo financeiro para o período informado.
    /// A chave de upsert é: (ano, mês, tipo_encargo, banco_id, contrato_id).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<OrcamentoEncargoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertOrcamentoEncargoRequest body,
        CancellationToken ct)
    {
        try
        {
            OrcamentoEncargoDto result = await mediator.Send(
                new UpsertOrcamentoEncargoCommand(
                    body.Ano,
                    body.Mes,
                    body.TipoEncargo,
                    body.ValorOrcadoBrl,
                    body.BancoId,
                    body.ContratoId,
                    body.Observacao),
                ct);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Lista orçamentos de encargo dentro do intervalo de competência informado.
    /// Filtros opcionais: banco_id e tipo_encargo.
    /// </summary>
    [HttpGet]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<OrcamentoEncargoDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int deAno,
        [FromQuery] int deMes,
        [FromQuery] int ateAno,
        [FromQuery] int ateMes,
        [FromQuery] Guid? bancoId,
        [FromQuery] string? tipo,
        CancellationToken ct)
    {
        EnvelopeResponse<IReadOnlyList<OrcamentoEncargoDto>> result = await mediator.Send(
            new GetOrcamentosEncargoQuery(deAno, deMes, ateAno, ateMes, bancoId, tipo),
            ct);

        return Ok(result);
    }
}
