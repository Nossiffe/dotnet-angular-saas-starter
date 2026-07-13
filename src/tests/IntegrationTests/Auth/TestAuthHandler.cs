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

    /// <summary>
    /// Émet un claim <c>platform_role</c> (ex. "PlatformAdmin") si le header est présent.
    /// AUCUN défaut : contrairement à tenant_role, ce claim n'existe dans aucun flow de login
    /// standard — c'est le contrat de prod (voir HangfireDashboardAuthFilter). Un test qui ne
    /// pose pas ce header simule donc un utilisateur authentifié SANS ce claim.
    /// </summary>
    public const string PlatformRoleHeader = "X-Test-PlatformRole";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenant = Request.Headers.TryGetValue(TenantHeader, out var t) && t.Count > 0
            ? t.ToString() : "00000000-0000-0000-0000-000000000001";
        var userId = Request.Headers.TryGetValue(UserHeader, out var u) && u.Count > 0
            ? u.ToString() : Guid.CreateVersion7().ToString();
        var tenantRole = Request.Headers.TryGetValue(TenantRoleHeader, out var r) && r.Count > 0
            ? r.ToString() : "Owner";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, "test@saas.local"),
            new("tenant", tenant),
            new(ClaimTypes.Role, "Admin"),
            new("tenant_role", tenantRole),
        };
        if (Request.Headers.TryGetValue(PlatformRoleHeader, out var p) && p.Count > 0)
        {
            claims.Add(new Claim("platform_role", p.ToString()));
        }

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
