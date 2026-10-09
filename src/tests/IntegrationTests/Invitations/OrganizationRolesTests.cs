using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;
using Saas.Infrastructure.Persistence;
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

    // L'Owner d'une organisation vise une autre organisation dans la route, avec une vraie
    // invitation et un vrai membre de celle-ci : 404 sur chaque route, et rien n'y change.
    [Theory]
    [InlineData("GET", "invitations")]
    [InlineData("POST", "invitations")]
    [InlineData("POST", "invitations/{invitation}/resend")]
    [InlineData("DELETE", "invitations/{invitation}")]
    [InlineData("PUT", "members/{member}/role")]
    public async Task Another_organization_in_the_route_returns_404_and_changes_nothing(string method, string route)
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var (otherOrgId, invitationId, memberId) = await OrganizationWithAnInvitationAndAMemberAsync(ct);
        var rowsBefore = await RowsOfAsync(otherOrgId, ct);
        var path = route
            .Replace("{invitation}", invitationId.ToString(), StringComparison.Ordinal)
            .Replace("{member}", memberId.ToString(), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/organizations/{otherOrgId}/{path}");
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { email = UniqueEmail("cross-org"), role = "Admin" });
        }

        var response = await InvitationTestFlow.ClientFor(factory, orgId, ownerId).SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RowsOfAsync(otherOrgId, ct)).ShouldBe(rowsBefore);
    }

    private async Task<(Guid OrgId, Guid InvitationId, Guid MemberId)> OrganizationWithAnInvitationAndAMemberAsync(
        CancellationToken ct)
    {
        var ownerId = Guid.CreateVersion7();
        var memberId = Guid.CreateVersion7();
        var organization = Organization.Create($"Org-{Guid.NewGuid():N}", Slug.From($"org-{Guid.NewGuid():N}"), ownerId);
        organization.AddMember(memberId, OrganizationRole.Member);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().Add(organization);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        var orgId = organization.Id.Value;
        var email = UniqueEmail("invitee");
        var invite = await InvitationTestFlow.ClientFor(factory, orgId, ownerId)
            .PostAsJsonAsync($"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (orgId, factory.Emails.For(email)!.ExtractInvitationLink().InvitationId, memberId);
    }

    // Lignes de l'organisation avec leur version (xmin) : toute écriture sur l'une d'elles la change.
    private async Task<string> RowsOfAsync(Guid orgId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.SqlQuery<string>($"""
            SELECT coalesce(string_agg("Row", ' ' ORDER BY "Row"), '') AS "Value" FROM (
                SELECT 'organization:' || xmin AS "Row" FROM "Organizations" WHERE "Id" = {orgId}
                UNION ALL
                SELECT 'invitation:' || "Id" || ':' || xmin FROM "Invitations" WHERE "OrganizationId" = {orgId}
                UNION ALL
                SELECT 'member:' || "UserId" || ':' || xmin FROM "OrganizationMemberships" WHERE "OrganizationId" = {orgId}
            ) AS "Rows"
            """).SingleAsync(ct);
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@saas.local";
}
