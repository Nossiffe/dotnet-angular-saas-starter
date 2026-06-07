using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IPasswordResetEnqueuer enqueuer) : ICommandHandler<ForgotPasswordCommand, Unit>
{
    public ValueTask<Unit> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Anti-énumération : réponse TOUJOURS identique (succès) quel que soit l'email.
        // Le vrai travail (lookup + token + envoi) est délégué à un job Hangfire qui s'exécute
        // hors bande → la réponse HTTP est instantanée et uniforme (ferme le timing oracle).
        enqueuer.Enqueue(command.Email);
        return ValueTask.FromResult(Unit.Value);
    }
}
