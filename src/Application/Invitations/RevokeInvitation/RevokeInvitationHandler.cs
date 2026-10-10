using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.RevokeInvitation;

public sealed class RevokeInvitationHandler(
    IInvitationRepository invitations,
    IUnitOfWork uow)
    : ICommandHandler<RevokeInvitationCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || invitation.OrganizationId.Value != command.OrganizationId)
        {
            return InvitationErrors.NotFound;
        }

        var revoked = invitation.Revoke();
        if (revoked.IsError)
        {
            return revoked.Errors;
        }

        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
