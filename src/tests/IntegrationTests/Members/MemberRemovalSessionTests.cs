using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Saas.IntegrationTests.Auth;
using Saas.IntegrationTests.Outbox;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Members;

// Ce qu'un retrait fait aux sessions du membre retiré, avec de vrais JWT. L'access token émis
// avant le retrait est refusé (adhésion relue à chaque requête), le refresh aussi (adhésion
// vérifiée avant la rotation), et les refresh tokens restants sont révoqués par l'outbox.
[Collection("real-jwt")]
public sealed class MemberRemovalSessionTests(RealJwtApiFactory factory)
{
    private const string Password = "Sup3rSecret!!42";

    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);

    // Le scénario que la relecture de l'adhésion empêche : sans elle, le token d'Admin d'avant le
    // retrait suffisait à inviter de nouveau sa propre adresse comme Admin.
    [Fact]
    public async Task A_removed_admin_cannot_invite_themselves_again_with_their_old_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithALoggedInMemberAsync("Admin", ct);

        var removal = await org.Owner.DeleteAsync($"/api/v1/organizations/{org.Id}/members/{org.Member.UserId}", ct);
        var invite = await Bearer(org.Member.AccessToken).PostAsJsonAsync(
            $"/api/v1/organizations/{org.Id}/invitations", new { email = org.MemberEmail, role = "Admin" }, ct);
        var pending = await org.Owner.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org.Id}/invitations", ct);

        removal.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        invite.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(invite, ct)).ShouldBe("Auth.SessionExpired");
        pending.EnumerateArray().Select(i => i.GetProperty("email").GetString())
            .Contains(org.MemberEmail, StringComparer.OrdinalIgnoreCase).ShouldBeFalse();
    }

    // Avant le retrait, ce token lisait les produits (vérifié par la mise en place).
    [Fact]
    public async Task An_access_token_issued_before_the_removal_gets_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithALoggedInMemberAsync("Member", ct);

        var removal = await org.Owner.DeleteAsync($"/api/v1/organizations/{org.Id}/members/{org.Member.UserId}", ct);
        var products = await Bearer(org.Member.AccessToken).GetAsync("/api/v1/products", ct);

        removal.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        products.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(products, ct)).ShouldBe("Auth.SessionExpired");
    }

    [Fact]
    public async Task A_removed_member_loses_their_session_and_cannot_log_in_to_the_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithALoggedInMemberAsync("Member", ct);
        var membersBefore = await MemberIdsAsync(org, ct);

        var removal = await org.Owner.DeleteAsync($"/api/v1/organizations/{org.Id}/members/{org.Member.UserId}", ct);
        var refresh = await org.MemberDevice.PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email = org.MemberEmail, password = Password, tenantId = org.Id }, ct);

        membersBefore.ShouldContain(org.Member.UserId);
        removal.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        refresh.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("rt=;", StringComparison.Ordinal));
        // Sans autre organisation, plus de connexion possible : la connexion exige une adhésion.
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await MemberIdsAsync(org, ct)).ShouldBe(membersBefore.Where(id => id != org.Member.UserId), ignoreOrder: true);
    }

    // Aucun appareil du membre n'a tenté de refresh entre le retrait et la nouvelle invitation :
    // sans la révocation par l'outbox, son ancien cookie redevenait valable une fois membre de
    // nouveau.
    [Fact]
    public async Task A_member_invited_again_after_a_removal_does_not_get_their_old_session_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithALoggedInMemberAsync("Member", ct);

        var removal = await org.Owner.DeleteAsync($"/api/v1/organizations/{org.Id}/members/{org.Member.UserId}", ct);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        // Le compte existe déjà : l'acceptation se fait sans mot de passe.
        await InviteAndAcceptAsync(org.Owner, org.Id, org.MemberEmail, "Member", password: null, ct);
        var refreshWithOldCookie = await org.MemberDevice.PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);

        removal.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refreshWithOldCookie.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // Le membre l'est bien de nouveau (sa connexion à l'organisation réussit, LoginAsync exige
        // un 200) : le refus vient de la révocation, pas de l'adhésion.
        await LoginAsync(factory.CreateClient(), org.MemberEmail, org.Id, ct);
    }

    // Les dates d'arrivée viennent de l'inscription de l'Owner et de l'acceptation de l'invité.
    [Fact]
    public async Task The_arrival_dates_are_the_registration_and_the_acceptance()
    {
        var ct = TestContext.Current.CancellationToken;
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var org = await OrganizationWithALoggedInMemberAsync("Member", ct);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        var members = (await org.Owner.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org.Id}/members", ct))
            .EnumerateArray().ToList();

        members.Count.ShouldBe(2);
        var ownerJoinedAt = members[0].GetProperty("joinedAt").GetDateTimeOffset();
        var memberJoinedAt = members[1].GetProperty("joinedAt").GetDateTimeOffset();
        members[1].GetProperty("userId").GetGuid().ShouldBe(org.Member.UserId);
        ownerJoinedAt.ShouldBeInRange(before, after);
        memberJoinedAt.ShouldBeInRange(ownerJoinedAt, after);
    }

    private sealed record LoggedInMember(Guid UserId, string AccessToken);

    private sealed record OrganizationWithMember(
        Guid Id, HttpClient Owner, LoggedInMember Member, HttpClient MemberDevice, string MemberEmail);

    // Un Owner inscrit et connecté ; un invité au rôle donné qui accepte, crée son compte et se
    // connecte, avec son cookie de refresh sur son propre client.
    private async Task<OrganizationWithMember> OrganizationWithALoggedInMemberAsync(string role, CancellationToken ct)
    {
        var anon = factory.CreateClient();
        var ownerEmail = UniqueEmail("owner");
        await AuthTestFlow.RegisterAndConfirmAsync(anon, factory, ownerEmail, Password, ct);
        var ownerAuth = await LoginAsync(anon, ownerEmail, tenantId: null, ct);
        var orgId = Guid.Parse(ReadClaim(ownerAuth.AccessToken, "tenant"));
        var owner = Bearer(ownerAuth.AccessToken);

        var memberEmail = UniqueEmail("member");
        await InviteAndAcceptAsync(owner, orgId, memberEmail, role, Password, ct);

        var memberDevice = AuthTestFlow.HttpsClient(factory);
        var memberAuth = await LoginAsync(memberDevice, memberEmail, orgId, ct);
        ReadClaim(memberAuth.AccessToken, "tenant_role").ShouldBe(role);
        var member = new LoggedInMember(Guid.Parse(ReadClaim(memberAuth.AccessToken, "sub")), memberAuth.AccessToken);
        (await Bearer(member.AccessToken).GetAsync("/api/v1/products", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return new OrganizationWithMember(orgId, owner, member, memberDevice, memberEmail);
    }

    private async Task InviteAndAcceptAsync(
        HttpClient owner, Guid orgId, string email, string role, string? password, CancellationToken ct)
    {
        var invite = await owner.PostAsJsonAsync($"/api/v1/organizations/{orgId}/invitations", new { email, role }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (invitationId, token) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();
        var accept = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<List<Guid>> MemberIdsAsync(OrganizationWithMember org, CancellationToken ct) =>
        (await org.Owner.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org.Id}/members", ct))
            .EnumerateArray().Select(m => m.GetProperty("userId").GetGuid()).ToList();

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
