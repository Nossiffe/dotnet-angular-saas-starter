using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Jobs;

/// <summary>
/// Job Hangfire résolu par DI : effectue le vrai travail du reset de mot de passe
/// (lookup, génération de token, envoi d'email) hors bande de la requête HTTP.
/// Exécuté par Hangfire après que le handler a renvoyé 200 instantanément.
/// </summary>
internal sealed class ProcessPasswordResetJob(
    IIdentityService identity,
    IEmailSender email,
    IEmailLinkBuilder links,
    ILogger<ProcessPasswordResetJob> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> _logSendFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(1, "ResetEmailSendFailed"),
            "Échec de l'envoi de l'email de reset de mot de passe pour {UserId}");

    public async Task RunAsync(string email_address)
    {
        // Anti-énumération préservée : on no-op silencieusement si l'email est inconnu.
        var generated = await identity.GeneratePasswordResetTokenAsync(email_address, CancellationToken.None)
            .ConfigureAwait(false);

        if (generated is not { } reset)
        {
            return;
        }

        var link = links.ResetPasswordLink(reset.UserId, reset.Token);
        var body = $"""
            <p>Vous avez demandé la réinitialisation de votre mot de passe.</p>
            <p><a href="{link}">Réinitialiser mon mot de passe</a></p>
            <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email.</p>
            """;

        try
        {
            await email.SendAsync(email_address, "Réinitialisation de votre mot de passe", body, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ne jamais faire échouer le job (ni révéler l'existence de l'email) si l'envoi SMTP échoue.
            _logSendFailed(logger, reset.UserId, ex);
        }
    }
}
