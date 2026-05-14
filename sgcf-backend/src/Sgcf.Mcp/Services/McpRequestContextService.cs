using System.Security.Cryptography;
using System.Text;
using Sgcf.Application.Common;

namespace Sgcf.Mcp.Services;

internal sealed class McpRequestContextService(IHttpContextAccessor accessor) : IRequestContextService
{
    public string Source => "mcp";

    public Guid RequestId
    {
        get
        {
            HttpContext? ctx = accessor.HttpContext;
            if (ctx is null)
            {
                return Guid.CreateVersion7();
            }

            if (ctx.Items.TryGetValue("_AuditRequestId", out object? cached) && cached is Guid id)
            {
                return id;
            }

            Guid newId = ctx.Request.Headers.TryGetValue("X-Request-Id", out var hdr)
                && Guid.TryParse(hdr, out Guid parsed)
                ? parsed
                : Guid.CreateVersion7();

            ctx.Items["_AuditRequestId"] = newId;
            return newId;
        }
    }

    public byte[]? IpHash
    {
        get
        {
            string? ip = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            return ip is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        }
    }
}
