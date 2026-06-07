using FluentValidation;

namespace Saas.Application.Auth.ConfirmEmail;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
    }
}
