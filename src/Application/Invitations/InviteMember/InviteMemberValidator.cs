using FluentValidation;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.InviteMember;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role)
            .Must(role => role is OrganizationRole.Member or OrganizationRole.Admin)
            .WithMessage("Le rôle doit être Member ou Admin.");
    }
}
