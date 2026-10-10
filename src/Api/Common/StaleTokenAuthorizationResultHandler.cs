using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Saas.Application.Auth;
using Saas.Infrastructure.Identity;

namespace Saas.Api.Common;

/// <summary>
/// Un refus dû à un token périmé (adhésion supprimée, ou rôle changé depuis l'émission du token,
/// <see cref="CurrentMembershipHandler"/>) répond 401 <c>Auth.SessionExpired</c>, et non 403 : le
/// client rafraîchit sa session, ce qui relit le rôle en base, puis rejoue la requête. Un membre
/// à jour dont le rôle ne suffit pas reçoit toujours 403. Les autres résultats suivent le
/// comportement par défaut.
/// </summary>
internal sealed class StaleTokenAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(r => r.Handler is CurrentMembershipHandler) == true)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
            await AuthErrors.SessionExpired.ToProblemHttpResult().ExecuteAsync(context);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
