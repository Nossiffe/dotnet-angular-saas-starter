using ErrorOr;
using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed class Invitation : AggregateRoot<InvitationId>
{
    /// <summary>Durée de validité d'une invitation, à l'envoi comme au renvoi.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

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

    public static ErrorOr<Invitation> Create(
        OrganizationId orgId,
        string email,
        OrganizationRole role,
        Guid invitedBy,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        // Guards techniques : jamais atteints après validation applicative (FluentValidation) —
        // restent des ArgumentException (pas des erreurs métier du Registre).
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("L'email de l'invité est requis.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Le hash du token est requis.", nameof(tokenHash));
        }

        if (role == OrganizationRole.Owner)
        {
            return InvitationErrors.OwnerRole;
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
            CreatedAt = createdAt,
        };
        invitation.Raise(new MemberInvitedDomainEvent(invitation.Id, orgId, invitation.InviteeEmail));
        return invitation;
    }

    public ErrorOr<Success> Revoke()
    {
        if (Status != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPendingRevoke;
        }

        Status = InvitationStatus.Revoked;
        return Result.Success;
    }

    public ErrorOr<Success> Accept(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPendingAccept;
        }

        if (ExpiresAt <= now)
        {
            return InvitationErrors.Expired;
        }

        Status = InvitationStatus.Accepted;
        Raise(new InvitationAcceptedDomainEvent(Id, OrganizationId, InviteeEmail, Role));
        return Result.Success;
    }

    public ErrorOr<Success> Refresh(string newTokenHash, DateTimeOffset newExpiresAt)
    {
        // Guard technique : jamais atteint après validation applicative.
        if (string.IsNullOrWhiteSpace(newTokenHash))
        {
            throw new ArgumentException("Le hash du token est requis.", nameof(newTokenHash));
        }

        if (Status != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPendingRefresh;
        }

        TokenHash = newTokenHash;
        ExpiresAt = newExpiresAt;
        return Result.Success;
    }
}
