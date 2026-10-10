using Saas.Domain.Tenancy;
using Shouldly;

namespace Saas.UnitTests.Application;

/// <summary>Organisations et invitations des tests de handlers, à une date fixe.</summary>
internal static class TenancyTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public static (Organization Organization, Guid OwnerId) OrganizationWithOwner()
    {
        var ownerId = Guid.CreateVersion7();
        return (Organization.Create("Acme", Slug.From("acme"), ownerId), ownerId);
    }

    // Invitation en attente dont le lien est parti (hash posé par SendInvitationEmailHandler).
    public static Invitation PendingInvitation(OrganizationId? organizationId = null, DateTimeOffset? expiresAt = null)
    {
        var invitation = Invitation.Create(
            organizationId ?? OrganizationId.From(Guid.CreateVersion7()),
            "invitee@saas.local",
            OrganizationRole.Member,
            Guid.CreateVersion7(),
            Now.AddDays(-1),
            expiresAt ?? Now.AddDays(6)).Value;
        invitation.IssueToken("stored-hash", Now.AddDays(-1)).IsError.ShouldBeFalse();
        return invitation;
    }
}
