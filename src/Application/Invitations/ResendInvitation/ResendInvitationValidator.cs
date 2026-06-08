using FluentValidation;

namespace Saas.Application.Invitations.ResendInvitation;

public sealed class ResendInvitationValidator : AbstractValidator<ResendInvitationCommand>
{
    public ResendInvitationValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.InvitationId).NotEmpty();
    }
}
