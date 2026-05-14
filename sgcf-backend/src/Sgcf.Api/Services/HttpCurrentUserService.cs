using Sgcf.Application.Common;

namespace Sgcf.Api.Services;

internal sealed class HttpCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public string ActorSub =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? accessor.HttpContext?.User.Identity?.Name
        ?? "anonymous";

    public string ActorRole =>
        accessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        ?? "anonymous";
}
