using Mediator;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Invitations;
using Saas.Application.Invitations.AcceptInvitation;
using Saas.Application.Invitations.InviteMember;
using Saas.Application.Invitations.ListPendingInvitations;
using Saas.Application.Invitations.ResendInvitation;
using Saas.Application.Invitations.RevokeInvitation;
using Saas.Infrastructure.Identity;

namespace Saas.Api.Endpoints;

public sealed class InvitationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Routes d'une organisation : Owner ou Admin, sur sa propre organisation.
        var orgGroup = app.MapGroup("/organizations/{orgId:guid}/invitations")
            .WithTags("Invitations")
            .RequireAuthorization(AuthorizationPolicies.TenantAdmin)
            .WithAuthorizationResponses()
            .AddEndpointFilter<OrganizationScopeFilter>();

        orgGroup.MapPost("/", InviteMember)
            .WithCreatedResponses<InviteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapGet("/", ListPendingInvitations)
            .Produces<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        orgGroup.MapPost("/{id:guid}/resend", ResendInvitation)
            .WithNoContentResponses();

        orgGroup.MapDelete("/{id:guid}", RevokeInvitation)
            .WithNoContentResponses();

        app.MapPost("/invitations/accept", AcceptInvitation)
            .WithTags("Invitations")
            .AllowAnonymous()
            .WithStandardResponses<AcceptInvitationResponse>();
    }

    private static async Task<IResult> InviteMember(
        Guid orgId,
        InviteMemberRequest req,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var result = await EndpointHelpers.ParseRole(req.Role)
            .ThenAsync(role => sender.Send(new InviteMemberCommand(orgId, req.Email, role), ct).AsTask());

        // Pas de route de lecture d'une invitation seule : Location désigne la liste des invitations
        // en attente, qui la contient.
        return result
            .Then(id => new InviteResponse(id))
            .ToCreatedHttpResult(_ => $"/api/v1/organizations/{orgId}/invitations");
    }

    private static async Task<IResult> ListPendingInvitations(
        Guid orgId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListPendingInvitationsQuery(orgId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ResendInvitation(
        Guid orgId,
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new ResendInvitationCommand(orgId, id), ct);
        return result.ToNoContentHttpResult();
    }

    private static async Task<IResult> RevokeInvitation(
        Guid orgId,
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
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
