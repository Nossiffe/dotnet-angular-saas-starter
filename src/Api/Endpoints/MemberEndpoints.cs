using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Common.Interfaces;
using Saas.Application.Members.ChangeMemberRole;
using Saas.Domain.Tenancy;

namespace Saas.Api.Endpoints;

public sealed class MemberEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/organizations/{orgId:guid}/members")
            .WithTags("Members")
            .RequireAuthorization("TenantAdmin");

        group.MapPut("/{userId:guid}/role", ChangeMemberRole)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<NoContent> ChangeMemberRole(
        Guid orgId,
        Guid userId,
        ChangeMemberRoleRequest req,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        EnforceTenantScope(orgId, tenant);
        var role = ParseRole(req.Role);
        await sender.Send(new ChangeMemberRoleCommand(orgId, userId, role), ct);
        return TypedResults.NoContent();
    }

    private static void EnforceTenantScope(Guid orgId, ITenantProvider tenant)
    {
        if (orgId != tenant.TenantId)
        {
            throw new ValidationException(
                [new ValidationFailure("orgId", "L'organisation demandée ne correspond pas au tenant du token.")]);
        }
    }

    private static OrganizationRole ParseRole(string role)
    {
        if (!Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsed))
        {
            throw new ValidationException(
                [new ValidationFailure("Role", $"Rôle invalide : '{role}'. Valeurs acceptées : Member, Admin.")]);
        }

        return parsed;
    }
}
