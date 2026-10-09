using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Invitations;

// Les politiques testées sont celles de l'application : la factory ne les redéclare pas.
[Collection("api")]
public sealed class OrganizationRolesTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Admin_can_invite_and_list_invitations()
    {
        var ct = TestContext.Current.CancellationToken;
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);
        var admin = InvitationTestFlow.ClientFor(factory, orgId, Guid.CreateVersion7(), tenantRole: "Admin");

        var invite = await admin.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email = UniqueEmail("by-admin"), role = "Member" }, ct);
        var list = await admin.GetAsync($"/api/v1/organizations/{orgId}/invitations", ct);

        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_cannot_change_member_roles()
    {
        var ct = TestContext.Current.CancellationToken;
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);
        var admin = InvitationTestFlow.ClientFor(factory, orgId, Guid.CreateVersion7(), tenantRole: "Admin");

        var change = await admin.PutAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members/{Guid.CreateVersion7()}/role", new { role = "Admin" }, ct);

        change.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Member_cannot_list_invitations()
    {
        var ct = TestContext.Current.CancellationToken;
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);
        var member = InvitationTestFlow.ClientFor(factory, orgId, Guid.CreateVersion7(), tenantRole: "Member");

        var list = await member.GetAsync($"/api/v1/organizations/{orgId}/invitations", ct);

        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GET", "invitations")]
    [InlineData("DELETE", "invitations/0190f1e2-0000-7000-8000-000000000001")]
    [InlineData("PUT", "members/0190f1e2-0000-7000-8000-000000000001/role")]
    public async Task Another_organization_in_the_route_returns_404(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var otherOrgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);

        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/organizations/{otherOrgId}/{path}");
        if (string.Equals(method, "PUT", StringComparison.Ordinal))
        {
            request.Content = JsonContent.Create(new { role = "Admin" });
        }

        var response = await owner.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@saas.local";
}
