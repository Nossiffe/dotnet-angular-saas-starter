using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Domain.Identity;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Invitations;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("api")]
public sealed class AuthHardeningTests(PostgresApiFactory factory)
{
    private const string Password = "Sup3rSecret!!42";

    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [Fact]
    public async Task Malformed_confirmation_token_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = UniqueEmail("malformed-confirm");
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);
        var (userId, _) = factory.Emails.For(email)!.ExtractLink();

        // "a" n'est pas un Base64Url valide : le décodage échouait et renvoyait un 500.
        var confirm = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, token = "a" }, ct);

        confirm.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Malformed_reset_token_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = UniqueEmail("malformed-reset");
        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, Password, ct);
        var (userId, _) = factory.Emails.For(email)!.ExtractLink();

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new { userId, token = "a", newPassword = "N3wSecret!!42" }, ct);

        reset.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Sans compte, ou compte verrouillé, la connexion répondait sans calculer de hash PBKDF2 : en
    // quelques millisecondes au lieu d'environ 400, ce qui révélait les e-mails inscrits. Le test
    // vérifie le comportement, pas une durée : chaque échec vérifie le mot de passe soumis, une fois.
    [Theory]
    [InlineData("unknown-email")]
    [InlineData("locked-account")]
    [InlineData("wrong-password")]
    public async Task Every_failed_login_verifies_the_submitted_password_once(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = UniqueEmail(scenario);
        if (!string.Equals(scenario, "unknown-email", StringComparison.Ordinal))
        {
            await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, Password, ct);
        }

        if (string.Equals(scenario, "locked-account", StringComparison.Ordinal))
        {
            for (var i = 0; i < 5; i++)
            {
                await LoginAsync(client, email, "WrongPass!!00", HttpStatusCode.Unauthorized, ct);
            }
        }

        var probe = $"Probe!{Guid.NewGuid():N}";
        await LoginAsync(client, email, probe, HttpStatusCode.Unauthorized, ct);

        factory.VerifiedPasswords.Count(p => string.Equals(p, probe, StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Password_reset_revokes_open_sessions()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthTestFlow.HttpsClient(factory);
        var email = UniqueEmail("reset-revokes");
        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, Password, ct);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessionCookie = AuthTestFlow.ExtractRefreshCookieValue(login);

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, ct);
        forgot.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (userId, token) = factory.Emails.For(email)!.ExtractLink();
        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new { userId, token, newPassword = "N3wSecret!!42" }, ct);
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refresh = await AuthTestFlow.RawCookieClient(factory, sessionCookie)
            .PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Le rafraîchissement accordait le rôle Member à un utilisateur retiré de l'organisation.
    [Fact]
    public async Task Refresh_fails_after_removal_from_the_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthTestFlow.HttpsClient(factory);
        var email = UniqueEmail("removed-member");
        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, Password, ct);
        var (userId, _) = factory.Emails.For(email)!.ExtractLink();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"""DELETE FROM "OrganizationMemberships" WHERE "UserId" = {userId}""", ct);
        }

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Le refresh token émis par la rotation est révoqué lui aussi : plus aucune session active.
        using var check = factory.Services.CreateScope();
        var activeSessions = await check.ServiceProvider.GetRequiredService<AppDbContext>()
            .Set<RefreshToken>()
            .CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct);
        activeSessions.ShouldBe(0);
    }

    [Fact]
    public async Task Login_without_tenant_uses_the_oldest_membership()
    {
        var ct = TestContext.Current.CancellationToken;
        var anon = factory.CreateClient();
        var email = UniqueEmail("two-orgs");
        await AuthTestFlow.RegisterAndConfirmAsync(anon, factory, email, Password, ct);
        var firstLogin = await LoginAsync(anon, email, tenantId: null, ct);
        var ownOrgId = ReadClaim(firstLogin.AccessToken, "tenant");

        var ownerId = Guid.CreateVersion7();
        var otherOrgId = await InvitationTestFlow.SeedOrganizationAsync(factory, ownerId, ct);
        var owner = InvitationTestFlow.ClientFor(factory, otherOrgId, ownerId);
        var invite = await owner.PostAsJsonAsync(
            $"/api/v1/organizations/{otherOrgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var (invitationId, token) = factory.Emails.For(email)!.ExtractInvitationLink();
        var accept = await anon.PostAsJsonAsync("/api/v1/invitations/accept", new { invitationId, token }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);

        var withoutTenant = await LoginAsync(anon, email, tenantId: null, ct);
        var withTenant = await LoginAsync(anon, email, otherOrgId, ct);

        ReadClaim(withoutTenant.AccessToken, "tenant").ShouldBe(ownOrgId);
        ReadClaim(withTenant.AccessToken, "tenant").ShouldBe(otherOrgId.ToString());
    }

    private static async Task LoginAsync(
        HttpClient client, string email, string password, HttpStatusCode expected, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        response.StatusCode.ShouldBe(expected);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email, Guid? tenantId, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password, tenantId }, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(ct))!;
    }

    private static string ReadClaim(string jwt, string claim)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return doc.RootElement.GetProperty(claim).GetString()!;
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@saas.local";
}
