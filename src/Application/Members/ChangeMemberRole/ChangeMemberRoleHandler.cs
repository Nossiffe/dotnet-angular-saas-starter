using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Tenancy;

namespace Saas.Application.Members.ChangeMemberRole;

public sealed class ChangeMemberRoleHandler(
    IOrganizationRepository organizations,
    IUnitOfWork uow,
    ICurrentUser currentUser)
    : ICommandHandler<ChangeMemberRoleCommand, Unit>
{
    public async ValueTask<Unit> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Garde anti self-demotion : un admin ne peut pas changer son propre rôle (évite de se verrouiller
        // ou de contourner la garde « dernier Owner » via soi-même).
        if (currentUser.UserId is not null
            && Guid.TryParse(currentUser.UserId, out var currentUserId)
            && currentUserId == command.MemberUserId)
        {
            throw new ValidationException(
                [new ValidationFailure("MemberUserId", "Vous ne pouvez pas modifier votre propre rôle.")]);
        }

        var organization = await organizations.GetByIdAsync(OrganizationId.From(command.OrganizationId), cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), command.OrganizationId);

        organization.ChangeMemberRole(command.MemberUserId, command.NewRole);
        await uow.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
