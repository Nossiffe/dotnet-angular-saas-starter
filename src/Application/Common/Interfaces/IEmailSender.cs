namespace Saas.Application.Common.Interfaces;

/// <summary>
/// Envoi d'emails transactionnels (confirmation, reset). Impl SMTP (Mailpit en dev) dans Infrastructure,
/// qui signale un échec par une <see cref="EmailDeliveryException"/>.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken);
}
