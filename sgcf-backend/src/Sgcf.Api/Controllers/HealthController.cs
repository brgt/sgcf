using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Tenancy.Services;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints de saúde operacional — diagnóstico de RLS e infraestrutura de tenancy.
/// Acesso exclusivo a super-admin.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController(IRlsHealthCheckService rlsCheck) : ControllerBase
{
    /// <summary>
    /// Valida que Row-Level Security está habilitada e funcionando em todas as tabelas tenant-scoped.
    ///
    /// Retorna 200 quando todos os 4 checks passam (rls_enabled, policies_present, canary_no_context, canary_with_proxys).
    /// Retorna 503 quando qualquer check falha — detalhe por check no corpo da resposta.
    ///
    /// Use este endpoint para:
    /// - Confirmar saúde do RLS após migrations.
    /// - Diagnosticar vazamentos cross-tenant suspeitos.
    /// - Monitoramento ativo via Prometheus blackbox exporter.
    /// </summary>
    [HttpGet("rls")]
    [Authorize(Policy = Policies.SuperAdmin)]
    [ProducesResponseType<RlsHealthReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<RlsHealthReport>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Rls(CancellationToken ct)
    {
        RlsHealthReport report = await rlsCheck.CheckAsync(ct);
        int statusCode = report.Status == "healthy"
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, report);
    }
}
