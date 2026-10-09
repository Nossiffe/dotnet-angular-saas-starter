using FluentValidation;
using FluentValidation.Results;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Api.Common;

internal static class EndpointHelpers
{
    /// <summary>
    /// Vérifie que l'<c>orgId</c> de la route correspond au tenant du token. Anti-énumération :
    /// un mismatch renvoie 404 (<see cref="OrganizationErrors.NotFound"/>), jamais 400/403 —
    /// un statut distinct révélerait à un appelant authentifié qu'une autre organisation existe
    /// (cf. docs/error-codes.md § anti-énumération).
    /// Retourne <see langword="null"/> si la portée est correcte (l'appelant continue), sinon
    /// le <see cref="IResult"/> 404 à retourner immédiatement.
    /// </summary>
    internal static IResult? CheckTenantScope(Guid orgId, ITenantProvider tenant)
        => orgId == tenant.TenantId ? null : OrganizationErrors.NotFound.ToProblemHttpResult();

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
