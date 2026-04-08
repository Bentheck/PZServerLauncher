using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Security;

public sealed class LoopbackTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    HostBootstrapStateStore stateStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LoopbackToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        if (!HostBootstrapStateStore.IsLoopback(Context.Connection.RemoteIpAddress))
        {
            return AuthenticateResult.Fail("Loopback token authentication is only allowed from loopback addresses.");
        }

        var token = Request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();
        var expected = await stateStore.GetLocalApiTokenAsync(Context.RequestAborted);
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(token),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            return AuthenticateResult.Fail("Invalid local API token.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "local-desktop"),
            new Claim(ClaimTypes.Name, "Local desktop"),
            new Claim(ClaimTypes.Role, "LocalSystem"),
            new Claim("auth_source", "loopback"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
