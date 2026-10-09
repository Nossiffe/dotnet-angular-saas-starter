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
    /// <summary>
    /// Hash du jeton du dernier lien envoyé. Null tant qu'aucun e-mail n'est parti, et après un
    /// renvoi jusqu'au nouvel envoi : aucun lien n'est alors valable.
    /// </summary>
    public string? TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ErrorOr<Invitation> Create(
        OrganizationId orgId,
        string email,
        OrganizationRole role,
        Guid invitedBy,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        // Guard technique : jamais atteint après validation applicative (FluentValidation) — reste
        // une ArgumentException (pas une erreur métier du Registre).
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("L'email de l'invité est requis.", nameof(email));
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
        var acceptable = CheckUsable(now, InvitationErrors.NotPendingAccept);
        if (acceptable.IsError)
        {
            return acceptable.Errors;
        }

        Status = InvitationStatus.Accepted;
        Raise(new InvitationAcceptedDomainEvent(Id, OrganizationId, InviteeEmail, Role));
        return Result.Success;
    }

    /// <summary>
    /// Enregistre le hash du jeton qui part dans l'e-mail : seul le dernier lien envoyé est valable.
    /// Refusé pour une invitation qui n'est plus en attente ou qui a expiré.
    /// </summary>
    public ErrorOr<Success> IssueToken(string tokenHash, DateTimeOffset now)
    {
        // Guard technique : le hash vient toujours du service de jetons.
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Le hash du token est requis.", nameof(tokenHash));
        }

        var usable = CheckUsable(now, InvitationErrors.NotPendingIssueToken);
        if (usable.IsError)
        {
            return usable.Errors;
        }

        TokenHash = tokenHash;
        return Result.Success;
    }

    /// <summary>
    /// Renvoi : nouvelle échéance, et le lien précédent cesse aussitôt d'être valable. Le nouveau
    /// lien part avec InvitationResentDomainEvent.
    /// </summary>
    public ErrorOr<Success> Refresh(DateTimeOffset newExpiresAt)
    {
        if (Status != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPendingRefresh;
        }

        TokenHash = null;
        ExpiresAt = newExpiresAt;
        Raise(new InvitationResentDomainEvent(Id, OrganizationId));
        return Result.Success;
    }

    private ErrorOr<Success> CheckUsable(DateTimeOffset now, Error notPending)
    {
        if (Status != InvitationStatus.Pending)
        {
            return notPending;
        }

        if (ExpiresAt <= now)
        {
            return InvitationErrors.Expired;
        }

        return Result.Success;
    }
}
