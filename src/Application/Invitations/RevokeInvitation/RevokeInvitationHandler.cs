using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.RevokeInvitation;

public sealed class RevokeInvitationHandler(
    IInvitationRepository invitations,
    IUnitOfWork uow)
    : ICommandHandler<RevokeInvitationCommand, Unit>
{
    public async ValueTask<Unit> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || invitation.OrganizationId.Value != command.OrganizationId)
        {
            throw new NotFoundException(nameof(Invitation), command.InvitationId);
        }

        invitation.Revoke();
        await uow.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
