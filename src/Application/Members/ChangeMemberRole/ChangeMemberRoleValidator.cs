using FluentValidation;
using Saas.Domain.Tenancy;

namespace Saas.Application.Members.ChangeMemberRole;

public sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MemberUserId).NotEmpty();
        RuleFor(x => x.NewRole)
            .Must(role => role is OrganizationRole.Member or OrganizationRole.Admin)
            .WithMessage("Le rôle doit être Member ou Admin.");
    }
}
