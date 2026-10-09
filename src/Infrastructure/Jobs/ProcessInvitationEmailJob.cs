using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Jobs;

/// <summary>
/// Job Hangfire résolu par DI : envoie l'email d'invitation hors bande de la requête HTTP.
/// Exécuté par Hangfire après que le handler a persisté l'invitation et renvoyé sa réponse.
/// </summary>
internal sealed partial class ProcessInvitationEmailJob(
    IEmailSender email,
    IEmailLinkBuilder links,
    ILogger<ProcessInvitationEmailJob> logger)
{
    public async Task RunAsync(Guid invitationId, string rawToken, string inviteeEmail)
    {
        var link = links.AcceptInvitationLink(invitationId, rawToken);
        var body = $"""
            <p>Vous avez été invité à rejoindre une organisation.</p>
            <p><a href="{link}">Accepter l'invitation</a></p>
            <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email.</p>
            """;

        try
        {
            await email.SendAsync(inviteeEmail, "Invitation à rejoindre une organisation", body, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ne jamais faire échouer le job si l'envoi SMTP échoue.
            LogSendFailed(logger, invitationId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Échec de l'envoi de l'e-mail d'invitation pour {InvitationId}")]
    private static partial void LogSendFailed(ILogger logger, Guid invitationId, Exception exception);
}
