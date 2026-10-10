using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class OrganizationTests
{
    private static readonly DateTimeOffset _createdAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _joinedAt = _createdAt.AddDays(3);

    [Fact]
    public void Create_adds_the_owner_as_member()
    {
        var ownerId = Guid.CreateVersion7();

        var organization = Organization.Create("Acme", Slug.From("Acme Corp"), ownerId, _createdAt);

        organization.Name.ShouldBe("Acme");
        organization.Slug.Value.ShouldBe("acme-corp");
        organization.Memberships.Count.ShouldBe(1);
        organization.Memberships[0].UserId.ShouldBe(ownerId);
        organization.Memberships[0].Role.ShouldBe(OrganizationRole.Owner);
        organization.Memberships[0].JoinedAt.ShouldBe(_createdAt);
    }

    [Fact]
    public void AddMember_records_the_arrival_date()
    {
        var organization = Organization.Create("Acme", Slug.From("acme"), Guid.CreateVersion7(), _createdAt);
        var memberId = Guid.CreateVersion7();

        organization.AddMember(memberId, OrganizationRole.Member, _joinedAt);

        organization.Memberships.Single(m => m.UserId == memberId).JoinedAt.ShouldBe(_joinedAt);
    }

    [Fact]
    public void AddMember_is_idempotent_per_user()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId, _createdAt);

        organization.AddMember(ownerId, OrganizationRole.Member, _joinedAt);

        organization.Memberships.Count.ShouldBe(1);
    }

    [Fact]
    public void An_empty_slug_throws() =>
        Should.Throw<Vogen.ValueObjectValidationException>(() => Slug.From(" "));

    [Fact]
    public void ChangeMemberRole_promotes_a_member()
    {
        var ownerId = Guid.CreateVersion7();
        var memberId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId, _createdAt);
        organization.AddMember(memberId, OrganizationRole.Member, _joinedAt);

        var result = organization.ChangeMemberRole(memberId, OrganizationRole.Admin);

        result.IsError.ShouldBeFalse();
        organization.Memberships.First(m => m.UserId == memberId).Role.ShouldBe(OrganizationRole.Admin);
    }

    [Fact]
    public void Demoting_the_last_owner_returns_an_error()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId, _createdAt);

        var result = organization.ChangeMemberRole(ownerId, OrganizationRole.Member);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(OrganizationErrors.LastOwner);
    }

    [Fact]
    public void Changing_the_role_of_an_unknown_user_returns_an_error()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId, _createdAt);

        var result = organization.ChangeMemberRole(Guid.CreateVersion7(), OrganizationRole.Admin);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(OrganizationErrors.MemberNotFound);
    }

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin)]
    public void RemoveMember_removes_a_member_or_an_admin_and_raises_an_event(OrganizationRole role)
    {
        var organization = Organization.Create("Acme", Slug.From("acme"), Guid.CreateVersion7(), _createdAt);
        var memberId = Guid.CreateVersion7();
        organization.AddMember(memberId, role, _joinedAt);
        organization.ClearDomainEvents();

        var result = organization.RemoveMember(memberId);

        result.IsError.ShouldBeFalse();
        organization.Memberships.ShouldNotContain(m => m.UserId == memberId);
        organization.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new MemberRemovedDomainEvent(organization.Id, memberId, role));
    }

    // Ni le seul Owner (qui se retirerait lui-même), ni un second Owner : l'organisation ne perd
    // jamais un Owner par un retrait.
    [Fact]
    public void RemoveMember_refuses_to_remove_the_only_owner()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId, _createdAt);
        organization.ClearDomainEvents();

        var result = organization.RemoveMember(ownerId);

        result.FirstError.ShouldBe(OrganizationErrors.CannotRemoveOwner);
        organization.Memberships.ShouldContain(m => m.UserId == ownerId);
        organization.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveMember_refuses_to_remove_another_owner()
    {
        var organization = Organization.Create("Acme", Slug.From("acme"), Guid.CreateVersion7(), _createdAt);
        var secondOwnerId = Guid.CreateVersion7();
        organization.AddMember(secondOwnerId, OrganizationRole.Owner, _joinedAt);

        var result = organization.RemoveMember(secondOwnerId);

        result.FirstError.ShouldBe(OrganizationErrors.CannotRemoveOwner);
        organization.Memberships.ShouldContain(m => m.UserId == secondOwnerId);
    }

    [Fact]
    public void Removing_an_unknown_user_returns_an_error()
    {
        var organization = Organization.Create("Acme", Slug.From("acme"), Guid.CreateVersion7(), _createdAt);

        var result = organization.RemoveMember(Guid.CreateVersion7());

        result.FirstError.ShouldBe(OrganizationErrors.MemberNotFound);
        organization.Memberships.Count.ShouldBe(1);
    }
}
