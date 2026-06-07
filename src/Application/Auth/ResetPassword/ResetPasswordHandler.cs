using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.ResetPassword;

public sealed class ResetPasswordHandler(IIdentityService identity) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async ValueTask<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ok = await identity.ResetPasswordAsync(
            command.UserId, command.Token, command.NewPassword, cancellationToken);
        if (!ok)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(command.Token), "Le lien de réinitialisation est invalide ou expiré.")]);
        }

        return Unit.Value;
    }
}
