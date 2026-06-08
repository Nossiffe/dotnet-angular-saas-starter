using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.ResendInvitation;

public sealed class ResendInvitationHandler(
    IInvitationRepository invitations,
    IInvitationTokenService tokens,
    IInvitationEmailEnqueuer enqueuer,
    IUnitOfWork uow,
    IDateTimeProvider clock)
    : ICommandHandler<ResendInvitationCommand, Unit>
{
    public async ValueTask<Unit> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || invitation.OrganizationId.Value != command.OrganizationId)
        {
            throw new NotFoundException(nameof(Invitation), command.InvitationId);
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new ValidationException(
                [new ValidationFailure("InvitationId", "Seule une invitation en attente peut être renvoyée.")]);
        }

        var rawToken = tokens.GenerateRawToken();
        var tokenHash = tokens.HashToken(rawToken);

        invitation.Refresh(tokenHash, clock.UtcNow.AddDays(7));
        await uow.SaveChangesAsync(cancellationToken);

        enqueuer.Enqueue(invitation.Id.Value, rawToken, invitation.InviteeEmail);

        return Unit.Value;
    }
}
