using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class OrganizationTests
{
    [Fact]
    public void Create_adds_the_owner_as_member()
    {
        var ownerId = Guid.CreateVersion7();

        var organization = Organization.Create("Acme", Slug.From("Acme Corp"), ownerId);

        organization.Name.ShouldBe("Acme");
        organization.Slug.Value.ShouldBe("acme-corp");
        organization.Memberships.Count.ShouldBe(1);
        organization.Memberships[0].UserId.ShouldBe(ownerId);
        organization.Memberships[0].Role.ShouldBe(OrganizationRole.Owner);
    }

    [Fact]
    public void AddMember_is_idempotent_per_user()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId);

        organization.AddMember(ownerId, OrganizationRole.Member);

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
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId);
        organization.AddMember(memberId, OrganizationRole.Member);

        var result = organization.ChangeMemberRole(memberId, OrganizationRole.Admin);

        result.IsError.ShouldBeFalse();
        organization.Memberships.First(m => m.UserId == memberId).Role.ShouldBe(OrganizationRole.Admin);
    }

    [Fact]
    public void Demoting_the_last_owner_returns_an_error()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId);

        var result = organization.ChangeMemberRole(ownerId, OrganizationRole.Member);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(OrganizationErrors.LastOwner);
    }

    [Fact]
    public void Changing_the_role_of_an_unknown_user_returns_an_error()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId);

        var result = organization.ChangeMemberRole(Guid.CreateVersion7(), OrganizationRole.Admin);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(OrganizationErrors.MemberNotFound);
    }
}
