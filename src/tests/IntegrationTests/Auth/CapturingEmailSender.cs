using System.Collections.Concurrent;
using Saas.Application.Common.Interfaces;

namespace Saas.IntegrationTests.Auth;

/// <summary>Fake IEmailSender qui capture les emails envoyés (dest + corps) pour extraire userId/token en test.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, CapturedEmail> _byRecipient =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CapturedEmail> Sent => _byRecipient.Values.ToArray();

    public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        _byRecipient[recipient] = new CapturedEmail(recipient, subject, htmlBody);
        return Task.CompletedTask;
    }

    public CapturedEmail? For(string recipient)
        => _byRecipient.TryGetValue(recipient, out var email) ? email : null;
}
