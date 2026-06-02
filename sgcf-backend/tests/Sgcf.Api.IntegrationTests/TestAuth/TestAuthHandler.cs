using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sgcf.Api.IntegrationTests.TestAuth;

/// <summary>
/// Minimal authentication handler for integration tests.
///
/// Reads the caller identity from the <c>X-Test-User-Sub</c> request header.
/// Falls back to <c>"dev-user-id"</c> when the header is absent (preserving the
/// behaviour of the existing <c>fixture.CreateAuthenticatedClient()</c> helper).
///
/// Registered as the default authentication scheme in test factories that need to
/// exercise per-user cache isolation (e.g. IdempotencyFilter multi-user tests).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The scheme name registered in test DI.</summary>
    public const string SchemeName = "TestAuth";

    /// <summary>Header name used to inject a custom sub value per request.</summary>
    public const string SubHeader = "X-Test-User-Sub";

    /// <summary>
    /// Optional header to override the tenant_id claim in tests.
    /// Falls back to <see cref="ProxysDevTenant.Id"/> when absent,
    /// preserving backward compatibility with all existing tests.
    /// </summary>
    public const string TenantIdHeader = "X-Test-Tenant-Id";

    /// <summary>
    /// Optional header to override the roles claimed by the test principal
    /// (comma-separated). Falls back to <c>admin,tesouraria</c> when absent,
    /// preserving backward compatibility with all existing tests.
    /// </summary>
    public const string RolesHeader = "X-Test-Roles";

    private static readonly string[] DefaultRoles = ["admin", "tesouraria"];

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Require at least an Authorization header so unauthenticated requests still fail.
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        string sub = Request.Headers[SubHeader].FirstOrDefault() ?? "dev-user-id";

        // X-Test-Tenant-Id allows cross-tenant isolation tests to impersonate different tenants.
        // Falls back to ProxysDevTenant.Id so every existing test continues to work unchanged.
        string tenantId = Request.Headers[TenantIdHeader].FirstOrDefault()
            ?? ProxysDevTenant.Id.ToString();

        // X-Test-Roles lets authorization tests impersonate non-admin principals.
        string[] roles = Request.Headers[RolesHeader].FirstOrDefault()
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? DefaultRoles;

        List<Claim> claims =
        [
            new Claim("sub",                     sub),
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim(ClaimTypes.Name,           "test-user"),
            new Claim("tenant_id",               tenantId),
        ];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        ClaimsIdentity identity  = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
