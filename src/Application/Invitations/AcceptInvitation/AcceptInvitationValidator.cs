using FluentValidation;

namespace Saas.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.InvitationId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password)
            .MinimumLength(12)
            .When(x => x.Password is not null);
    }
}
