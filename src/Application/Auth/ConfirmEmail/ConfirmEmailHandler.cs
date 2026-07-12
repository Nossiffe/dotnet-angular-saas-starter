using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.ConfirmEmail;

public sealed class ConfirmEmailHandler(IIdentityService identity)
    : ICommandHandler<ConfirmEmailCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var confirmed = await identity.ConfirmEmailAsync(command.UserId, command.Token, cancellationToken);
        return confirmed ? Result.Success : AuthErrors.InvalidConfirmationLink;
    }
}
