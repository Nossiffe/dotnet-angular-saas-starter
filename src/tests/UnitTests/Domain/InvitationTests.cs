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
    public void Create_invitation_valide()
    {
        var invitation = CreatePending();

        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.Role.ShouldBe(OrganizationRole.Member);
        invitation.InviteeEmail.ShouldBe("invitee@example.com");
        invitation.DomainEvents.OfType<MemberInvitedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_avec_role_Owner_retourne_une_erreur()
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
    public void Create_avec_email_vide_lance() =>
        Should.Throw<ArgumentException>(() => Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            " ",
            OrganizationRole.Member,
            Guid.CreateVersion7(),
            "deadbeef",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7)));

    [Fact]
    public void Accept_expire_retourne_une_erreur()
    {
        var invitation = CreatePending(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var result = invitation.Accept(DateTimeOffset.UtcNow);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.Expired);
    }

    [Fact]
    public void Revoke_d_une_invitation_acceptee_retourne_une_erreur()
    {
        var invitation = CreatePending();
        invitation.Accept(DateTimeOffset.UtcNow);

        var result = invitation.Revoke();

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingRevoke);
    }

    [Fact]
    public void Accept_pending_reussit()
    {
        var invitation = CreatePending();

        var result = invitation.Accept(DateTimeOffset.UtcNow);

        result.IsError.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.DomainEvents.OfType<InvitationAcceptedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Accept_deja_acceptee_retourne_une_erreur()
    {
        var invitation = CreatePending();
        invitation.Accept(DateTimeOffset.UtcNow);

        var result = invitation.Accept(DateTimeOffset.UtcNow);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingAccept);
    }

    [Fact]
    public void Revoke_pending_reussit()
    {
        var invitation = CreatePending();

        var result = invitation.Revoke();

        result.IsError.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Revoked);
    }

    [Fact]
    public void Refresh_d_une_invitation_revoquee_retourne_une_erreur()
    {
        var invitation = CreatePending();
        invitation.Revoke();

        var result = invitation.Refresh("newhash", DateTimeOffset.UtcNow.AddDays(7));

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(InvitationErrors.NotPendingRefresh);
    }

    [Fact]
    public void Refresh_pending_reussit()
    {
        var invitation = CreatePending();
        var newExpiry = DateTimeOffset.UtcNow.AddDays(7);

        var result = invitation.Refresh("newhash", newExpiry);

        result.IsError.ShouldBeFalse();
        invitation.TokenHash.ShouldBe("newhash");
        invitation.ExpiresAt.ShouldBe(newExpiry);
    }

    [Fact]
    public void Refresh_avec_hash_vide_lance()
    {
        var invitation = CreatePending();

        Should.Throw<ArgumentException>(() => invitation.Refresh(" ", DateTimeOffset.UtcNow.AddDays(7)));
    }
}
