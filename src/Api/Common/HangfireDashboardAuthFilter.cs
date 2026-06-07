using Hangfire.Dashboard;

namespace Saas.Api.Common;

// Le dashboard Hangfire (/hangfire) est servi au NAVIGATEUR (cookie/session), pas via le JWT Bearer
// de l'API. Le filtre décide donc l'accès indépendamment de la fallback policy ASP.NET :
//  - En Development : on autorise le loopback (localhost) pour le confort de dev.
//  - Hors Development : on exige un utilisateur authentifié AVEC le rôle Owner (tenant_role).
//
// Durcissement prod recommandé (documenté dans ARCHITECTURE.md) : placer /hangfire derrière un
// reverse-proxy/VPN, ou exiger une auth interactive (cookie) dédiée aux admins plateforme.
public sealed class HangfireDashboardAuthFilter(IWebHostEnvironment environment) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var httpContext = context.GetHttpContext();

        if (environment.IsDevelopment())
        {
            var remoteIp = httpContext.Connection.RemoteIpAddress;
            return remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);
        }

        var user = httpContext.User;
        return user.Identity?.IsAuthenticated == true
            && user.HasClaim("tenant_role", "Owner");
    }
}
