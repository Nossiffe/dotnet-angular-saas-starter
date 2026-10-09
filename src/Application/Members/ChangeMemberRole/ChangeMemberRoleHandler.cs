using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Members.ChangeMemberRole;

public sealed class ChangeMemberRoleHandler(
    IOrganizationRepository organizations,
    IUnitOfWork uow,
    ICurrentUser currentUser)
    : ICommandHandler<ChangeMemberRoleCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Personne ne change son propre rôle : on ne se retire pas ses droits par erreur, et la garde
        // du dernier Owner ne se contourne pas par soi-même.
        if (Guid.TryParse(currentUser.UserId, out var currentUserId) && currentUserId == command.MemberUserId)
        {
            return OrganizationErrors.CannotChangeOwnRole;
        }

        var organization = await organizations.GetByIdAsync(OrganizationId.From(command.OrganizationId), cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var changed = organization.ChangeMemberRole(command.MemberUserId, command.NewRole);
        if (changed.IsError)
        {
            return changed.Errors;
        }

        await uow.SaveChangesAsync(cancellationToken);

        return Result.Updated;
    }
}
