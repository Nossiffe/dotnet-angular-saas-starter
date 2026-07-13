using Mediator;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations;
using Saas.Application.Invitations.AcceptInvitation;
using Saas.Application.Invitations.InviteMember;
using Saas.Application.Invitations.ListPendingInvitations;
using Saas.Application.Invitations.ResendInvitation;
using Saas.Application.Invitations.RevokeInvitation;

namespace Saas.Api.Endpoints;

public sealed class InvitationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Routes organisation-scoped : Owner uniquement.
        var orgGroup = app.MapGroup("/organizations/{orgId:guid}/invitations")
            .WithTags("Invitations")
            .RequireAuthorization("TenantAdmin");

        orgGroup.MapPost("/", InviteMember)
            .WithCreatedResponses<InviteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapGet("/", ListPendingInvitations)
            .Produces<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK);

        orgGroup.MapPost("/{id:guid}/resend", ResendInvitation)
            .WithNoContentResponses();

        orgGroup.MapDelete("/{id:guid}", RevokeInvitation)
            .WithNoContentResponses();

        // Route anonyme : accept invitation.
        app.MapPost("/invitations/accept", AcceptInvitation)
            .WithTags("Invitations")
            .AllowAnonymous()
            .WithStandardResponses<AcceptInvitationResponse>();
    }

    private static async Task<IResult> InviteMember(
        Guid orgId,
        InviteMemberRequest req,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (EndpointHelpers.CheckTenantScope(orgId, tenant) is { } scopeError)
        {
            return scopeError;
        }

        var role = EndpointHelpers.ParseRole(req.Role);
        var result = await sender.Send(new InviteMemberCommand(orgId, req.Email, role), ct);
        return result
            .Then(id => new InviteResponse(id))
            .ToCreatedHttpResult(response => $"/api/v1/organizations/{orgId}/invitations/{response.InvitationId}");
    }

    private static async Task<IResult> ListPendingInvitations(
        Guid orgId,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        if (EndpointHelpers.CheckTenantScope(orgId, tenant) is { } scopeError)
        {
            return scopeError;
        }

        var result = await sender.Send(new ListPendingInvitationsQuery(orgId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ResendInvitation(
        Guid orgId,
        Guid id,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        if (EndpointHelpers.CheckTenantScope(orgId, tenant) is { } scopeError)
        {
            return scopeError;
        }

        var result = await sender.Send(new ResendInvitationCommand(orgId, id), ct);
        return result.ToNoContentHttpResult();
    }

    private static async Task<IResult> RevokeInvitation(
        Guid orgId,
        Guid id,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        if (EndpointHelpers.CheckTenantScope(orgId, tenant) is { } scopeError)
        {
            return scopeError;
        }

        var result = await sender.Send(new RevokeInvitationCommand(orgId, id), ct);
        return result.ToNoContentHttpResult();
    }

    private static async Task<IResult> AcceptInvitation(
        AcceptInvitationRequest req,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var result = await sender.Send(new AcceptInvitationCommand(req.InvitationId, req.Token, req.Password), ct);
        return result
            .Then(r => new AcceptInvitationResponse(r.IsNewUser))
            .ToHttpResult();
    }

}
