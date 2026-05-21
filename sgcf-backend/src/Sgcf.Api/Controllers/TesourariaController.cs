using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Tesouraria;
using Sgcf.Application.Tesouraria.Commands;
using Sgcf.Application.Tesouraria.Queries;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints de tesouraria: saldos de caixa, posição consolidada e efetividade de hedge.
/// </summary>
[ApiController]
[Route("api/v1/tesouraria")]
[Authorize]
public sealed class TesourariaController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Cria ou atualiza em lote os saldos de caixa para as contas e datas informadas.
    /// Em caso de atualização, persiste diff de auditoria com o valor anterior.
    /// </summary>
    [HttpPost("saldos")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<IReadOnlyList<SaldoCaixaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertSaldos(
        [FromBody] IReadOnlyList<UpsertSaldoCaixaItemDto> itens,
        CancellationToken ct)
    {
        try
        {
            IReadOnlyList<SaldoCaixaDto> resultado =
                await mediator.Send(new UpsertLoteSaldoCaixaCommand(itens), ct);
            return Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Consulta a série histórica de saldos de caixa de uma conta em um intervalo de datas.
    /// </summary>
    [HttpGet("saldos")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<SaldoCaixaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSaldos(
        [FromQuery] Guid contaId,
        [FromQuery] string dataDe,
        [FromQuery] string dataAte,
        CancellationToken ct)
    {
        try
        {
            IReadOnlyList<SaldoCaixaDto> resultado =
                await mediator.Send(new GetSaldoCaixaQuery(contaId, dataDe, dataAte), ct);
            return Ok(resultado);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return BadRequest(new { detail = "dataDe e dataAte devem ser datas ISO válidas (yyyy-MM-dd)." });
        }
    }

    /// <summary>
    /// Retorna a posição consolidada de caixa em BRL para a data informada (default: hoje BRT).
    /// </summary>
    [HttpGet("posicao-caixa")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<PosicaoCaixaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPosicaoCaixa(
        [FromQuery] string? dataReferencia,
        CancellationToken ct)
    {
        EnvelopeResponse<PosicaoCaixaDto> resultado =
            await mediator.Send(new GetPosicaoCaixaQuery(dataReferencia), ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Retorna a efetividade de hedge consolidada: exposição por moeda, nocional de cobertura,
    /// taxa de cobertura e MtM atual de cada instrumento NDF ativo.
    /// </summary>
    [HttpGet("hedge-efetividade")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<HedgeEfetividadeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHedgeEfetividade(CancellationToken ct)
    {
        EnvelopeResponse<HedgeEfetividadeDto> resultado =
            await mediator.Send(new GetHedgeEfetividadeQuery(), ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Cria em lote um ou mais eventos manuais de fluxo de caixa (entradas ou saídas).
    /// </summary>
    [HttpPost("eventos-fluxo")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<IReadOnlyList<EventoFluxoCaixaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEventosFluxo(
        [FromBody] IReadOnlyList<CreateEventoFluxoCaixaItemDto> itens,
        CancellationToken ct)
    {
        try
        {
            IReadOnlyList<EventoFluxoCaixaDto> resultado =
                await mediator.Send(new CreateEventoFluxoCaixaCommand(itens), ct);
            return Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Retorna a projeção de fluxo de caixa diária para o período informado.
    /// Combina eventos previstos do cronograma de contratos com eventos manuais registrados.
    /// Período default: hoje BRT até hoje + 30 dias. Máximo: 90 dias.
    /// </summary>
    [HttpGet("fluxo-caixa")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<FluxoCaixaDiaDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFluxoCaixa(
        [FromQuery] string? dataDe,
        [FromQuery] string? dataAte,
        CancellationToken ct)
    {
        EnvelopeResponse<IReadOnlyList<FluxoCaixaDiaDto>> resultado =
            await mediator.Send(new GetFluxoCaixaQuery(dataDe, dataAte), ct);
        return Ok(resultado);
    }
}
