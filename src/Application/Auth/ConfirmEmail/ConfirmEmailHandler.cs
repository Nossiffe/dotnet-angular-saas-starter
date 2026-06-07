using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.ConfirmEmail;

public sealed class ConfirmEmailHandler(IIdentityService identity) : ICommandHandler<ConfirmEmailCommand, bool>
{
    public async ValueTask<bool> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await identity.ConfirmEmailAsync(command.UserId, command.Token, cancellationToken);
    }
}
