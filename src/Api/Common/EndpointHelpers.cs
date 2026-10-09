using FluentValidation;
using FluentValidation.Results;
using Saas.Domain.Tenancy;

namespace Saas.Api.Common;

internal static class EndpointHelpers
{
    internal static OrganizationRole ParseRole(string role)
    {
        if (!Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsed))
        {
            throw new ValidationException(
                [new ValidationFailure("Role", $"Rôle invalide : '{role}'. Valeurs acceptées : Member, Admin.")]);
        }

        return parsed;
    }
}
