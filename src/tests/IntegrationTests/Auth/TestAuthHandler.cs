using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Saas.IntegrationTests.Auth;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string TenantHeader = "X-Test-Tenant";
    public const string UserHeader = "X-Test-User";

    /// <summary>
    /// Override the tenant_role claim. Defaults to "Owner" when not set.
    /// Set to any other value (e.g. "Member") to simulate a non-Owner identity.
    /// </summary>
    public const string TenantRoleHeader = "X-Test-TenantRole";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenant = Request.Headers.TryGetValue(TenantHeader, out var t) && t.Count > 0
            ? t.ToString() : "00000000-0000-0000-0000-000000000001";
        var userId = Request.Headers.TryGetValue(UserHeader, out var u) && u.Count > 0
            ? u.ToString() : Guid.CreateVersion7().ToString();
        var tenantRole = Request.Headers.TryGetValue(TenantRoleHeader, out var r) && r.Count > 0
            ? r.ToString() : "Owner";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, "test@saas.local"),
            new Claim("tenant", tenant),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("tenant_role", tenantRole),
        };
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
