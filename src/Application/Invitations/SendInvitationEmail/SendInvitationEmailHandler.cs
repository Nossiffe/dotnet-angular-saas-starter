using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.SendInvitationEmail;

/// <summary>
/// Envoie l'e-mail d'invitation, à la création comme au renvoi, depuis l'outbox : l'invitation est
/// déjà enregistrée. Le jeton du lien n'existe qu'ici, et seul son hash est écrit en base, avant
/// l'envoi : un lien qui part est donc toujours valable au moment où il part.
///
/// Un échec (SMTP, conflit de concurrence) fait échouer ce handler, que l'outbox reprend plus tard.
/// Le nouvel essai pose un nouveau jeton : un e-mail parti malgré l'échec devient caduc, et seul le
/// dernier lien envoyé est valable.
/// </summary>
public sealed partial class SendInvitationEmailHandler(
    IInvitationRepository invitations,
    IInvitationTokenService tokens,
    IUnitOfWork uow,
    IEmailLinkBuilder links,
    IEmailSender email,
    IDateTimeProvider clock,
    ILogger<SendInvitationEmailHandler> logger)
    : IDomainEventHandler<MemberInvitedDomainEvent>, IDomainEventHandler<InvitationResentDomainEvent>
{
    public ValueTask HandleAsync(MemberInvitedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return SendAsync(domainEvent.InvitationId, cancellationToken);
    }

    public ValueTask HandleAsync(InvitationResentDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return SendAsync(domainEvent.InvitationId, cancellationToken);
    }

    private async ValueTask SendAsync(InvitationId invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByIdAsync(invitationId, cancellationToken);
        if (invitation is null)
        {
            LogNotSent(logger, invitationId.Value);
            return;
        }

        // Révoquée, acceptée ou expirée entre-temps : aucun lien à envoyer.
        var rawToken = tokens.GenerateRawToken();
        var issued = invitation.IssueToken(tokens.HashToken(rawToken), clock.UtcNow);
        if (issued.IsError)
        {
            LogNotSent(logger, invitationId.Value);
            return;
        }

        await uow.SaveChangesAsync(cancellationToken);

        var link = links.AcceptInvitationLink(invitation.Id.Value, rawToken);
        var body = $"""
            <p>Vous avez été invité à rejoindre une organisation.</p>
            <p><a href="{link}">Accepter l'invitation</a></p>
            <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email.</p>
            """;
        await email.SendAsync(invitation.InviteeEmail, "Invitation à rejoindre une organisation", body, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Invitation {InvitationId} : plus en attente, aucun e-mail envoyé")]
    private static partial void LogNotSent(ILogger logger, Guid invitationId);
}
