using System.Collections.Concurrent;
using Saas.Application.Common.Interfaces;

namespace Saas.IntegrationTests.Auth;

/// <summary>Fake IEmailSender qui capture les emails envoyés (dest + corps) pour extraire userId/token en test.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, CapturedEmail> _byRecipient =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, (int Count, Exception? Error)> _failuresLeft =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CapturedEmail> Sent => _byRecipient.Values.ToArray();

    public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (_failuresLeft.TryGetValue(recipient, out var failure) && failure.Count > 0)
        {
            _failuresLeft[recipient] = (failure.Count - 1, failure.Error);
            // Par défaut, comme l'erreur brute d'un serveur SMTP, le message cite l'adresse du destinataire.
            return Task.FromException(failure.Error ?? new InvalidOperationException(
                $"550 5.1.1 <{recipient}>: Recipient address rejected (test)"));
        }

        _byRecipient[recipient] = new CapturedEmail(recipient, subject, htmlBody);
        return Task.CompletedTask;
    }

    /// <summary>Les <paramref name="count"/> prochains envois vers ce destinataire échouent, avec l'erreur donnée.</summary>
    public void FailNext(string recipient, int count = 1, Exception? error = null) => _failuresLeft[recipient] = (count, error);

    public CapturedEmail? For(string recipient)
        => _byRecipient.TryGetValue(recipient, out var email) ? email : null;
}
