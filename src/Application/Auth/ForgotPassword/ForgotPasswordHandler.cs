using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IIdentityService identity,
    IEmailSender email,
    IEmailLinkBuilder links,
    ILogger<ForgotPasswordHandler> logger) : ICommandHandler<ForgotPasswordCommand, Unit>
{
    private static readonly Action<ILogger, Guid, Exception?> _logSendFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(1, "ResetEmailSendFailed"),
            "Échec de l'envoi de l'email de reset de mot de passe pour {UserId}");

    public async ValueTask<Unit> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Anti-énumération : la réponse est TOUJOURS identique (succès) que l'email existe ou non.
        // On n'envoie l'email que si l'utilisateur existe ; aucune info n'est renvoyée à l'appelant.
        var generated = await identity.GeneratePasswordResetTokenAsync(command.Email, cancellationToken);
        if (generated is not { } reset)
        {
            return Unit.Value;
        }

        var link = links.ResetPasswordLink(reset.UserId, reset.Token);
        var body = $"""
            <p>Vous avez demandé la réinitialisation de votre mot de passe.</p>
            <p><a href="{link}">Réinitialiser mon mot de passe</a></p>
            <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email.</p>
            """;

        try
        {
            await email.SendAsync(command.Email, "Réinitialisation de votre mot de passe", body, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ne jamais faire échouer le flux (ni révéler l'existence de l'email) si l'envoi SMTP échoue.
            _logSendFailed(logger, reset.UserId, ex);
        }

        return Unit.Value;
    }
}
