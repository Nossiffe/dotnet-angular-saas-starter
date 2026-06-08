using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.InvitationAccepted;

/// <summary>
/// Envoie un email de bienvenue (best-effort) quand un invité accepte et rejoint l'organisation.
/// Ne doit jamais faire échouer l'acceptation si SMTP est indisponible.
/// </summary>
public sealed partial class InvitationAcceptedHandler(
    IEmailSender email,
    ILogger<InvitationAcceptedHandler> logger)
    : INotificationHandler<InvitationAcceptedDomainEvent>
{
    public async ValueTask Handle(InvitationAcceptedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var body = $"""
                <p>Bienvenue ! Vous avez rejoint votre organisation en tant que {notification.Role}.</p>
                <p>Vous pouvez désormais vous connecter et accéder à votre espace.</p>
                """;
            await email.SendAsync(notification.InviteeEmail, "Bienvenue dans votre organisation", body, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWelcomeEmailFailed(logger, notification.InvitationId.Value, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Échec de l'envoi de l'email de bienvenue pour l'invitation {InvitationId}")]
    private static partial void LogWelcomeEmailFailed(ILogger logger, Guid invitationId, Exception exception);
}
