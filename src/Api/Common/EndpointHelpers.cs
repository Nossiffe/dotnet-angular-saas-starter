using ErrorOr;
using Saas.Domain.Tenancy;

namespace Saas.Api.Common;

internal static class EndpointHelpers
{
    // Le contrat HTTP reçoit le rôle en texte ; les commandes le reçoivent typé.
    internal static ErrorOr<OrganizationRole> ParseRole(string role)
        => Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : OrganizationErrors.InvalidRole;
}
