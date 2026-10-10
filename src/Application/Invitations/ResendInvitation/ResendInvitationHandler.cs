using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.ResendInvitation;

public sealed class ResendInvitationHandler(
    IInvitationRepository invitations,
    IUnitOfWork uow,
    IDateTimeProvider clock)
    : ICommandHandler<ResendInvitationCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || invitation.OrganizationId.Value != command.OrganizationId)
        {
            return InvitationErrors.NotFound;
        }

        // Le lien précédent cesse d'être valable ici ; le nouveau part par l'outbox
        // (InvitationResentDomainEvent, SendInvitationEmailHandler).
        var refreshed = invitation.Refresh(clock.UtcNow.Add(Invitation.Lifetime));
        if (refreshed.IsError)
        {
            return refreshed.Errors;
        }

        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
