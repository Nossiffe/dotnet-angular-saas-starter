namespace Saas.Infrastructure.Identity;

/// <summary>
/// Noms des politiques d'autorisation, définies dans <c>DependencyInjection.AddAuthorization</c>. Les
/// trois exigent en plus une adhésion actuelle (<see cref="CurrentMembershipRequirement"/>).
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Claim <c>tenant</c> présent : toute route qui lit ou écrit des données d'un tenant.</summary>
    public const string HasTenant = "HasTenant";

    /// <summary>Owner ou Admin de l'organisation : invitations, liste des membres, suppression de produits.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Owner de l'organisation : changement des rôles des membres.</summary>
    public const string TenantOwner = "TenantOwner";
}
