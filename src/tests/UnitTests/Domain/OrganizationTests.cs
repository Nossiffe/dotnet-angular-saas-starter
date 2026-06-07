using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class OrganizationTests
{
    [Fact]
    public void Create_ajoute_un_membre_Owner()
    {
        var ownerId = Guid.CreateVersion7();

        var organization = Organization.Create("Acme", Slug.From("Acme Corp"), ownerId);

        organization.Name.ShouldBe("Acme");
        organization.Slug.Value.ShouldBe("acme-corp");
        organization.Memberships.Count.ShouldBe(1);
        organization.Memberships[0].UserId.ShouldBe(ownerId);
        organization.Memberships[0].Role.ShouldBe("Owner");
    }

    [Fact]
    public void AddMember_est_idempotent_par_utilisateur()
    {
        var ownerId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme", Slug.From("acme"), ownerId);

        organization.AddMember(ownerId, "Member");

        organization.Memberships.Count.ShouldBe(1);
    }

    [Fact]
    public void Slug_vide_lance() =>
        Should.Throw<Vogen.ValueObjectValidationException>(() => Slug.From(" "));
}
