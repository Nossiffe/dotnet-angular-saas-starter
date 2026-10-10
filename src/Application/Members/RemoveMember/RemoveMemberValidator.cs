using FluentValidation;

namespace Saas.Application.Members.RemoveMember;

public sealed class RemoveMemberValidator : AbstractValidator<RemoveMemberCommand>
{
    public RemoveMemberValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MemberUserId).NotEmpty();
    }
}
