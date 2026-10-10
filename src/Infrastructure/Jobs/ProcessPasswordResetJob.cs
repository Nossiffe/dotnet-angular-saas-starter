using Hangfire;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Jobs;

/// <summary>
/// Job Hangfire résolu par DI : effectue le vrai travail du reset de mot de passe
/// (lookup, génération de token, envoi d'email) hors bande de la requête HTTP.
/// Exécuté par Hangfire après que le handler a renvoyé 200 instantanément.
///
/// La demande n'écrit rien en base : il n'y a pas de transaction où accrocher l'outbox, d'où
/// Hangfire. Un échec SMTP fait échouer le job, que Hangfire reprend jusqu'à trois fois. Le job passe
/// ensuite à l'état Deleted, qui expire après 24 h comme un job réussi : l'adresse e-mail, argument
/// du job, ne reste pas indéfiniment dans les tables Hangfire, comme elle le ferait en échec.
/// </summary>
internal sealed partial class ProcessPasswordResetJob(
    IIdentityService identity,
    IEmailSender emailSender,
    IEmailLinkBuilder links,
    ILogger<ProcessPasswordResetJob> logger)
{
    internal const int MaxRetries = 3;

    [AutomaticRetry(Attempts = MaxRetries, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task RunAsync(string email)
    {
        // E-mail inconnu : rien à envoyer. La réponse HTTP est déjà partie, la même que pour un
        // e-mail connu : c'est elle qui ne révèle rien.
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
            // Hangfire journalise et conserve l'exception : seule la raison d'une EmailDeliveryException est sûre.
            var reason = ex is EmailDeliveryException delivery ? delivery.Reason : ex.GetType().Name;
            LogSendFailed(logger, reset.UserId, reason);
#pragma warning disable MA0054 // exception d'origine volontairement omise : son message peut citer l'adresse
            throw new InvalidOperationException(
                $"Échec de l'envoi de l'e-mail de réinitialisation pour l'utilisateur {reset.UserId} ({reason}).");
#pragma warning restore MA0054
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Échec de l'envoi de l'e-mail de réinitialisation du mot de passe pour {UserId} ({Reason})")]
    private static partial void LogSendFailed(ILogger logger, Guid userId, string reason);
}
