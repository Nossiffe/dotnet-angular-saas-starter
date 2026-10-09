using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Jobs;

/// <summary>
/// Job Hangfire résolu par DI : effectue le vrai travail du reset de mot de passe
/// (lookup, génération de token, envoi d'email) hors bande de la requête HTTP.
/// Exécuté par Hangfire après que le handler a renvoyé 200 instantanément.
/// </summary>
internal sealed partial class ProcessPasswordResetJob(
    IIdentityService identity,
    IEmailSender emailSender,
    IEmailLinkBuilder links,
    ILogger<ProcessPasswordResetJob> logger)
{
    public async Task RunAsync(string email)
    {
        // Anti-énumération préservée : on no-op silencieusement si l'email est inconnu.
        var generated = await identity.GeneratePasswordResetTokenAsync(email, CancellationToken.None)
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
            await emailSender.SendAsync(email, "Réinitialisation de votre mot de passe", body, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ne jamais faire échouer le job (ni révéler l'existence de l'email) si l'envoi SMTP échoue.
            LogSendFailed(logger, reset.UserId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Échec de l'envoi de l'e-mail de réinitialisation du mot de passe pour {UserId}")]
    private static partial void LogSendFailed(ILogger logger, Guid userId, Exception exception);
}
