using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Contabilidade.Queries;
using System.Collections.Generic;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Gerencia o modelo global de plano de contas.
///
/// Acesso exclusivo a super-admin — o modelo é global e serve de base para
/// clonagem ao provisionar novos tenants. Alterações no modelo não retroagem
/// a tenants já provisionados (cada um tem cópia independente).
///
/// Rota: /api/v1/admin/plano-contas-modelo (bypassa TenantResolverMiddleware
/// pelo prefixo /admin/, igual a /admin/tenants).
/// </summary>
[ApiController]
[Route("api/v1/admin/plano-contas-modelo")]
[Authorize(Policy = Policies.SuperAdmin)]
public sealed class PlanoContasModeloController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lista todas as entradas do modelo global de plano de contas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlanoContasModeloDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        IReadOnlyList<PlanoContasModeloDto> result = await mediator.Send(new ListContasModeloQuery(), ct);
        return Ok(result);
    }
}
