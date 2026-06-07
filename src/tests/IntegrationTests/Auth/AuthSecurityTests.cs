using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("api")]
public sealed class AuthSecurityTests(PostgresApiFactory factory)
{
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);

    // ── Email dupliqué → 400 ─────────────────────────────────────────────
    [Fact]
    public async Task Duplicate_email_register_renvoie_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        // Première inscription → 201
        var first = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Deuxième inscription avec le même email → 400 (email déjà pris)
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Mauvais password → 401 ───────────────────────────────────────────
    [Fact]
    public async Task Mauvais_password_login_renvoie_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"badpwd-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        // Login avec un mauvais mot de passe → 401
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = "WrongPass!!00" }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Refresh-token reuse → révocation famille ─────────────────────────
    [Fact]
    public async Task Refresh_token_reuse_declenche_revocation_famille()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"reuse-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        // Login → obtenir le premier refresh token
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth1 = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth1.ShouldNotBeNull();
        var originalRefreshToken = auth1!.RefreshToken;

        // Premier refresh légitime → rotation (nouveau token, l'ancien est révoqué)
        var refresh1 = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = originalRefreshToken }, ct);
        refresh1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth2 = await refresh1.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth2.ShouldNotBeNull();
        var currentRefreshToken = auth2!.RefreshToken;

        // Vérifier que la rotation a bien eu lieu
        currentRefreshToken.ShouldNotBe(originalRefreshToken);

        // Re-présenter l'ANCIEN token révoqué → détection de réutilisation
        // → révocation de tous les tokens actifs de l'utilisateur → null → 401
        var reuseAttempt = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = originalRefreshToken }, ct);
        reuseAttempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Le token COURANT (issu de la rotation légale) doit aussi être révoqué
        // par la cascade de révocation famille déclenchée par la réutilisation
        var currentAfterRevocation = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = currentRefreshToken }, ct);
        currentAfterRevocation.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
