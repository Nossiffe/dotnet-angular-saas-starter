using Mediator;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Members.ChangeMemberRole;
using Saas.Infrastructure.Identity;

namespace Saas.Api.Endpoints;

public sealed class MemberEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/organizations/{orgId:guid}/members")
            .WithTags("Members")
            .RequireAuthorization(AuthorizationPolicies.TenantOwner)
            .AddEndpointFilter<OrganizationScopeFilter>();

        group.MapPut("/{userId:guid}/role", ChangeMemberRole)
            .WithNoContentResponses();
    }

    private static async Task<IResult> ChangeMemberRole(
        Guid orgId,
        Guid userId,
        ChangeMemberRoleRequest req,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var role = EndpointHelpers.ParseRole(req.Role);
        var result = await sender.Send(new ChangeMemberRoleCommand(orgId, userId, role), ct);
        return result.ToNoContentHttpResult();
    }
}
