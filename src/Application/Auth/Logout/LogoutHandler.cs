using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.Logout;

public sealed class LogoutHandler(IRefreshTokenStore refresh) : ICommandHandler<LogoutCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Idempotent : révoque le refresh token serveur (aucune erreur si déjà révoqué/inconnu).
        await refresh.RevokeAsync(command.RefreshToken, cancellationToken);
        return Result.Success;
    }
}
