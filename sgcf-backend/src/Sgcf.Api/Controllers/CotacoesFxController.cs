using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Sgcf.Application.Authorization;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cambio.Commands;
using Sgcf.Application.Cambio.Queries;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Cadastro/consulta manual de cotações cambiais (PTAX). Uso administrativo / contingência
/// quando a ingestão automática do BCB não está disponível. SPEC §4.2.
/// </summary>
[ApiController]
[Route("api/v1/cotacoes-fx")]
public sealed class CotacoesFxController(IMediator mediator) : ControllerBase
{
    /// <summary>Registra (upsert idempotente) uma cotação cambial. Grava PtaxD0 por padrão.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CotacaoFxDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarCotacaoFxCommand command, CancellationToken ct)
    {
        CotacaoFxDto result = await mediator.Send(command, ct);

        string ate = DateOnly.FromDateTime(result.Momento.UtcDateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return CreatedAtAction(
            nameof(Buscar),
            new { moeda = result.MoedaBase, tipo = result.Tipo, ate },
            result);
    }

    /// <summary>Conferência: retorna a cotação mais recente para (moeda, tipo) até a data informada.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CotacaoFxDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string moeda,
        [FromQuery] string tipo,
        [FromQuery] DateOnly ate,
        CancellationToken ct)
    {
        if (!Enum.TryParse<Moeda>(moeda, true, out Moeda moedaEnum))
        {
            return BadRequest($"Moeda inválida: {moeda}. Valores aceitos: {string.Join(", ", Enum.GetNames<Moeda>())}.");
        }

        if (!Enum.TryParse<TipoCotacao>(tipo, true, out TipoCotacao tipoEnum))
        {
            return BadRequest($"Tipo inválido: {tipo}. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoCotacao>())}.");
        }

        LocalDate ateLocal = new(ate.Year, ate.Month, ate.Day);
        CotacaoFxDto? result = await mediator.Send(new BuscarCotacaoFxQuery(moedaEnum, tipoEnum, ateLocal), ct);

        return result is null ? NotFound() : Ok(result);
    }
}
