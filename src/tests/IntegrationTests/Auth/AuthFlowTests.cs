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

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password }, ct);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // Vérifie que le refresh fonctionne de bout en bout côté serveur.
        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = auth.RefreshToken }, ct);
        refresh.EnsureSuccessStatusCode();
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>(ct);
        refreshed!.RefreshToken.ShouldNotBe(auth.RefreshToken); // rotation
    }
}
