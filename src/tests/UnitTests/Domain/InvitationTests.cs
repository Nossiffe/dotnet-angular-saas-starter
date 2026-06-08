using Saas.Domain.Common;
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
            expiresAt ?? DateTimeOffset.UtcNow.AddDays(7));

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
    public void Create_avec_role_Owner_lance() =>
        Should.Throw<DomainException>(() => CreatePending(OrganizationRole.Owner));

    [Fact]
    public void Accept_expire_lance()
    {
        var invitation = CreatePending(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        Should.Throw<DomainException>(() => invitation.Accept(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Revoke_d_une_invitation_acceptee_lance()
    {
        var invitation = CreatePending();
        invitation.Accept(DateTimeOffset.UtcNow);

        Should.Throw<DomainException>(invitation.Revoke);
    }
}
