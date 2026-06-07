using Hangfire.Dashboard;

namespace Saas.Api.Common;

// Le dashboard Hangfire (/hangfire) est servi au NAVIGATEUR (cookie/session), pas via le JWT Bearer
// de l'API. Le filtre décide donc l'accès indépendamment de la fallback policy ASP.NET :
//  - En Development : on autorise le loopback (localhost) pour le confort de dev.
//  - Hors Development : on exige le claim platform_role=PlatformAdmin (claim plateforme dédié).
//
// SECURITE : on N'utilise PAS tenant_role=Owner — tout propriétaire de tenant aurait accès au
// dashboard GLOBAL (jobs de tous les tenants). Le claim platform_role n'est émis par aucun flow
// de login standard ; seul un opérateur infra reçoit platform_role=PlatformAdmin (via seed ou IdP).
// Le dashboard est donc secure-by-default : personne n'y accède en prod tant qu'aucun opérateur
// n'est explicitement provisionné. Voir ARCHITECTURE.md (section Hangfire) pour le détail.
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
            && user.HasClaim("platform_role", "PlatformAdmin");
    }
}
