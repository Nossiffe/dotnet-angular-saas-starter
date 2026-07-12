using Mediator;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Common.Interfaces;
using Saas.Application.Members.ChangeMemberRole;

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

    private static async Task<IResult> ChangeMemberRole(
        Guid orgId,
        Guid userId,
        ChangeMemberRoleRequest req,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        EndpointHelpers.EnforceTenantScope(orgId, tenant);
        var role = EndpointHelpers.ParseRole(req.Role);
        var result = await sender.Send(new ChangeMemberRoleCommand(orgId, userId, role), ct);
        return result.ToNoContentHttpResult();
    }

}
