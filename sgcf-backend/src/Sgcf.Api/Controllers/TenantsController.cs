using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Tenancy;
using Sgcf.Application.Tenancy.Commands;
using Sgcf.Application.Tenancy.Queries;
using Sgcf.Application.Tenancy.Services;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints de administração global de tenants — exclusivo para super-admins Nordware.
/// Todos os endpoints exigem a policy SuperAdmin (role "super-admin" no JWT).
/// </summary>
[ApiController]
[Route("api/v1/admin/tenants")]
[Authorize(Policy = Policies.SuperAdmin)]
public sealed class TenantsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<TenantDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] StatusTenant? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        PagedResult<TenantDto> result = await mediator.Send(
            new ListTenantsQuery(status, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{idOrSlug}")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string idOrSlug, CancellationToken ct)
    {
        try
        {
            TenantDto dto = await mediator.Send(new GetTenantQuery(idOrSlug), ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ProducesResponseType<TenantDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarTenantCommand command, CancellationToken ct)
    {
        TenantDto dto = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { idOrSlug = dto.Slug }, dto);
    }

    [HttpPatch("{idOrSlug}")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        string idOrSlug,
        [FromBody] AtualizarTenantRequest request,
        CancellationToken ct)
    {
        try
        {
            TenantDto dto = await mediator.Send(
                new AtualizarTenantCommand(idOrSlug, request.Plano), ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{idOrSlug}/suspender")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspender(
        string idOrSlug,
        [FromBody] SuspenderTenantRequest request,
        CancellationToken ct)
    {
        try
        {
            TenantDto dto = await mediator.Send(
                new SuspenderTenantCommand(idOrSlug, request.Motivo), ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{idOrSlug}/reativar")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reativar(string idOrSlug, CancellationToken ct)
    {
        try
        {
            TenantDto dto = await mediator.Send(new ReativarTenantCommand(idOrSlug), ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{idOrSlug}/arquivar")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Arquivar(string idOrSlug, CancellationToken ct)
    {
        try
        {
            TenantDto dto = await mediator.Send(new ArquivarTenantCommand(idOrSlug), ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Provisiona os dados mestres iniciais de um tenant (idempotente).
    /// Retorna 200 com os contadores de registros criados/ignorados por categoria.
    /// Pode ser chamado múltiplas vezes com segurança — segunda chamada retorna criados=0.
    /// </summary>
    [HttpPost("{idOrSlug}/provisionar")]
    [ProducesResponseType<ResultadoProvisionamento>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Provisionar(string idOrSlug, CancellationToken ct)
    {
        TenantDto? tenantDto;
        try
        {
            tenantDto = await mediator.Send(new GetTenantQuery(idOrSlug), ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        try
        {
            ResultadoProvisionamento resultado =
                await mediator.Send(new ProvisionarTenantCommand(tenantDto.Id), ct);
            return Ok(resultado);
        }
        catch (TenantSuspendoException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Tenant suspenso",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (TenantArquivadoException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Tenant arquivado",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
    }
}

public sealed record AtualizarTenantRequest(PlanoAssinatura? Plano);

public sealed record SuspenderTenantRequest(string Motivo);
