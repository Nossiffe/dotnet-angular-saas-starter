using FluentValidation;
using FluentValidation.Results;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Api.Common;

internal static class EndpointHelpers
{
    internal static void EnforceTenantScope(Guid orgId, ITenantProvider tenant)
    {
        if (orgId != tenant.TenantId)
        {
            throw new ValidationException(
                [new ValidationFailure("orgId", "L'organisation demandée ne correspond pas au tenant du token.")]);
        }
    }

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
