using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Api.Common;

/// <summary>
/// Pour les routes <c>/organizations/{orgId}/...</c> : l'<c>orgId</c> de la route doit être le
/// tenant du token. Sinon 404, jamais 403 : un autre statut révélerait à un appelant authentifié
/// qu'une autre organisation existe (anti-énumération, <c>docs/error-codes.md</c>).
/// </summary>
internal sealed class OrganizationScopeFilter(ITenantProvider tenant) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var routeValue = context.HttpContext.GetRouteValue("orgId")?.ToString();
        if (!Guid.TryParse(routeValue, out var orgId) || orgId != tenant.TenantId)
        {
            return OrganizationErrors.NotFound.ToProblemHttpResult();
        }

        return await next(context);
    }
}
