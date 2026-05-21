using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Sgcf.Api.Filters;
using Sgcf.Application.Alertas.Commands;
using Sgcf.Application.Alertas.Dtos;
using Sgcf.Application.Alertas.Queries;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Domain.Alertas;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints do módulo de Alertas do cockpit financeiro.
///
/// <para>
/// Suporta dois padrões de resposta:
/// <list type="bullet">
///   <item>GET endpoints → envelopados em <see cref="EnvelopeResponse{T}"/> via <c>[ProducesEnvelope]</c>.</item>
///   <item>POST de mutação → retornam 204 NoContent (operações idempotentes sem payload de saída).</item>
/// </list>
/// </para>
///
/// <para>
/// Convenção de mapeamento de erros (consistente com demais controllers):
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 409 Conflict</item>
/// </list>
/// </para>
///
/// Task 0.3: AlertasController — GET/POST endpoints.
/// </summary>
[ApiController]
[Route("api/v1/alertas")]
[Authorize]
public sealed class AlertasController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lista alertas paginados com filtros opcionais por perfil, severidade, categoria e status.
    /// O isolamento por tenant é garantido pelo global query filter do EF Core.
    /// </summary>
    /// <param name="perfil">Filtra alertas visíveis para o perfil de cockpit informado.</param>
    /// <param name="severidade">Filtra por nível de urgência.</param>
    /// <param name="categoria">Filtra por domínio funcional.</param>
    /// <param name="status">Filtra pelo ciclo de vida do alerta.</param>
    /// <param name="pageNumber">Número da página (base 1, padrão 1).</param>
    /// <param name="pageSize">Tamanho da página (padrão 20, máx. 100).</param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesEnvelope]
    [ServiceFilter<EnvelopeResultFilter>]
    [ProducesResponseType<PagedResult<AlertaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAlertas(
        [FromQuery] PerfilCockpit? perfil,
        [FromQuery] SeveridadeAlerta? severidade,
        [FromQuery] CategoriaAlerta? categoria,
        [FromQuery] StatusAlerta? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<AlertaDto> resultado = await mediator.Send(
            new ListAlertasQuery(perfil, severidade, categoria, status, pageNumber, pageSize), ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Retorna a contagem de alertas abertos por severidade para o perfil informado.
    /// Usado pelo cockpit para exibir os badges de notificação.
    /// </summary>
    /// <param name="perfil">Perfil de cockpit para o qual os contadores são calculados. Obrigatório.</param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpGet("contadores")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesEnvelope]
    [ServiceFilter<EnvelopeResultFilter>]
    [ProducesResponseType<ContadoresAlertaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContadores(
        [FromQuery] PerfilCockpit perfil,
        CancellationToken ct = default)
    {
        ContadoresAlertaDto resultado = await mediator.Send(
            new GetContadoresAlertasQuery(perfil), ct);

        return Ok(resultado);
    }

    /// <summary>
    /// Dispensa o alerta, removendo-o da fila ativa do cockpit.
    /// A operação é idempotente: chamadas subsequentes com o mesmo Id retornam 204 sem erro.
    /// </summary>
    /// <param name="id">Id do alerta a dispensar.</param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpPost("{id:guid}/dispensar")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dispensar(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new DispensarAlertaCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Marca o alerta como lido.
    /// A operação é idempotente quando o alerta já está no status Lido.
    /// Retorna 409 se o alerta estiver Dispensado (transição inválida de domínio).
    /// </summary>
    /// <param name="id">Id do alerta a marcar como lido.</param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpPost("{id:guid}/marcar-como-lido")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarcarComoLido(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new MarcarAlertaComoLidoCommand(id), ct);
            return NoContent();
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
}
