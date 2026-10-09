using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class InvitationTests
{
    private static Invitation CreatePending(
        OrganizationRole role = OrganizationRole.Member,
        DateTimeOffset? expiresAt = null) =>
        Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            "Invitee@Example.com",
            role,
            Guid.CreateVersion7(),
            "deadbeef",
            DateTimeOffset.UtcNow,
            expiresAt ?? DateTimeOffset.UtcNow.AddDays(7)).Value;

    [Fact]
    public void Create_returns_a_pending_invitation()
    {
        var invitation = CreatePending();

        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.Role.ShouldBe(OrganizationRole.Member);
        invitation.InviteeEmail.ShouldBe("invitee@example.com");
        invitation.DomainEvents.OfType<MemberInvitedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_with_the_Owner_role_returns_an_error()
    {
        var result = Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            "invitee@example.com",
            OrganizationRole.Owner,
            Guid.CreateVersion7(),
            "deadbeef",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7));

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
            "deadbeef",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7)));

    [Fact]
    public void Accepting_an_expired_invitation_returns_an_error()
    {
        var invitation = CreatePending(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var result = invitation.Accept(DateTimeOffset.UtcNow);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.Expired);
    }

    [Fact]
    public void Revoking_an_accepted_invitation_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Accept(DateTimeOffset.UtcNow);

        var result = invitation.Revoke();

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingRevoke);
    }

    [Fact]
    public void Accepting_a_pending_invitation_succeeds()
    {
        var invitation = CreatePending();

        var result = invitation.Accept(DateTimeOffset.UtcNow);

        result.IsError.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.DomainEvents.OfType<InvitationAcceptedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Accepting_twice_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Accept(DateTimeOffset.UtcNow);

        var result = invitation.Accept(DateTimeOffset.UtcNow);

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

    [Fact]
    public void Refreshing_a_revoked_invitation_returns_an_error()
    {
        var invitation = CreatePending();
        invitation.Revoke();

        var result = invitation.Refresh("newhash", DateTimeOffset.UtcNow.AddDays(7));

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingRefresh);
    }

    [Fact]
    public void Refreshing_a_pending_invitation_succeeds()
    {
        var invitation = CreatePending();
        var newExpiry = DateTimeOffset.UtcNow.AddDays(7);

        var result = invitation.Refresh("newhash", newExpiry);

        result.IsError.ShouldBeFalse();
        invitation.TokenHash.ShouldBe("newhash");
        invitation.ExpiresAt.ShouldBe(newExpiry);
    }

    [Fact]
    public void Refresh_with_an_empty_hash_throws()
    {
        var invitation = CreatePending();

        Should.Throw<ArgumentException>(() => invitation.Refresh(" ", DateTimeOffset.UtcNow.AddDays(7)));
    }
}
