using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
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

        // Garde anti self-demotion : un admin ne peut pas changer son propre rôle (évite de se verrouiller
        // ou de contourner la garde « dernier Owner » via soi-même). Field-tied (ValidationFailure liée à
        // MemberUserId) → reste un throw ValidationException, comme les échecs Identity d'AcceptInvitation.
        if (currentUser.UserId is not null
            && Guid.TryParse(currentUser.UserId, out var currentUserId)
            && currentUserId == command.MemberUserId)
        {
            throw new ValidationException(
                [new ValidationFailure("MemberUserId", "Vous ne pouvez pas modifier votre propre rôle.")]);
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
