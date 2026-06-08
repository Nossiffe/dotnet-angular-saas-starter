using FluentValidation;

namespace Saas.Application.Invitations.RevokeInvitation;

public sealed class RevokeInvitationValidator : AbstractValidator<RevokeInvitationCommand>
{
    public RevokeInvitationValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.InvitationId).NotEmpty();
    }
}
