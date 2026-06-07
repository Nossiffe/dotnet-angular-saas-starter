namespace Saas.Application.Common.Interfaces;

/// <summary>Envoi d'emails transactionnels (confirmation, reset). Impl SMTP (Mailpit en dev) dans Infrastructure.</summary>
public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken);
}
