using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

/// <summary>
/// Relit l'adhésion en base (index unique UserId + OrganizationId), une fois par requête même si
/// plusieurs politiques de la route portent l'exigence. Adhésion absente ou rôle différent du
/// token : échec avec la raison <see cref="StaleToken"/>, que l'API transforme en 401 pour que le
/// client rafraîchisse sa session (le refresh relit le rôle en base). Un token sans utilisateur ou
/// sans organisation lisible (absente, invalide ou vide) échoue sans cette raison : 403.
/// </summary>
public sealed class CurrentMembershipHandler(IDbConnectionFactory connections) : IAuthorizationHandler
{
    public const string StaleToken = "L'adhésion ou le rôle ne correspond plus au token.";

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements.OfType<CurrentMembershipRequirement>().ToList();
        if (pending.Count == 0)
        {
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            || !Guid.TryParse(context.User.FindFirst("tenant")?.Value, out var tenantId)
            || tenantId == Guid.Empty)
        {
            context.Fail();
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var currentRole = await FindRoleAsync(userId, tenantId, cancellationToken);
        if (currentRole is null
            || !string.Equals(currentRole, context.User.FindFirst("tenant_role")?.Value, StringComparison.Ordinal))
        {
            context.Fail(new AuthorizationFailureReason(this, StaleToken));
            return;
        }

        foreach (var requirement in pending)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<string?> FindRoleAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT "Role"
            FROM "OrganizationMemberships"
            WHERE "UserId" = @UserId AND "OrganizationId" = @TenantId
            """,
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
