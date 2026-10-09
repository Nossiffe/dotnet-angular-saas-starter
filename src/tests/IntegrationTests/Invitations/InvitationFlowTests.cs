using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations;
using Saas.Domain.Tenancy;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Invitations;

[Collection("api")]
public sealed class InvitationFlowTests(PostgresApiFactory factory)
{
    private sealed record AcceptResponse(bool IsNewUser);
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record InviteResponse(Guid InvitationId);

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@saas.local";

    // ── Happy path : nouvel utilisateur invité → accept (+password) → login ─────────
    [Fact]
    public async Task Invite_new_user_then_accept_creates_membership_and_allows_login()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("invitee-new");
        const string password = "Inv!tedUser42";

        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);

        var captured = factory.Emails.For(email);
        captured.ShouldNotBeNull();
        var (invitationId, token) = captured!.ExtractInvitationLink();

        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await accept.Content.ReadFromJsonAsync<AcceptResponse>(ct);
        body.ShouldNotBeNull();
        body!.IsNewUser.ShouldBeTrue();

        var login = await anon.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // Le JWT porte le tenant de l'organisation invitée → membership effectif.
        ReadJwtClaim(auth.AccessToken, "tenant").ShouldBe(orgId.ToString());
        ReadJwtClaim(auth.AccessToken, "tenant_role").ShouldBe("Member");
    }

    // ── Happy path : utilisateur existant → accept sans password ────────────────────
    [Fact]
    public async Task Invite_existing_user_then_accept_without_password()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("invitee-existing");
        await AuthTestFlow.RegisterAndConfirmAsync(anon, factory, email, "Existing!User42", ct);

        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);

        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();

        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await accept.Content.ReadFromJsonAsync<AcceptResponse>(ct);
        body.ShouldNotBeNull();
        body!.IsNewUser.ShouldBeFalse();
    }

    // ── Doublon d'invitation pending → 409 ──────────────────────────────────────────
    // InvitationErrors.AlreadyPending est un Error.Conflict, comme la branche 23505 du
    // GlobalExceptionHandler qui couvre la même règle en cas de course.
    [Fact]
    public async Task Duplicate_pending_invite_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);

        var email = UniqueEmail("dup");
        var first = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── Déjà membre → 400 ───────────────────────────────────────────────────────────
    [Fact]
    public async Task Invite_when_already_member_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("already");
        (await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();
        (await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password = "Member!User42" }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Réinviter le même email (désormais membre) → 400.
        var reinvite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        reinvite.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Revoke puis accept → 400 ────────────────────────────────────────────────────
    [Fact]
    public async Task Revoke_then_accept_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("revoke");
        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await invite.Content.ReadFromJsonAsync<InviteResponse>(ct);
        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();

        var revoke = await owner.DeleteAsync(
            $"/api/v1/organizations/{orgId}/invitations/{created!.InvitationId}", ct);
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password = "Revoked!User42" }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Resend invalide l'ancien token ──────────────────────────────────────────────
    [Fact]
    public async Task Resend_invalidates_previous_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("resend");
        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await invite.Content.ReadFromJsonAsync<InviteResponse>(ct);
        var (invitationId, token1) = factory.Emails.For(email)!.ExtractInvitationLink();

        var resend = await owner.PostAsync(
            $"/api/v1/organizations/{orgId}/invitations/{created!.InvitationId}/resend", content: null, ct);
        resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var (_, token2) = factory.Emails.For(email)!.ExtractInvitationLink();
        token2.ShouldNotBe(token1);

        // L'ancien token n'est plus valide → 400.
        var acceptOld = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token = token1, password = "Resend!User42" }, ct);
        acceptOld.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Le nouveau token fonctionne → 200.
        var acceptNew = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token = token2, password = "Resend!User42" }, ct);
        acceptNew.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── List pending (3 invitées, 1 révoquée → 2) ───────────────────────────────────
    [Fact]
    public async Task List_pending_excludes_revoked()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);

        var emails = new[] { UniqueEmail("list-a"), UniqueEmail("list-b"), UniqueEmail("list-c") };
        Guid toRevoke = Guid.Empty;
        foreach (var email in emails)
        {
            var resp = await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            var created = await resp.Content.ReadFromJsonAsync<InviteResponse>(ct);
            toRevoke = created!.InvitationId; // garde la dernière → on en révoque une
        }

        (await owner.DeleteAsync($"/api/v1/organizations/{orgId}/invitations/{toRevoke}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await owner.GetFromJsonAsync<List<InvitationDto>>(
            $"/api/v1/organizations/{orgId}/invitations", ct);
        list.ShouldNotBeNull();
        list!.Count.ShouldBe(2);
        list.ShouldAllBe(i => i.Email != null);
    }

    // ── ChangeMemberRole → Admin (vérifié au re-login) ──────────────────────────────
    [Fact]
    public async Task ChangeMemberRole_promotes_member_to_admin_reflected_on_relogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("promote");
        const string password = "Promote!User42";
        (await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();
        (await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Récupère le userId du membre via un premier login (claim sub).
        var firstLogin = await anon.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password, tenantId = orgId }, ct);
        firstLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstAuth = await firstLogin.Content.ReadFromJsonAsync<AuthResponse>(ct);
        var memberUserId = Guid.Parse(ReadJwtClaim(firstAuth!.AccessToken, "sub"));
        ReadJwtClaim(firstAuth.AccessToken, "tenant_role").ShouldBe("Member");

        // Owner promeut → Admin.
        var change = await owner.PutAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members/{memberUserId}/role", new { role = "Admin" }, ct);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Re-login → le claim tenant_role reflète le nouveau rôle.
        var reLogin = await anon.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password, tenantId = orgId }, ct);
        reLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reAuth = await reLogin.Content.ReadFromJsonAsync<AuthResponse>(ct);
        ReadJwtClaim(reAuth!.AccessToken, "tenant_role").ShouldBe("Admin");
    }

    // ── Non-Owner ne peut pas inviter → 403 ─────────────────────────────────────────
    [Fact]
    public async Task Non_owner_cannot_invite_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var member = InvitationTestFlow.ClientFor(factory, orgId, Guid.CreateVersion7(), tenantRole: "Member");

        var invite = await member.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email = UniqueEmail("forbidden"), role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── orgId de la route ≠ tenant du token → 404 (anti-énumération) ────────────────
    // OrganizationScopeFilter renvoie OrganizationErrors.NotFound : la réponse est la même que
    // pour une organisation inexistante, et ne révèle pas qu'une autre organisation existe.
    [Fact]
    public async Task Invite_with_mismatched_orgId_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var otherOrgId = Guid.CreateVersion7();

        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{otherOrgId}/invitations", new { email = UniqueEmail("mismatch"), role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Token expiré → 400 (ExpiresAt mis dans le passé en base) ────────────────────
    [Fact]
    public async Task Expired_invitation_accept_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("expired");
        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await invite.Content.ReadFromJsonAsync<InviteResponse>(ct);
        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();

        // Force l'expiration en base via un scope AppDbContext.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.Database.ExecuteSqlAsync(
                $"""UPDATE "Invitations" SET "ExpiresAt" = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE "Id" = {created!.InvitationId}""",
                ct);
            rows.ShouldBe(1);
        }

        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password = "Expired!User42" }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Invitation expirée + nouvel email → 400 et aucun compte orphelin ──
    // Régression : si le compte Identity était créé avant la vérification d'expiration, un
    // early-return ErrorOr ne déclenche pas le rollback de la transaction (seule une exception le
    // fait) : le compte resterait en base malgré l'échec de l'acceptation. On vérifie ici
    // qu'une tentative de connexion avec ce couple email/mot de passe échoue.
    [Fact]
    public async Task Expired_invitation_with_new_email_creates_no_orphan_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, orgId, ownerId);
        var anon = factory.CreateClient();

        var email = UniqueEmail("orphan");
        const string password = "Orphan!User42";
        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await invite.Content.ReadFromJsonAsync<InviteResponse>(ct);
        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();

        // Force l'expiration en base via un scope AppDbContext.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.Database.ExecuteSqlAsync(
                $"""UPDATE "Invitations" SET "ExpiresAt" = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE "Id" = {created!.InvitationId}""",
                ct);
            rows.ShouldBe(1);
        }

        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token, password }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var login = await anon.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Accept idempotent : invitee déjà membre → succès silencieux (comportement d'origine) ──
    // On seed directement une invitation Pending pour un email déjà membre de l'organisation
    // (impossible à obtenir via l'endpoint d'invite, qui refuse avec AlreadyMember — cf.
    // Invite_when_already_member_returns_400) pour reproduire le cas concurrentiel/legacy où
    // l'invitee accepte alors qu'il est déjà membre : Organization.AddMember est un no-op idempotent,
    // l'invitation passe quand même à Accepted, et l'endpoint renvoie 200.
    [Fact]
    public async Task Accept_when_invitee_already_member_is_idempotent_success()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var anon = factory.CreateClient();

        var email = UniqueEmail("idempotent");
        const string password = "Idempotent!User42";
        await AuthTestFlow.RegisterAndConfirmAsync(anon, factory, email, password, ct);

        Guid invitationId;
        string rawToken;
        using (var scope = factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var invitationsRepo = scope.ServiceProvider.GetRequiredService<IInvitationRepository>();
            var tokens = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var user = await identity.FindByEmailAsync(email, ct);
            user.ShouldNotBeNull();

            var organization = await organizations.GetByIdAsync(OrganizationId.From(orgId), ct);
            organization.ShouldNotBeNull();
            organization!.AddMember(user!.Id, OrganizationRole.Member);
            await uow.SaveChangesAsync(ct);

            rawToken = tokens.GenerateRawToken();
            var created = Invitation.Create(
                OrganizationId.From(orgId),
                email,
                OrganizationRole.Member,
                ownerId,
                tokens.HashToken(rawToken),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(7));
            created.IsError.ShouldBeFalse();
            invitationsRepo.Add(created.Value);
            await uow.SaveChangesAsync(ct);
            invitationId = created.Value.Id.Value;
        }

        var accept = await anon.PostAsJsonAsync(
            "/api/v1/invitations/accept", new { invitationId, token = rawToken }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await accept.Content.ReadFromJsonAsync<AcceptResponse>(ct);
        body.ShouldNotBeNull();
        body!.IsNewUser.ShouldBeFalse();
    }

    /// <summary>Décode le payload d'un JWT (base64url) et lit un claim par nom.</summary>
    private static string ReadJwtClaim(string jwt, string claim)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(claim).GetString()!;
    }
}
