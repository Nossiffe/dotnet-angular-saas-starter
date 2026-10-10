using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Saas.IntegrationTests.Outbox;
using Saas.IntegrationTests.Products;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

// Un access token garde le rôle qu'il portait à son émission. Avec de vrais JWT, un membre dont
// l'Owner vient de changer le rôle présente son token d'avant. Les routes rattachées à une
// organisation relisent l'adhésion en base : rôle différent du token, 401 ; le refresh relit le
// rôle en base et donne un token au rôle actuel, sans nouvelle connexion.
[Collection("real-jwt")]
public sealed class StaleRoleTests(RealJwtApiFactory factory)
{
    private const string Password = "Sup3rSecret!!42";

    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);

    // Sans revérification, le token d'Admin suffisait à inviter une autre adresse comme Admin :
    // après acceptation, l'ancien Admin gardait un accès Admin durable.
    [Fact]
    public async Task A_demoted_admin_cannot_use_their_old_token_to_invite_an_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithARoleChangeAsync("Admin", "Member", ct);
        var otherAddress = UniqueEmail("other-address");

        var invite = await Bearer(org.OldToken).PostAsJsonAsync(
            $"/api/v1/organizations/{org.Id}/invitations", new { email = otherAddress, role = "Admin" }, ct);
        var pending = await org.Owner.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org.Id}/invitations", ct);

        invite.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(invite, ct)).ShouldBe("Auth.SessionExpired");
        pending.EnumerateArray().Select(i => i.GetProperty("email").GetString())
            .Contains(otherAddress, StringComparer.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public async Task A_demoted_admin_cannot_use_their_old_token_to_delete_a_product()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithARoleChangeAsync("Admin", "Member", ct);
        var productId = await org.Owner.CreateProductAsync($"Stale-{Guid.NewGuid():N}", 1m, ct);

        var delete = await Bearer(org.OldToken).DeleteAsync($"/api/v1/products/{productId}", ct);
        var product = await org.Owner.GetAsync($"/api/v1/products/{productId}", ct);

        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        product.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Ce que fait le front sur un 401 : il rafraîchit la session et rejoue la requête avec le
    // nouveau token. Le refresh relit le rôle en base : le nouveau token dit Member, et la
    // requête rejouée n'obtient pas un second 401.
    [Fact]
    public async Task After_the_401_the_refreshed_session_of_a_demoted_admin_carries_the_member_role()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithARoleChangeAsync("Admin", "Member", ct);

        var withOldToken = await Bearer(org.OldToken).GetAsync("/api/v1/products", ct);
        var refreshed = await RefreshAsync(org.Device, ct);
        var withNewToken = Bearer(refreshed.AccessToken);

        withOldToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ReadClaim(refreshed.AccessToken, "tenant_role").ShouldBe("Member");
        (await withNewToken.GetAsync("/api/v1/products", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await withNewToken.GetAsync($"/api/v1/organizations/{org.Id}/invitations", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Même mécanique dans l'autre sens : un Member promu Admin obtient ses nouveaux droits au
    // prochain refresh, sans se reconnecter. Son ancien token reçoit 401 et non 403, si bien que le
    // front rafraîchit la session au lieu d'afficher un refus.
    [Fact]
    public async Task A_member_promoted_to_admin_gets_the_new_role_by_refreshing_without_logging_in_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithARoleChangeAsync("Member", "Admin", ct);

        var withOldToken = await Bearer(org.OldToken).GetAsync($"/api/v1/organizations/{org.Id}/invitations", ct);
        var refreshed = await RefreshAsync(org.Device, ct);
        var withNewToken = await Bearer(refreshed.AccessToken).GetAsync($"/api/v1/organizations/{org.Id}/invitations", ct);

        withOldToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ReadClaim(refreshed.AccessToken, "tenant_role").ShouldBe("Admin");
        withNewToken.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record RoleChangedOrganization(Guid Id, HttpClient Owner, string OldToken, HttpClient Device);

    // Un Owner inscrit ; un invité qui accepte et se connecte sur son propre client (cookie de
    // refresh compris) ; puis l'Owner change son rôle. Le token de l'invité porte encore l'ancien.
    private async Task<RoleChangedOrganization> OrganizationWithARoleChangeAsync(
        string invitedRole, string newRole, CancellationToken ct)
    {
        var anon = factory.CreateClient();
        var ownerEmail = UniqueEmail("owner");
        await AuthTestFlow.RegisterAndConfirmAsync(anon, factory, ownerEmail, Password, ct);
        var ownerAuth = await LoginAsync(anon, ownerEmail, tenantId: null, ct);
        var orgId = Guid.Parse(ReadClaim(ownerAuth.AccessToken, "tenant"));
        var owner = Bearer(ownerAuth.AccessToken);

        var inviteeEmail = UniqueEmail("invitee");
        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email = inviteeEmail, role = invitedRole }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (invitationId, token) = factory.Emails.For(inviteeEmail).ShouldNotBeNull().ExtractInvitationLink();
        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password = Password }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);

        var device = AuthTestFlow.HttpsClient(factory);
        var inviteeAuth = await LoginAsync(device, inviteeEmail, orgId, ct);
        ReadClaim(inviteeAuth.AccessToken, "tenant_role").ShouldBe(invitedRole);
        var inviteeId = Guid.Parse(ReadClaim(inviteeAuth.AccessToken, "sub"));

        var change = await owner.PutAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members/{inviteeId}/role", new { role = newRole }, ct);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return new RoleChangedOrganization(orgId, owner, inviteeAuth.AccessToken, device);
    }

    private static async Task<AuthResponse> RefreshAsync(HttpClient device, CancellationToken ct)
    {
        var refresh = await device.PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await refresh.Content.ReadFromJsonAsync<AuthResponse>(ct)).ShouldNotBeNull();
    }

    private HttpClient Bearer(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email, Guid? tenantId, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password, tenantId }, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(ct)).ShouldNotBeNull();
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response, CancellationToken ct) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString();

    private static string ReadClaim(string jwt, string claim)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return doc.RootElement.GetProperty(claim).GetString()!;
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@saas.local";
}
