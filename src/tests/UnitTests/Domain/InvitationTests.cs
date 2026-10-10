using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Invitation CreatePending(
        OrganizationRole role = OrganizationRole.Member,
        DateTimeOffset? expiresAt = null) =>
        Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            "Invitee@Example.com",
            role,
            Guid.CreateVersion7(),
            _now,
            expiresAt ?? _now.AddDays(7)).Value;

    [Fact]
    public void Create_returns_a_pending_invitation_without_a_token()
    {
        var invitation = CreatePending();

        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.Role.ShouldBe(OrganizationRole.Member);
        invitation.InviteeEmail.ShouldBe("invitee@example.com");
        invitation.TokenHash.ShouldBeNull();
        var invited = invitation.DomainEvents.OfType<MemberInvitedDomainEvent>().ShouldHaveSingleItem();
        invited.InvitationId.ShouldBe(invitation.Id);
    }

    [Fact]
    public void Create_with_the_Owner_role_returns_an_error()
    {
        var result = Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            "invitee@example.com",
            OrganizationRole.Owner,
            Guid.CreateVersion7(),
            _now,
            _now.AddDays(7));

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.OwnerRole);
    }

    [Fact]
    public void Create_with_an_empty_email_throws() =>
        Should.Throw<ArgumentException>(() => Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            " ",
            OrganizationRole.Member,
            Guid.CreateVersion7(),
            _now,
            _now.AddDays(7)));

    [Fact]
    public void Issuing_a_token_records_its_hash()
    {
        var invitation = CreatePending();

        var result = invitation.IssueToken("hash-1", _now);

        result.IsError.ShouldBeFalse();
        invitation.TokenHash.ShouldBe("hash-1");
    }

    // Un nouvel envoi remplace le lien précédent.
    [Fact]
    public void Issuing_a_second_token_replaces_the_first()
    {
        var invitation = CreatePending();
        invitation.IssueToken("hash-1", _now);

        invitation.IssueToken("hash-2", _now);

        invitation.TokenHash.ShouldBe("hash-2");
    }

    [Fact]
    public void Issuing_a_token_just_before_the_expiry_succeeds()
    {
        var invitation = CreatePending(expiresAt: _now.AddTicks(1));

        invitation.IssueToken("hash-1", _now).IsError.ShouldBeFalse();
    }

    [Fact]
    public void Issuing_a_token_at_the_expiry_returns_an_error_and_keeps_no_token()
    {
        var invitation = CreatePending(expiresAt: _now);

        var result = invitation.IssueToken("hash-1", _now);

        result.FirstError.ShouldBe(InvitationErrors.Expired);
        invitation.TokenHash.ShouldBeNull();
    }

    [Fact]
    public void Issuing_a_token_on_a_revoked_invitation_returns_an_error_and_keeps_the_previous_token()
    {
        var invitation = CreatePending();
        invitation.IssueToken("hash-1", _now);
        invitation.Revoke();

        var result = invitation.IssueToken("hash-2", _now);

        result.FirstError.ShouldBe(InvitationErrors.NotPendingIssueToken);
        invitation.TokenHash.ShouldBe("hash-1");
    }

    [Fact]
    public void Issuing_a_token_on_an_accepted_invitation_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Accept(_now);

        invitation.IssueToken("hash-1", _now).FirstError.ShouldBe(InvitationErrors.NotPendingIssueToken);
    }

    [Fact]
    public void Issuing_an_empty_token_hash_throws()
    {
        var invitation = CreatePending();

        Should.Throw<ArgumentException>(() => invitation.IssueToken(" ", _now));
    }

    [Fact]
    public void Refreshing_a_pending_invitation_drops_the_token_and_raises_the_resent_event()
    {
        var invitation = CreatePending();
        invitation.IssueToken("hash-1", _now);
        var newExpiry = _now.AddDays(10);

        var result = invitation.Refresh(newExpiry);

        result.IsError.ShouldBeFalse();
        invitation.TokenHash.ShouldBeNull();
        invitation.ExpiresAt.ShouldBe(newExpiry);
        var resent = invitation.DomainEvents.OfType<InvitationResentDomainEvent>().ShouldHaveSingleItem();
        resent.InvitationId.ShouldBe(invitation.Id);
        resent.OrganizationId.ShouldBe(invitation.OrganizationId);
    }

    // Un renvoi relance une invitation expirée : elle redevient utilisable jusqu'à la nouvelle échéance.
    [Fact]
    public void Refreshing_an_expired_invitation_makes_it_usable_again()
    {
        var invitation = CreatePending(expiresAt: _now.AddDays(-1));

        invitation.Refresh(_now.AddDays(7)).IsError.ShouldBeFalse();

        invitation.IssueToken("hash-1", _now).IsError.ShouldBeFalse();
    }

    [Fact]
    public void Refreshing_a_revoked_invitation_returns_an_error_and_changes_nothing()
    {
        var invitation = CreatePending();
        invitation.IssueToken("hash-1", _now);
        invitation.Revoke();
        var expiresAt = invitation.ExpiresAt;

        var result = invitation.Refresh(_now.AddDays(10));

        result.FirstError.ShouldBe(InvitationErrors.NotPendingRefresh);
        invitation.TokenHash.ShouldBe("hash-1");
        invitation.ExpiresAt.ShouldBe(expiresAt);
        invitation.DomainEvents.OfType<InvitationResentDomainEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Checking_a_pending_invitation_succeeds_without_accepting_it()
    {
        var invitation = CreatePending();

        invitation.CheckAcceptable(_now).IsError.ShouldBeFalse();

        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.DomainEvents.OfType<InvitationAcceptedDomainEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Checking_an_invitation_at_its_expiry_returns_an_error()
    {
        var invitation = CreatePending(expiresAt: _now);

        invitation.CheckAcceptable(_now).FirstError.ShouldBe(InvitationErrors.Expired);
    }

    [Fact]
    public void Checking_a_revoked_invitation_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Revoke();

        invitation.CheckAcceptable(_now).FirstError.ShouldBe(InvitationErrors.NotPendingAccept);
    }

    [Fact]
    public void Accepting_an_expired_invitation_returns_an_error()
    {
        var invitation = CreatePending(expiresAt: _now.AddDays(-1));

        var result = invitation.Accept(_now);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.Expired);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void Accepting_just_before_the_expiry_succeeds()
    {
        var invitation = CreatePending(expiresAt: _now.AddTicks(1));

        invitation.Accept(_now).IsError.ShouldBeFalse();
    }

    [Fact]
    public void Revoking_an_accepted_invitation_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Accept(_now);

        var result = invitation.Revoke();

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingRevoke);
    }

    [Fact]
    public void Accepting_a_pending_invitation_succeeds()
    {
        var invitation = CreatePending();

        var result = invitation.Accept(_now);

        result.IsError.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.DomainEvents.OfType<InvitationAcceptedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Accepting_twice_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Accept(_now);

        var result = invitation.Accept(_now);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingAccept);
    }

    [Fact]
    public void Revoking_a_pending_invitation_succeeds()
    {
        var invitation = CreatePending();

        var result = invitation.Revoke();

        result.IsError.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Revoked);
    }
}
