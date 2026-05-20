using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Api.Middleware;

/// <summary>
/// Resolve o tenant para cada request e popula <see cref="TenantContext"/> antes de
/// <c>UseAuthorization()</c> ser executado.
///
/// Fluxo:
/// 1. Paths de bypass (/api/v1/admin, /health, /swagger) passam sem resolução.
/// 2. Extrai <c>tenant_id</c> do claim JWT.
/// 3. Super-admin pode substituir via header <c>X-Tenant-Id</c> (impersonation).
/// 4. Busca no <see cref="ITenantCache"/>; se miss, consulta o banco via <see cref="ITenantRepository"/>.
/// 5. Tenant suspenso ou arquivado → 403 Forbidden.
/// 6. Chama <see cref="TenantContext.Resolve"/> e prossegue.
/// </summary>
internal sealed partial class TenantResolverMiddleware(
    RequestDelegate next,
    ILogger<TenantResolverMiddleware> logger)
{
    // Prefixos de path que não exigem contexto de tenant.
    private static readonly string[] BypassPrefixes =
    [
        "/api/v1/admin",
        "/health",
        "/swagger",
    ];

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "TenantResolver: path '{Path}' está no bypass — skipping resolução de tenant.")]
    private static partial void LogBypass(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "TenantResolver: tenant {TenantId} ({TenantSlug}) resolvido. SuperAdmin={SuperAdmin} Impersonating={Impersonating}.")]
    private static partial void LogResolvido(
        ILogger logger, Guid tenantId, string tenantSlug, bool superAdmin, bool impersonating);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TenantResolver: tenant {TenantId} está com status {Status} — acesso negado.")]
    private static partial void LogTenantInativo(ILogger logger, Guid tenantId, StatusTenant status);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TenantResolver: claim 'tenant_id' ausente ou inválido para '{Path}'.")]
    private static partial void LogClaimAusente(ILogger logger, string path);

    public async Task InvokeAsync(
        HttpContext ctx,
        ITenantContext tenantContext,
        ITenantCache cache,
        ITenantRepository repo)
    {
        string path = ctx.Request.Path.Value ?? string.Empty;

        if (IsBypassPath(path))
        {
            LogBypass(logger, path);
            await next(ctx);
            return;
        }

        bool isSuperAdmin = ctx.User.IsInRole("super-admin");
        Guid? tenantId = ResolveTenantId(ctx, isSuperAdmin, out bool isImpersonating);

        if (!tenantId.HasValue)
        {
            LogClaimAusente(logger, path);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        TenantInfo? info = await cache.GetByIdAsync(tenantId.Value, ctx.RequestAborted);

        if (info is null)
        {
            Sgcf.Domain.Tenancy.Tenant? tenant = await repo.GetAsync(tenantId.Value, ctx.RequestAborted);
            if (tenant is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            info = new TenantInfo(tenant.Id, tenant.Slug, tenant.Status);
            await cache.SetAsync(info, ctx.RequestAborted);
        }

        if (info.Status is StatusTenant.Suspenso or StatusTenant.Arquivado)
        {
            LogTenantInativo(logger, info.Id, info.Status);
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        tenantContext.Resolve(info.Id, info.Slug, isSuperAdmin, isImpersonating);

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = info.Id,
            ["TenantSlug"] = info.Slug,
            ["Impersonating"] = isImpersonating,
        });

        LogResolvido(logger, info.Id, info.Slug, isSuperAdmin, isImpersonating);
        await next(ctx);
    }

    private static bool IsBypassPath(string path)
    {
        foreach (string prefix in BypassPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Guid? ResolveTenantId(HttpContext ctx, bool isSuperAdmin, out bool isImpersonating)
    {
        isImpersonating = false;

        // Super-admin pode impersonar qualquer tenant via header.
        if (isSuperAdmin && ctx.Request.Headers.TryGetValue("X-Tenant-Id", out Microsoft.Extensions.Primitives.StringValues headerVal))
        {
            if (Guid.TryParse(headerVal.ToString(), out Guid headerTenantId))
            {
                isImpersonating = true;
                return headerTenantId;
            }
        }

        // Claim padrão do JWT para usuários normais.
        string? claimValue = ctx.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out Guid claimTenantId))
        {
            return claimTenantId;
        }

        return null;
    }
}
