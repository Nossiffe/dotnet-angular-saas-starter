using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("api")]
public sealed class AuthFlowTests(PostgresApiFactory factory)
{
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);

    [Fact]
    public async Task Register_login_donne_un_token_utilisable()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // Vérifie que le refresh fonctionne de bout en bout côté serveur.
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }, ct);
        refresh.EnsureSuccessStatusCode();
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>(ct);
        refreshed!.RefreshToken.ShouldNotBe(auth.RefreshToken); // rotation
    }

    [Fact]
    public async Task Login_email_non_confirme_est_refuse()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"unconfirmed-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        // Inscription SANS confirmation d'email.
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Login avec un mot de passe valide mais un email non confirmé → refusé (400, message borné).
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Une fois l'email confirmé, le même login réussit.
        await AuthTestFlow.ConfirmEmailAsync(client, factory, email, ct);
        var loginConfirmed = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        loginConfirmed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
