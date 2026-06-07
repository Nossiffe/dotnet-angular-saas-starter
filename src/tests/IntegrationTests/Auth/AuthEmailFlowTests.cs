using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Saas.Domain.Identity;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("api")]
public sealed class AuthEmailFlowTests(PostgresApiFactory factory)
{
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);

    [Fact]
    public async Task Confirm_email_active_le_compte()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"confirm-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var captured = factory.Emails.For(email);
        captured.ShouldNotBeNull();
        var (userId, token) = captured!.ExtractLink();

        var confirm = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email", new { userId, token }, ct);
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        user.ShouldNotBeNull();
        user!.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task Forgot_then_reset_password_permet_de_se_reconnecter()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@saas.local";
        const string oldPassword = "Sup3rSecret!!42";
        const string newPassword = "BrandNewPass!!99";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = oldPassword }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Forgot (email connu) → 200 + email capturé
        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, ct);
        forgot.StatusCode.ShouldBe(HttpStatusCode.OK);

        var captured = factory.Emails.For(email);
        captured.ShouldNotBeNull();
        captured!.Subject.ShouldContain("mot de passe");
        var (userId, token) = captured.ExtractLink();

        // Reset avec le token → 200
        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new { userId, token, newPassword }, ct);
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Login avec le nouveau mot de passe → OK
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = newPassword }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        // L'ancien mot de passe ne fonctionne plus
        var loginOld = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = oldPassword }, ct);
        loginOld.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_email_inconnu_renvoie_200_sans_envoyer_de_mail()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var unknown = $"nobody-{Guid.NewGuid():N}@saas.local";

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = unknown }, ct);

        // Anti-énumération : 200 identique, et aucun email capturé pour cette adresse.
        forgot.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Emails.For(unknown).ShouldBeNull();
    }

    [Fact]
    public async Task Logout_revoque_le_refresh_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"logout-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth.ShouldNotBeNull();

        var logout = await client.PostAsJsonAsync(
            "/api/v1/auth/logout", new { refreshToken = auth!.RefreshToken }, ct);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Le refresh token révoqué n'est plus utilisable → 401.
        var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }, ct);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
