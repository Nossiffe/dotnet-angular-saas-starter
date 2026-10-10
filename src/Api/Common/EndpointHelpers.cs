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

    // Chemin d'une route nommée, avec la version d'API de la requête en cours.
    internal static string PathTo(this HttpContext http, string routeName, object values)
        => http.RequestServices.GetRequiredService<LinkGenerator>().GetPathByRouteValues(http, routeName, values)
            ?? throw new InvalidOperationException($"Aucune route nommée {routeName} ne correspond à ces valeurs.");
}
