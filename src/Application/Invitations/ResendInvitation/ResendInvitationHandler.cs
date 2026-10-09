using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.ResendInvitation;

public sealed class ResendInvitationHandler(
    IInvitationRepository invitations,
    IInvitationTokenService tokens,
    IInvitationEmailEnqueuer enqueuer,
    IUnitOfWork uow,
    IDateTimeProvider clock)
    : ICommandHandler<ResendInvitationCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || invitation.OrganizationId.Value != command.OrganizationId)
        {
            return InvitationErrors.NotFound;
        }

        var rawToken = tokens.GenerateRawToken();
        var tokenHash = tokens.HashToken(rawToken);

        var refreshed = invitation.Refresh(tokenHash, clock.UtcNow.Add(Invitation.Lifetime));
        if (refreshed.IsError)
        {
            return refreshed.Errors;
        }

        await uow.SaveChangesAsync(cancellationToken);

        enqueuer.Enqueue(invitation.Id.Value, rawToken, invitation.InviteeEmail);

        return Result.Success;
    }
}
