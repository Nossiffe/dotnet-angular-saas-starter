using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Members.RemoveMember;

// Dès l'enregistrement, le membre retiré perd l'accès à l'organisation : ses access tokens
// reçoivent 401 (adhésion relue à chaque requête, CurrentMembershipHandler) et son refresh est
// refusé (RefreshTokenHandler). Ses refresh tokens sont ensuite révoqués par l'outbox
// (MemberRemovedHandler). ARCHITECTURE.md, Sessions.
public sealed class RemoveMemberHandler(
    IOrganizationRepository organizations,
    IUnitOfWork uow)
    : ICommandHandler<RemoveMemberCommand, ErrorOr<Deleted>>
{
    public async ValueTask<ErrorOr<Deleted>> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetByIdAsync(OrganizationId.From(command.OrganizationId), cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var removed = organization.RemoveMember(command.MemberUserId);
        if (removed.IsError)
        {
            return removed.Errors;
        }

        await uow.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
