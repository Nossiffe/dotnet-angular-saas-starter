using Saas.Application.Common.Events;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.InvitationAccepted;

/// <summary>
/// Envoie l'e-mail de bienvenue quand un invité rejoint l'organisation. L'acceptation est déjà
/// enregistrée : un échec SMTP fait échouer ce handler seul, et l'outbox le retente plus tard.
/// </summary>
public sealed class InvitationAcceptedHandler(IEmailSender email)
    : IDomainEventHandler<InvitationAcceptedDomainEvent>
{
    public async ValueTask HandleAsync(InvitationAcceptedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var body = $"""
            <p>Bienvenue ! Vous avez rejoint votre organisation en tant que {domainEvent.Role}.</p>
            <p>Vous pouvez désormais vous connecter et accéder à votre espace.</p>
            """;
        await email.SendAsync(domainEvent.InviteeEmail, "Bienvenue dans votre organisation", body, cancellationToken);
    }
}
