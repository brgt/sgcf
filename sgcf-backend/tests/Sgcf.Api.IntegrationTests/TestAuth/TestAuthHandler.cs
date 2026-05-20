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

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Require at least an Authorization header so unauthenticated requests still fail.
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        string sub = Request.Headers[SubHeader].FirstOrDefault() ?? "dev-user-id";

        Claim[] claims =
        [
            new Claim("sub",                                           sub),
            new Claim(ClaimTypes.NameIdentifier,                       sub),
            new Claim(ClaimTypes.Name,                                 "test-user"),
            new Claim(ClaimTypes.Role,                                 "admin"),
            new Claim(ClaimTypes.Role,                                 "tesouraria"),
        ];

        ClaimsIdentity identity  = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
