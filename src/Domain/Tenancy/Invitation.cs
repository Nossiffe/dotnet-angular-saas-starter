using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed class Invitation : AggregateRoot<InvitationId>
{
    private Invitation()
    {
        // EF
    }

    public OrganizationId OrganizationId { get; private set; }
    public string InviteeEmail { get; private set; } = null!;
    public OrganizationRole Role { get; private set; }
    public InvitationStatus Status { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Invitation Create(
        OrganizationId orgId,
        string email,
        OrganizationRole role,
        Guid invitedBy,
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("L'email de l'invité est requis.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Le hash du token est requis.");
        }

        if (role == OrganizationRole.Owner)
        {
            throw new DomainException("Impossible d'inviter un membre avec le rôle Owner.");
        }

        var invitation = new Invitation
        {
            Id = InvitationId.From(Guid.CreateVersion7()),
            OrganizationId = orgId,
            InviteeEmail = email.Trim().ToLowerInvariant(),
            Role = role,
            Status = InvitationStatus.Pending,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        invitation.Raise(new MemberInvitedDomainEvent(invitation.Id, orgId, invitation.InviteeEmail));
        return invitation;
    }

    public void Revoke()
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new DomainException("Seule une invitation en attente peut être révoquée.");
        }

        Status = InvitationStatus.Revoked;
    }

    public void Accept(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new DomainException("Seule une invitation en attente peut être acceptée.");
        }

        if (ExpiresAt <= now)
        {
            throw new DomainException("Cette invitation a expiré.");
        }

        Status = InvitationStatus.Accepted;
    }

    public void Refresh(string newTokenHash, DateTimeOffset newExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(newTokenHash))
        {
            throw new DomainException("Le hash du token est requis.");
        }

        if (Status != InvitationStatus.Pending)
        {
            throw new DomainException("Seule une invitation en attente peut être renouvelée.");
        }

        TokenHash = newTokenHash;
        ExpiresAt = newExpiresAt;
    }
}
