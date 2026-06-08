using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations;
using Saas.Application.Invitations.AcceptInvitation;
using Saas.Application.Invitations.InviteMember;
using Saas.Application.Invitations.ListPendingInvitations;
using Saas.Application.Invitations.ResendInvitation;
using Saas.Application.Invitations.RevokeInvitation;
using Saas.Domain.Tenancy;

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
            .Produces<InviteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        orgGroup.MapGet("/", ListPendingInvitations)
            .Produces<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK);

        orgGroup.MapPost("/{id:guid}/resend", ResendInvitation)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        orgGroup.MapDelete("/{id:guid}", RevokeInvitation)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // Route anonyme : accept invitation.
        app.MapPost("/invitations/accept", AcceptInvitation)
            .WithTags("Invitations")
            .AllowAnonymous()
            .Produces<AcceptInvitationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<Created<InviteResponse>> InviteMember(
        Guid orgId,
        InviteMemberRequest req,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        EnforceTenantScope(orgId, tenant);
        var role = ParseRole(req.Role);
        var id = await sender.Send(new InviteMemberCommand(orgId, req.Email, role), ct);
        return TypedResults.Created($"/api/v1/organizations/{orgId}/invitations/{id}", new InviteResponse(id));
    }

    private static async Task<Ok<IReadOnlyList<InvitationDto>>> ListPendingInvitations(
        Guid orgId,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        EnforceTenantScope(orgId, tenant);
        var result = await sender.Send(new ListPendingInvitationsQuery(orgId), ct);
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> ResendInvitation(
        Guid orgId,
        Guid id,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        EnforceTenantScope(orgId, tenant);
        await sender.Send(new ResendInvitationCommand(orgId, id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RevokeInvitation(
        Guid orgId,
        Guid id,
        ISender sender,
        ITenantProvider tenant,
        CancellationToken ct)
    {
        EnforceTenantScope(orgId, tenant);
        await sender.Send(new RevokeInvitationCommand(orgId, id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<AcceptInvitationResponse>> AcceptInvitation(
        AcceptInvitationRequest req,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var result = await sender.Send(new AcceptInvitationCommand(req.InvitationId, req.Token, req.Password), ct);
        return TypedResults.Ok(new AcceptInvitationResponse(result.IsNewUser));
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
