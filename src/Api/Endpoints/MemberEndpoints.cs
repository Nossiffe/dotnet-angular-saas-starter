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
            .WithAuthorizationResponses()
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
        var result = await EndpointHelpers.ParseRole(req.Role)
            .ThenAsync(role => sender.Send(new ChangeMemberRoleCommand(orgId, userId, role), ct).AsTask());
        return result.ToNoContentHttpResult();
    }
}
