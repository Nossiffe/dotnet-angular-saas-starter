using Mediator;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Members;
using Saas.Application.Members.ChangeMemberRole;
using Saas.Application.Members.ListMembers;
using Saas.Application.Members.RemoveMember;
using Saas.Infrastructure.Identity;

namespace Saas.Api.Endpoints;

public sealed class MemberEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Lecture : Owner ou Admin. Changement de rôle et retrait : Owner seulement, les deux
        // policies s'appliquant à ces routes.
        var group = app.MapGroup("/organizations/{orgId:guid}/members")
            .WithTags("Members")
            .RequireAuthorization(AuthorizationPolicies.TenantAdmin)
            .WithAuthorizationResponses()
            .AddEndpointFilter<OrganizationScopeFilter>();

        group.MapGet("/", ListMembers)
            .Produces<IReadOnlyList<MemberDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{userId:guid}/role", ChangeMemberRole)
            .RequireAuthorization(AuthorizationPolicies.TenantOwner)
            .WithNoContentResponses();

        group.MapDelete("/{userId:guid}", RemoveMember)
            .RequireAuthorization(AuthorizationPolicies.TenantOwner)
            .WithNoContentResponses();
    }

    private static async Task<IResult> ListMembers(
        Guid orgId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListMembersQuery(orgId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ChangeMemberRole(
        Guid orgId,
        Guid userId,
        ChangeMemberRoleRequest req,
        ISender sender,
        CancellationToken ct)
    {
        var result = await EndpointHelpers.ParseRole(req.Role)
            .ThenAsync(role => sender.Send(new ChangeMemberRoleCommand(orgId, userId, role), ct).AsTask());
        return result.ToNoContentHttpResult();
    }

    private static async Task<IResult> RemoveMember(
        Guid orgId,
        Guid userId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new RemoveMemberCommand(orgId, userId), ct);
        return result.ToNoContentHttpResult();
    }
}
