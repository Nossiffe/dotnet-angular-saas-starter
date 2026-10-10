using Microsoft.AspNetCore.Authorization;

namespace Saas.Infrastructure.Identity;

/// <summary>
/// Exigée par les politiques des routes rattachées à une organisation (<see cref="AuthorizationPolicies"/>) :
/// l'utilisateur du token est encore membre de l'organisation du token, avec le rôle que le token
/// annonce. Un access token reste valable jusqu'à son expiration ; sans cette relecture, un membre
/// rétrogradé gardait les droits de son ancien rôle jusque-là.
/// </summary>
public sealed class CurrentMembershipRequirement : IAuthorizationRequirement
{
    public static readonly CurrentMembershipRequirement Instance = new();

    private CurrentMembershipRequirement()
    {
    }
}
